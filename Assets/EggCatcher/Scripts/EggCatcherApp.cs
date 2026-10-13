using System;
using System.Collections;
using System.Collections.Generic;
using AlienProject.WebWrapper;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EggCatcher
{

    [DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed partial class EggCatcherApp : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Sprite actionButtonSprite;
        [SerializeField] private EggCatcherGame gameplay;
        public float LocalPreparationProgress { get; private set; }
        private EggCatcherAudio gameAudio;
        public bool MusicEnabled => data != null && data.MusicEnabled;
        public bool SoundsEnabled => data != null && data.SoundEnabled;
        public bool HapticsEnabled => data != null && data.HapticsEnabled;
        [SerializeField] private UnityEvent onPlayRequested = new UnityEvent();
        public event Action PlayRequested;
        public event Action SettingsChanged;
        public EggCatcherData Profile => GameDataStore.Deserialize(GameDataStore.Serialize(data));

        private readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();
        private static readonly string[] ScreenNames = { "LoadingPanel", "CreateProfilePanel", "MainMenu", "ProfilePanel", "StatsPanel", "EditProfilePanel", "SkinsPanel", "SettingPanel", "How to playPanel" };
        private EggCatcherData data;
        private string profileSlot;
        private InputField createName, editName;
        private Text createHint, editHint;
        private readonly Sprite[] avatars = new Sprite[EggCatcherData.AvatarCount];
        private int selectedAvatar = -1;
        private int editingAvatar;
        private string editReturnScreen = "ProfilePanel";
        private GameObject modal;
        private Font font;
        private Vector2 keyboardBase;
        private RectTransform keyboardPanel;
        private static readonly Color Ink = new Color32(28, 77, 132, 255);
        private static readonly Color Blue = new Color32(37, 139, 255, 255);
        private static readonly Color Error = new Color32(255, 63, 67, 255);

        private void Awake()
        {

            AndroidTestConsole.EnsureInstalled();
            EggCatcherViewport.Ensure(GetComponent<Canvas>());
            ApplyMobileDisplaySettings();
            Screen.orientation = ScreenOrientation.Portrait;
            if (gameplay != null) gameplay.gameObject.SetActive(false);

            if (GetComponent<EggCatcherPushPanel>() == null) gameObject.AddComponent<EggCatcherPushPanel>();
            if (GetComponent<EggCatcherLoadingPanel>() == null) gameObject.AddComponent<EggCatcherLoadingPanel>();
        }

        private static void ApplyMobileDisplaySettings()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR

            Application.targetFrameRate = 60;
            UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = 1;
#if UNITY_ANDROID
            Screen.fullScreen = true;
#endif
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) ApplyMobileDisplaySettings();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) ApplyMobileDisplaySettings();
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            profileSlot = UnityEditor.SessionState.GetString("EggCatcher.PreviewSlot", string.Empty);
            if (WebWrapperEditorLiveSession.IsEnabled) profileSlot = "ServerTest_" + WebWrapperEditorLiveSession.InstallationId;
#endif
            if (canvas == null) canvas = GetComponent<Canvas>();
            foreach (var screen in ScreenNames) screens.Add(screen, canvas.transform.Find(screen).gameObject);

            foreach (Transform child in canvas.transform)
                if (child.name != "PushPanel" && !screens.ContainsKey(child.name) && child.GetComponent<Image>() != null)
                    screens.Add(child.name, child.gameObject);

            foreach (var screen in screens) screen.Value.SetActive(screen.Key == "LoadingPanel");
            Get<Text>("LoadingPanel", "LoaingTXT").text = "Loading...";
            LocalPreparationProgress = .05f;
            yield return null;
            font = Get<Text>("LoadingPanel", "LoaingTXT").font;
            BindUi();
            LocalPreparationProgress = .3f;
            yield return null;
            LoadProfile();
            LocalPreparationProgress = .5f;
            yield return null;
            gameAudio = GetComponent<EggCatcherAudio>() ?? gameObject.AddComponent<EggCatcherAudio>();
            gameAudio.Initialize();
            LocalPreparationProgress = .75f;
            yield return null;
            SettingsChanged += ApplyAudioSettings;
            if (gameplay != null) gameplay.Initialize(this, gameAudio);
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => gameAudio.PlayUi());
            Canvas.ForceUpdateCanvases();
            LocalPreparationProgress = 1f;
            while (!WebWrapperLaunchState.CanStartGame(WebWrapperBootstrap.IsAutoStartEnabled))
                yield return null;
            FindFirstObjectByType<WebWrapperController>()?.ContinueInGameForSession();
            ApplyAudioSettings();
            ShowScreen(data.HasProfile ? "MainMenu" : "CreateProfilePanel");
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("EggCatcher.PreviewPush", false))
            {
                UnityEditor.SessionState.EraseBool("EggCatcher.PreviewPush");
                GetComponent<EggCatcherPushPanel>().PreviewInEditor();
            }
            if (data.HasProfile && UnityEditor.SessionState.GetBool("EggCatcher.PreviewGame", false))
            {
                UnityEditor.SessionState.EraseBool("EggCatcher.PreviewGame");
                RequestPlay();
            }
