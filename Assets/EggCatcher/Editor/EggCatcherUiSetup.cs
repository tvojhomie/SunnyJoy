using System;
using System.Collections.Generic;
using AlienProject.WebWrapper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggCatcher.Editor
{

    public static class EggCatcherUiSetup
    {
        public static string ScenePath => AssetDatabase.GUIDToAssetPath("8c9cfa26abfee488c85f1582747f6a02");
        private const string SafeAreaName = "SafeAreaContent";
        private const string DefaultActionsPath =
            "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
        private static readonly string[] PanelNames =
        {
            "LoadingPanel", "CreateProfilePanel", "MainMenu", "ProfilePanel", "StatsPanel", "EditProfilePanel", "SkinsPanel", "SettingPanel", "How to playPanel"
        };

        [InitializeOnLoadMethod]
        private static void RegisterPreviewCleanup()
        {
            EditorApplication.playModeStateChanged -= ClearPreviewSlot;
            EditorApplication.playModeStateChanged += ClearPreviewSlot;
        }

        private static void ClearPreviewSlot(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool preview = SessionState.GetBool("EggCatcher.TestsRunning", false) ||
                    !string.IsNullOrEmpty(SessionState.GetString("EggCatcher.PreviewSlot", string.Empty));
                WebWrapperBootstrap.SetEditorPreview(preview);
                var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
                if (ShouldPrepareServerTest(settings, SceneManager.GetActiveScene().path == ScenePath,
                        preview, WebWrapperEditorLiveSession.IsEnabled))
                {

                    WebWrapperEditorLiveSession.Prepare(true, WebWrapperEditorLiveSession.Mode);
                    WebWrapperEditorLiveSession.Trace("Standard Unity Play: fresh Editor QA installation; real server launch.");
                }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                WebWrapperBootstrap.SetEditorPreview(false);
                SessionState.EraseString("EggCatcher.PreviewSlot");
                SessionState.EraseBool("EggCatcher.PreviewGame");
                SessionState.EraseBool("EggCatcher.PreviewPush");
            }
        }

        public static bool ShouldPrepareServerTest(WebWrapperSettings settings, bool isHostScene,
            bool previewOrTests, bool sessionAlreadyPrepared) => isHostScene && !previewOrTests &&
            !sessionAlreadyPrepared && settings != null && settings.AutoStart && !settings.UseMockServices;

        [MenuItem("Tools/Egg Catcher/Preview UI (Separate Test Profile)")]
        public static void PreviewUi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " first.");
            SessionState.SetString("EggCatcher.PreviewSlot", "UiPreview");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Egg Catcher/Preview Shop (2480 Test Eggs)")]
        public static void PreviewShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " first.");

            GameDataStore.Save(new EggCatcherData
            {
                Name = "Speckles", HasProfile = true, Coins = 2480, Level = 7, LevelXp = 340,
                NextLevelXp = 500, BestScore = 1870, GamesPlayed = 48, TotalScore = 41360,
                TotalEggsCaught = 3240, TotalPlaySeconds = 8040, BestCombo = 31,
                CurrentSkinId = "pirate", BackgroundId = "day"
            }, "ShopPreview");
            SessionState.SetString("EggCatcher.PreviewSlot", "ShopPreview");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Egg Catcher/Preview Push Panel (No Real Permissions)")]
        public static void PreviewPush()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " first.");
            SessionState.SetString("EggCatcher.PreviewSlot", "UiPreview");
            SessionState.SetBool("EggCatcher.PreviewPush", true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Egg Catcher/Connect UI Template")]
        public static void ConnectUiTemplate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before connecting the Egg Catcher UI.");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " as the active scene first. No other scene is modified.");

            Canvas canvas = FindTemplateCanvas(scene);
            var panels = ValidatePanels(canvas);
            EventSystem eventSystem = FindEventSystem(scene);

            if (scene.isDirty && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the current UI template; setup was not applied.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Egg Catcher UI Template");
            int wrappersAdded = 0;
            int blankTmpRemoved = 0;
            try
            {
                EggCatcherApp app = canvas.GetComponent<EggCatcherApp>();
                if (app == null)
                    app = Undo.AddComponent<EggCatcherApp>(canvas.gameObject);
                Undo.RecordObject(app, "Assign Egg Catcher canvas");
                var appSerialized = new SerializedObject(app);
                SerializedProperty canvasProperty = appSerialized.FindProperty("canvas");
                if (canvasProperty != null && canvasProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    canvasProperty.objectReferenceValue = canvas;
                    appSerialized.ApplyModifiedProperties();
                }
                app.enabled = true;
                var buttonSpriteProperty = appSerialized.FindProperty("actionButtonSprite");
                if (buttonSpriteProperty != null)
                {
                    buttonSpriteProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/0_UI/Menu/Button_PrimaryGreen.png");
                    appSerialized.ApplyModifiedProperties();
                }
                AssignSprite(appSerialized, "selectedCardSprite", "Assets/0_UI/Skins/Card_Selected.png");
                AssignSprite(appSerialized, "selectedActionSprite", "Assets/0_UI/Skins/Button_Selected.png");
                AssignSprite(appSerialized, "toggleOffSprite", "Assets/0_UI/Skins/Toggle_Off.png");

                foreach (RectTransform panel in panels)
                {
                    if (EnsureSafeArea(panel))
                        wrappersAdded++;
                    Undo.RecordObject(panel.gameObject, "Set initial Egg Catcher screen");
                    panel.gameObject.SetActive(panel.name == "LoadingPanel");
                }

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
                Undo.RecordObject(scaler, "Keep authored portrait canvas scaling");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                Transform badge = canvas.transform.Find("MainMenu/" + SafeAreaName + "/TopPanel/ProfilePanel");
                if (badge != null)
                {
                    Button button = badge.GetComponent<Button>();
                    if (button == null)
                        button = Undo.AddComponent<Button>(badge.gameObject);
                    Undo.RecordObject(button, "Make profile badge interactive");
                    if (button.targetGraphic == null)
                        button.targetGraphic = badge.GetComponent<Image>();
                }
                else
                {
                    throw new InvalidOperationException("The authored MainMenu/TopPanel/ProfilePanel badge is missing.");
                }

                EnsureInputModule(scene, eventSystem);
                EggCatcherGameplaySetup.Connect(scene, canvas, app);
                blankTmpRemoved = RemoveBlankFontlessTmpDuplicates(canvas);
                ConfigurePortraitPlayer();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("UI wiring was applied but the scene could not be saved.");
                AssetDatabase.SaveAssets();
                Debug.Log("[Egg Catcher] UI template connected and saved: " + ScenePath +
                          "; " + panels.Count + " safe-area panels (" + wrappersAdded + " new containers), " +
                          canvas.GetComponentsInChildren<Button>(true).Length + " buttons, " +
                          canvas.GetComponentsInChildren<InputField>(true).Length + " legacy name inputs, " +
                          blankTmpRemoved + " empty fontless TMP duplicates removed. " +
                          "Loading screen is the initial screen; host WebWrapper settings are preserved. " +
                          "Portrait iOS target: " + PlayerSettings.iOS.targetOSVersionString + ".", canvas);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void AssignSprite(SerializedObject target, string field, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing UI sprite: " + path);
            target.FindProperty(field).objectReferenceValue = sprite;
            target.ApplyModifiedProperties();
        }

        private static Canvas FindTemplateCanvas(Scene scene)
        {
            Canvas result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() == null || root.transform.Find("MainMenu") == null)
                    continue;
                Canvas candidate = root.GetComponent<Canvas>();
                if (candidate == null || result != null)
                    throw new InvalidOperationException("The template must have exactly one root menu Canvas.");
                result = candidate;
            }
            if (result == null)
                throw new InvalidOperationException("The expected root Canvas was not found. No scene changes were made.");
            return result;
        }

        private static List<RectTransform> ValidatePanels(Canvas canvas)
        {
            var result = new List<RectTransform>();
            foreach (string name in PanelNames)
            {
                Transform panel = canvas.transform.Find(name);
                if (!(panel is RectTransform rect) || panel.GetComponent<Image>() == null)
                    throw new InvalidOperationException("Expected authored root panel Canvas/" + name + " with a background Image.");
                Transform existingWrapper = panel.Find(SafeAreaName);
                if (existingWrapper != null && !(existingWrapper is RectTransform))
                    throw new InvalidOperationException("Canvas/" + name + "/" + SafeAreaName + " must be a RectTransform.");
                result.Add(rect);
            }
            Transform menu = canvas.transform.Find("MainMenu");
            Transform content = menu.Find(SafeAreaName) ?? menu;
            if (content.Find("TopPanel/ProfilePanel") == null)
                throw new InvalidOperationException("The authored MainMenu/TopPanel/ProfilePanel badge is missing.");
            return result;
        }

        private static bool EnsureSafeArea(RectTransform panel)
        {
            RectTransform wrapper = panel.Find(SafeAreaName) as RectTransform;
            bool created = wrapper == null;
            if (created)
            {
                var content = new GameObject(SafeAreaName, typeof(RectTransform));
                content.layer = panel.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(content, "Create safe-area content");
                wrapper = (RectTransform)content.transform;
                Undo.SetTransformParent(wrapper, panel, "Parent safe-area content");
            }

            Undo.RecordObject(wrapper, "Stretch safe-area content");
            wrapper.anchorMin = Vector2.zero;
            wrapper.anchorMax = Vector2.one;
            wrapper.pivot = new Vector2(0.5f, 0.5f);
            wrapper.anchoredPosition3D = Vector3.zero;
            wrapper.sizeDelta = Vector2.zero;
            wrapper.localRotation = Quaternion.identity;
            wrapper.localScale = Vector3.one;

            var children = new List<Transform>();
            foreach (Transform child in panel)
                if (child != wrapper)
                    children.Add(child);
            foreach (Transform child in children)
            {
                var snapshot = new LayoutSnapshot(child);
                Undo.SetTransformParent(child, wrapper, "Move authored content into safe area");
                Undo.RecordObject(child, "Preserve authored UI layout");
                snapshot.Restore(child);
                child.SetAsLastSibling();
            }

            EggCatcherSafeArea safeArea = wrapper.GetComponent<EggCatcherSafeArea>();
            if (safeArea == null)
                safeArea = Undo.AddComponent<EggCatcherSafeArea>(wrapper.gameObject);
            Undo.RecordObject(safeArea, "Enable safe-area fitting");
            safeArea.enabled = true;
            return created;
        }

        private static EventSystem FindEventSystem(Scene scene)
        {
            EventSystem found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EventSystem candidate in root.GetComponentsInChildren<EventSystem>(true))
                {
                    if (found != null)
                        throw new InvalidOperationException("The template contains multiple EventSystems. Resolve them before setup.");
                    found = candidate;
                }
            }
            return found;
        }

        private static void EnsureInputModule(Scene scene, EventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                var inputObject = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(inputObject, scene);
                Undo.RegisterCreatedObjectUndo(inputObject, "Create UI EventSystem");
                eventSystem = Undo.AddComponent<EventSystem>(inputObject);
            }
            Undo.RecordObject(eventSystem.gameObject, "Enable UI EventSystem object");
            eventSystem.gameObject.SetActive(true);
            Undo.RecordObject(eventSystem, "Enable UI EventSystem");
            eventSystem.enabled = true;

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            Undo.RecordObject(module, "Configure UI input actions");
            module.enabled = true;
            if (module.actionsAsset == null || !HasAction(module.point) || !HasAction(module.leftClick) ||
                !HasAction(module.move) || !HasAction(module.submit) || !HasAction(module.cancel))
                AssignPersistentDefaultActions(module);

            foreach (StandaloneInputModule legacy in eventSystem.GetComponents<StandaloneInputModule>())
                if (legacy.GetType() == typeof(StandaloneInputModule))
                    Undo.DestroyObjectImmediate(legacy);
        }

        private static bool HasAction(InputActionReference reference)
        {
            return reference != null && reference.action != null;
        }

        private static void AssignPersistentDefaultActions(InputSystemUIInputModule module)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultActionsPath);
            if (asset == null)
                throw new InvalidOperationException("The Input System default UI actions asset could not be loaded.");
            var references = new Dictionary<string, InputActionReference>(StringComparer.Ordinal);
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(DefaultActionsPath))
            {
                var reference = subAsset as InputActionReference;
                if (reference != null && reference.action != null && reference.action.actionMap.name == "UI")
                    references[reference.action.name] = reference;
            }
            string[] required = { "Point", "Click", "Navigate", "Submit", "Cancel", "ScrollWheel", "RightClick", "MiddleClick", "TrackedDevicePosition", "TrackedDeviceOrientation" };
            foreach (string name in required)
                if (!references.ContainsKey(name))
                    throw new InvalidOperationException("The default UI action reference is missing: " + name);

            module.actionsAsset = asset;
            module.point = references["Point"];
            module.leftClick = references["Click"];
            module.move = references["Navigate"];
            module.submit = references["Submit"];
            module.cancel = references["Cancel"];
            module.scrollWheel = references["ScrollWheel"];
            module.rightClick = references["RightClick"];
            module.middleClick = references["MiddleClick"];
            module.trackedDevicePosition = references["TrackedDevicePosition"];
            module.trackedDeviceOrientation = references["TrackedDeviceOrientation"];
        }

        private static int RemoveBlankFontlessTmpDuplicates(Canvas canvas)
        {
            int removed = 0;
            foreach (Component component in canvas.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "TMPro.TextMeshProUGUI" ||
                    component.GetComponent<Text>() == null)
                    continue;
                var serialized = new SerializedObject(component);
                SerializedProperty font = serialized.FindProperty("m_fontAsset");
                SerializedProperty text = serialized.FindProperty("m_text");
                if (font != null && text != null && font.objectReferenceValue == null && string.IsNullOrEmpty(text.stringValue))
                {
                    Undo.DestroyObjectImmediate(component);
                    removed++;
                }
            }
            return removed;
        }

        private static void ConfigurePortraitPlayer()
        {
            foreach (PlayerSettings settings in Resources.FindObjectsOfTypeAll<PlayerSettings>())
                Undo.RecordObject(settings, "Configure portrait iOS UI test");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            string current = PlayerSettings.iOS.targetOSVersionString;
            string normalized = current != null && current.IndexOf('.') < 0 ? current + ".0" : current;
            if (Version.TryParse(normalized, out Version target))
            {
                if (target < new Version(18, 0))
                    PlayerSettings.iOS.targetOSVersionString = "18.0";
            }
            else
            {
                Debug.LogWarning("[Egg Catcher] Unrecognized iOS minimum version; left unchanged: " + current);
            }
        }

        private readonly struct LayoutSnapshot
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector2 sizeDelta;

            public LayoutSnapshot(Transform transform)
            {
                rotation = transform.localRotation;
                scale = transform.localScale;
                if (transform is RectTransform rect)
                {
                    position = rect.anchoredPosition3D;
                    anchorMin = rect.anchorMin;
                    anchorMax = rect.anchorMax;
                    pivot = rect.pivot;
                    sizeDelta = rect.sizeDelta;
                }
                else
                {
                    position = transform.localPosition;
                    anchorMin = anchorMax = pivot = sizeDelta = Vector2.zero;
                }
            }

            public void Restore(Transform transform)
            {
                if (transform is RectTransform rect)
                {
                    rect.anchorMin = anchorMin;
                    rect.anchorMax = anchorMax;
                    rect.pivot = pivot;
                    rect.sizeDelta = sizeDelta;
                    rect.anchoredPosition3D = position;
                }
                else
                {
                    transform.localPosition = position;
                }
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }
    }
}