#endif
        }

        private void LoadProfile()
        {
            data = GameDataStore.Load(profileSlot);
            selectedAvatar = -1;
            createName.SetTextWithoutNotify(string.Empty);
            LoadSavedPhoto();
            RefreshProfile();
            RefreshName(false);
            if (gameAudio != null) ApplyAudioSettings();
        }

        private void BindUi()
        {
            createName = Get<InputField>("CreateProfilePanel", "Panel/InputField (Legacy)");
            editName = Get<InputField>("EditProfilePanel", "Panel/InputField (Legacy)");
            createHint = Get<Text>("CreateProfilePanel", "txt");

            createHint.rectTransform.anchoredPosition = new Vector2(0, -555);
            createHint.rectTransform.sizeDelta = new Vector2(770, 64);
            createHint.rectTransform.SetAsLastSibling();
            editHint = MakeLabel("NameValidation", editName.transform, "", new Vector2(0, -84), new Vector2(770, 48), 30);
            ConfigureName(createName, false);
            ConfigureName(editName, true);
            ConfigureActionButton(Get<Button>("CreateProfilePanel", "StartBtn"));
            for (var i = 0; i < avatars.Length; i++)
            {
                var index = i;
                var path = "Panel/Avatars/" + (i + 1);
                avatars[i] = Get<Image>("CreateProfilePanel", path).sprite;
                Bind("CreateProfilePanel", path, () => SelectAvatar(index, false));
                Bind("EditProfilePanel", path, () => SelectAvatar(index, true));
            }
            Bind("CreateProfilePanel", "StartBtn", CreateProfile);
            Bind("MainMenu", "TopPanel/ProfilePanel", () => ShowScreen("ProfilePanel"));
            Bind("MainMenu", "Play", RequestPlay);
            Bind("MainMenu", "Skins", () => OpenShop("MainMenu"));
            Bind("MainMenu", "Stats", () => ShowScreen("StatsPanel"));
            Bind("MainMenu", "Settings", ShowSettings);
            Bind("ProfilePanel", "TopPanel/Back", () => ShowScreen("MainMenu"));
            Bind("ProfilePanel", "EditProfile", () => BeginEdit("ProfilePanel"));
            Bind("ProfilePanel", "CurrentLook", ShowCurrentLook);
            Bind("ProfilePanel", "CurrentLook/Open", ShowCurrentLook);
            Bind("ProfilePanel", "ResetProgress", () => Node("ProfilePanel", "ResetPanel").gameObject.SetActive(true));
            Bind("ProfilePanel", "ResetPanel/Panel/Cancel", () => Node("ProfilePanel", "ResetPanel").gameObject.SetActive(false));
            Bind("ProfilePanel", "ResetPanel/Panel/Reset", ResetProfile);
            Bind("StatsPanel", "TopPanel/Back", () => ShowScreen("MainMenu"));
            Bind("StatsPanel", "EmptyResult/Play", RequestPlay);
            Bind("EditProfilePanel", "TopPanel/Back", CancelEdit);
            Bind("EditProfilePanel", "Cancel", CancelEdit);
            Bind("EditProfilePanel", "Save", SaveEdit);
            BindShop();
            BindSettingsAndHelp();
            BindPrivacyPolicy();
            BindPhotos();
        }

        private void ConfigureName(InputField field, bool editing)
        {
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            field.keyboardType = TouchScreenKeyboardType.Default;
            field.characterLimit = 0;
            field.shouldHideMobileInput = true;
            field.onValueChanged.AddListener(_ => RefreshName(editing));
            field.onEndEdit.AddListener(_ => RefreshName(editing));
        }

        private void RefreshName(bool editing)
        {
            var input = editing ? editName : createName;
            bool valid = GameDataStore.TryValidateName(input.text, out _, out var error);
            bool empty = string.IsNullOrWhiteSpace(input.text);
            var hint = editing ? editHint : createHint;
            hint.text = !valid && !empty ? error : editing ? "2–12 characters" :
                empty ? "Enter a name to continue" : selectedAvatar < 0 ? "Choose an avatar to continue" : "Ready to start!";
            hint.color = !valid && !empty ? Error : editing ? Ink : Color.white;
            var outline = input.GetComponent<Outline>() ?? input.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3, -3);
            outline.effectColor = !valid && !empty ? Error : input.isFocused ? Blue : new Color32(177, 205, 234, 255);
            var submit = Get<Button>(editing ? "EditProfilePanel" : "CreateProfilePanel", editing ? "Save" : "StartBtn");
            submit.interactable = valid && (editing || selectedAvatar >= 0);
            var label = submit.GetComponentInChildren<Text>();
            if (label != null) label.color = submit.interactable ? Color.white : new Color32(143, 157, 175, 255);
            RefreshAvatarSelection(editing);
        }

        private bool AvatarUnlocked(int index) => index < 6 || data.Level >= (index == 6 ? 10 : 15);

        private void SelectAvatar(int index, bool editing)
        {
            if (!AvatarUnlocked(index)) return;
            if (editing)
            {
                editingAvatar = index;
                ClearDraftPhoto();
                draftPhotoRemoved = true;
                Get<Image>("EditProfilePanel", "Icon").sprite = avatars[index];
            }
            else selectedAvatar = index;
            RefreshName(editing);
            gameAudio?.PlayUi(EggSound.SkinSelect);
        }

        private void ConfigureActionButton(Button button)
        {
            if (actionButtonSprite == null) return;
            var state = button.spriteState;
            state.disabledSprite = button.GetComponent<Image>().sprite;
            state.highlightedSprite = state.pressedSprite = state.selectedSprite = actionButtonSprite;
            button.GetComponent<Image>().sprite = actionButtonSprite;
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
        }

        private void RefreshAvatarSelection(bool editing)
        {
            var screen = editing ? "EditProfilePanel" : "CreateProfilePanel";
            for (int i = 0; i < avatars.Length; i++)
            {
                var button = Get<Button>(screen, "Panel/Avatars/" + (i + 1));
                button.interactable = AvatarUnlocked(i);
                var locked = button.transform.Find("Lock");
                if (locked != null) locked.gameObject.SetActive(!AvatarUnlocked(i));
                var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(48, 184, 55, 255);
                outline.effectDistance = new Vector2(6, -6);
                outline.enabled = i == (editing ? editingAvatar : selectedAvatar) && (!editing || DraftPhoto == null);
            }
        }

        private void CreateProfile()
        {
            if (!GameDataStore.TryValidateName(createName.text, out var name, out _) || selectedAvatar < 0 || !AvatarUnlocked(selectedAvatar)) return;
            data.Name = name;
            data.AvatarIndex = selectedAvatar;
            data.HasProfile = true;
            GameDataStore.Save(data, profileSlot);
            RefreshProfile();
            ShowScreen("MainMenu");
            gameAudio?.PlayUi(EggSound.Confirm);
        }

        private void BeginEdit(string returnScreen)
        {
            editReturnScreen = returnScreen;
            editingAvatar = data.AvatarIndex;
            ClearDraftPhoto();
            draftPhotoRemoved = false;
            editName.SetTextWithoutNotify(data.Name);
            Get<Image>("EditProfilePanel", "Icon").sprite = SavedAvatar;
            ShowScreen("EditProfilePanel");
            RefreshName(true);
        }

        private void CancelEdit()
        {
            ClearDraftPhoto();
            ShowScreen(editReturnScreen);
            gameAudio?.PlayUi(EggSound.Back);
        }

        private void SaveEdit()
        {
            if (!GameDataStore.TryValidateName(editName.text, out var name, out _)) return;
            if (!CommitPhoto()) return;
            data.Name = name;
            data.AvatarIndex = editingAvatar;
            GameDataStore.Save(data, profileSlot);
            ClearDraftPhoto();
            LoadSavedPhoto();
            RefreshProfile();
            ShowScreen(editReturnScreen);
            gameAudio?.PlayUi(EggSound.Confirm);
        }

        private void ResetProfile()
        {
            ForgetSavedPhoto();
            data = GameDataStore.Reset(profileSlot);
            LoadProfile();
            ShowScreen("CreateProfilePanel");
            gameAudio?.PlayUi(EggSound.Confirm);
        }

        private void RefreshProfile()
        {
            SetText("MainMenu", "TopPanel/ProfilePanel/NickName", data.Name);
            SetText("MainMenu", "TopPanel/ProfilePanel/LevelTXT", "Level " + data.Level);
            SetText("MainMenu", "TopPanel/MoneyPanel/NickName", GameDataStore.FormatNumber(data.Coins));
            SetText("MainMenu", "BestPanel/Best", "Best: " + GameDataStore.FormatNumber(data.BestScore));
            Get<Image>("MainMenu", "TopPanel/ProfilePanel/Icon").sprite = SavedAvatar;
            Get<Image>("ProfilePanel", "Panel/Icon").sprite = SavedAvatar;
            SetText("ProfilePanel", "Panel/NickName", data.Name);
            SetText("ProfilePanel", "Panel/Joined/JoinedTXT", "Joined " + GameDataStore.FormatJoinedDate(data.JoinedUtcIso));
            SetText("ProfilePanel", "Panel/Nimble Hen/Tittle", data.Level < 10 ? "Little Chick" : "Nimble Hen");
            SetText("ProfilePanel", "Panel/LevelProgressPanel/LevelPanel/NickName", data.Level.ToString());
            SetText("ProfilePanel", "BestScore/Score", GameDataStore.FormatNumber(data.BestScore));
            SetText("ProfilePanel", "Games played/Score", GameDataStore.FormatNumber(data.GamesPlayed));
            SetText("ProfilePanel", "Total eggs/Score", GameDataStore.FormatNumber(data.TotalEggsCaught));
            RefreshCurrentLook();
            RefreshSettings();
            SetText("StatsPanel", "Result/BestScore/Score", GameDataStore.FormatNumber(data.BestScore));
            SetText("StatsPanel", "Result/Games played/Value", GameDataStore.FormatNumber(data.GamesPlayed));
            SetText("StatsPanel", "Result/Total score/Value", GameDataStore.FormatNumber(data.TotalScore));
            SetText("StatsPanel", "Result/Average score/Value", GameDataStore.FormatNumber(data.GamesPlayed == 0 ? 0 : data.TotalScore / data.GamesPlayed));
            SetText("StatsPanel", "Result/Time played/Value", FormatDuration(data.TotalPlaySeconds));
            SetText("StatsPanel", "Result/Eggs caught/Value", GameDataStore.FormatNumber(data.TotalEggsCaught));
            SetText("StatsPanel", "Result/Best streak/Value", GameDataStore.FormatNumber(data.BestCombo));
            SetText("StatsPanel", "Result/Eggs spent/Value", GameDataStore.FormatNumber(data.TotalEggsSpent));
            Node("StatsPanel", "Result").gameObject.SetActive(data.GamesPlayed > 0);
            Node("StatsPanel", "EmptyResult").gameObject.SetActive(data.GamesPlayed == 0);
        }

        private static string FormatDuration(double seconds)
        {
            var minutes = Math.Floor(seconds / 60);
            return minutes >= 60 ? Math.Floor(minutes / 60) + " h " + minutes % 60 + " min" : minutes + " min";
        }

        private void ShowScreen(string name)
        {
            RestoreKeyboardPanel();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (createName != null) createName.DeactivateInputField();
            if (editName != null) editName.DeactivateInputField();
            CloseModal();
            CloseShopDialogs();
            Node("ProfilePanel", "ResetPanel").gameObject.SetActive(false);
            Node("EditProfilePanel", "ProfilePhotoPanel").gameObject.SetActive(false);
            Node("EditProfilePanel", "TakeAPhotoPanel").gameObject.SetActive(false);
            foreach (var pair in screens) pair.Value.SetActive(pair.Key == name);
            if (name == "MainMenu" && !WebWrapperLaunchState.IsGameReady)
                StartCoroutine(NotifyGameMenuReady());
        }

        private IEnumerator NotifyGameMenuReady()
        {

            yield return new WaitForSecondsRealtime(.75f);
            if (screens["MainMenu"].activeInHierarchy) WebWrapperLaunchState.NotifyGameReady();
        }

        private void RequestPlay()
        {
            if (gameplay != null)
            {
                CloseModal();
                canvas.enabled = false;
                canvas.GetComponent<GraphicRaycaster>().enabled = false;
                gameplay.StartRound();
            }
            if (PlayRequested != null || onPlayRequested.GetPersistentEventCount() > 0)
            {
                PlayRequested?.Invoke();
                onPlayRequested.Invoke();
            }
            else if (gameplay == null) ShowNotice("Gameplay is not connected", "Run Tools → Egg Catcher → Connect UI Template.", "Back");
        }

        public void ReturnFromGame()
        {
            canvas.enabled = true;
            canvas.GetComponent<GraphicRaycaster>().enabled = true;
            RefreshProfile();
            ShowScreen("MainMenu");
        }

        public bool SaveRound(EggCatcherRound round)
        {
            if (round == null || !round.Commit(data)) return false;
            GameDataStore.Save(data, profileSlot);
            RefreshProfile();
            return true;
        }

        public void ToggleGameSetting(int setting)
        {
            if (setting == 0) data.MusicEnabled = !data.MusicEnabled;
            else if (setting == 1) data.SoundEnabled = !data.SoundEnabled;
            else if (setting == 2) data.HapticsEnabled = !data.HapticsEnabled;
            SaveSettings();
        }

        private void ApplyAudioSettings() => gameAudio.ApplySettings(data.MusicEnabled, data.SoundEnabled);

        private void ShowCurrentLook() => OpenShop("ProfilePanel");

        private void LateUpdate()
        {
            if (createName == null) return;

            var focused = createName.isFocused ? createName : editName.isFocused ? editName : null;
            if (focused != null) RefreshName(focused == editName);
            if (focused == null || !TouchScreenKeyboard.visible || TouchScreenKeyboard.area.height <= 0)
            {
                RestoreKeyboardPanel();
                return;
            }
            var panel = focused.transform.parent as RectTransform;
            if (panel != keyboardPanel)
            {
                RestoreKeyboardPanel();
                keyboardPanel = panel;
                keyboardBase = panel.anchoredPosition;
            }
            panel.anchoredPosition = keyboardBase;
            var corners = new Vector3[4];
            focused.GetComponent<RectTransform>().GetWorldCorners(corners);
            float bottom = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[0]).y;
            float overlap = TouchScreenKeyboard.area.yMax + 35 - bottom;
            if (overlap > 0)
            {

                var parent = (RectTransform)panel.parent;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(0, bottom), camera, out var from);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(0, bottom + overlap), camera, out var to);
                panel.anchoredPosition = keyboardBase + to - from;
            }
        }

        private void RestoreKeyboardPanel()
        {
            if (keyboardPanel != null) keyboardPanel.anchoredPosition = keyboardBase;
            keyboardPanel = null;
        }

        private Transform Node(string screen, string path)
        {
            var root = screens[screen].transform;
            var found = (root.Find("SafeAreaContent") ?? root).Find(path);
            if (found == null) throw new InvalidOperationException("Egg Catcher UI element is missing: " + screen + "/" + path);
            return found;
        }
        private T Get<T>(string screen, string path) where T : Component => Node(screen, path).GetComponent<T>() ??
            throw new InvalidOperationException("Egg Catcher UI component is missing: " + typeof(T).Name + " at " + screen + "/" + path);
        private void Bind(string screen, string path, UnityAction action) => Get<Button>(screen, path).onClick.AddListener(action);
        private void SetText(string screen, string path, string value) => Get<Text>(screen, path).text = value;
        private void OnDestroy() { ClearDraftPhoto(); ReleaseSavedPhoto(); ReleaseCrop(); ReleaseShopThumbnails(); }
    }
}
