using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggCatcher.Editor
{
    public static class EggCatcherGameplaySetup
    {
        public static void Connect(Scene scene, Canvas menu, EggCatcherApp app)
        {
            GameObject root = null;
            Camera camera = null;
            foreach (var candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == "Game") root = candidate;
                if (candidate.CompareTag("MainCamera")) camera = candidate.GetComponent<Camera>();
            }
            if (root == null) return;
            if (camera == null) throw new InvalidOperationException("Gameplay requires the authored Main Camera.");
            var gameCanvas = root.transform.Find("GameCanvas").GetComponent<Canvas>();
            var player = root.transform.Find("Player").GetComponent<SpriteRenderer>();
            var backdrop = root.transform.Find("BG").GetComponent<SpriteRenderer>();

            foreach (var duplicate in gameCanvas.GetComponents<EggCatcherApp>()) Undo.DestroyObjectImmediate(duplicate);
            var game = root.GetComponent<EggCatcherGame>() ?? Undo.AddComponent<EggCatcherGame>(root);
            Undo.RecordObject(game, "Connect gameplay assets");
            var serialized = new SerializedObject(game);
            Set(serialized, "worldCamera", camera);
            Set(serialized, "gameCanvas", gameCanvas);
            Set(serialized, "player", player);
            Set(serialized, "backdrop", backdrop);
            string[] itemPaths = { "Items/Pickup_Egg", "Items/Pickup_Corn", "Items/Pickup_Worm", "Pickup_Heart", "Items/Pickup_Magnet", "Items/Hazard_Rock", "Items/Hazard_RottenApple", "Items/Hazard_Bomb" };
            var items = serialized.FindProperty("itemSprites");
            items.arraySize = itemPaths.Length;
            for (int i = 0; i < itemPaths.Length; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = Sprite("Assets/0_Sprites/" + itemPaths[i] + ".png");
            string[] ids = { "classic", "chick", "pirate", "ninja", "space", "gold" };
            var skins = serialized.FindProperty("skins");
            skins.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                var skin = skins.GetArrayElementAtIndex(i);
                skin.FindPropertyRelative("Id").stringValue = ids[i];
                string name = char.ToUpperInvariant(ids[i][0]) + ids[i].Substring(1);
                string path = "Assets/0_Sprites/Players/" + name + "/" + name + "_";
                skin.FindPropertyRelative("Stand").objectReferenceValue = Sprite(path + "Idle.png");
                skin.FindPropertyRelative("Left").objectReferenceValue = Sprite(path + "RunLeft.png");
                skin.FindPropertyRelative("Right").objectReferenceValue = Sprite(path + "RunRight.png");
                skin.FindPropertyRelative("Hurt").objectReferenceValue = Sprite(path + "Hurt.png");
            }
            Set(serialized, "truckSprite", Sprite("Assets/0_Sprites/Items/Truck_EggCarrier.png"));
            Set(serialized, "crowSprite", Sprite("Assets/0_Sprites/Items/Crow_HazardCarrier.png"));
            Set(serialized, "balloonSprite", Sprite("Assets/0_Sprites/Items/Balloon_PowerupCarrier.png"));
            Set(serialized, "cloudSprite", Sprite("Assets/0_Sprites/Items/Cloud_Drifting.png"));
            Set(serialized, "fullHeart", Sprite("Assets/0_Sprites/Pickup_Heart.png"));
            Set(serialized, "emptyHeart", Sprite("Assets/0_Sprites/HUD_HeartEmpty.png"));
            Set(serialized, "activeMagnet", Sprite("Assets/0_Sprites/HUD_MagnetActive.png"));
            Set(serialized, "inactiveMagnet", Sprite("Assets/0_Sprites/HUD_MagnetInactive.png"));
            Set(serialized, "newBestRibbon", Sprite("Assets/0_UI/Game/Ribbon_NewRecord.png"));
            Set(serialized, "toggleOn", Sprite("Assets/0_UI/Skins/Toggle_On.png"));
            Set(serialized, "toggleOff", Sprite("Assets/0_UI/Skins/Toggle_Off.png"));
            serialized.ApplyModifiedProperties();
            var appSerialized = new SerializedObject(app);
            appSerialized.FindProperty("gameplay").objectReferenceValue = game;
            appSerialized.ApplyModifiedProperties();

            if (gameCanvas.transform.Find("HudSafeArea") == null)
            {
                var safe = new GameObject("HudSafeArea", typeof(RectTransform), typeof(EggCatcherSafeArea));
                Undo.RegisterCreatedObjectUndo(safe, "Create game HUD safe area");
                safe.transform.SetParent(gameCanvas.transform, false);
                safe.transform.SetAsFirstSibling();
                var rect = (RectTransform)safe.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var top = gameCanvas.transform.Find("TopPanel");
                var topRect = (RectTransform)top;
                Vector2 position = topRect.anchoredPosition, size = topRect.sizeDelta;
                Undo.SetTransformParent(top, safe.transform, "Fit game HUD to safe area");
                Undo.RecordObject(top, "Preserve HUD layout");
                topRect.anchorMin = topRect.anchorMax = Vector2.one * .5f;
                topRect.localScale = Vector3.one;
                topRect.anchoredPosition = position; topRect.sizeDelta = size;
            }
            EggCatcherHeartLayout.Configure(gameCanvas.transform.Find("HudSafeArea/TopPanel/Panel/Health"));
            Undo.RecordObject(menu.gameObject, "Start in the menu");
            menu.gameObject.SetActive(true);
            Undo.RecordObject(root, "Start game only from Play");
            root.SetActive(false);
        }

        private static void Set(SerializedObject target, string field, UnityEngine.Object value) => target.FindProperty(field).objectReferenceValue = value;
        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing gameplay sprite: " + path);

        [MenuItem("Tools/Egg Catcher/Preview Game (Separate Test Profile)")]
        public static void PreviewGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != EggCatcherUiSetup.ScenePath)
                throw new InvalidOperationException("Open the MainMenu scene first.");
            var data = GameDataStore.Load("GameplayPreview");
            if (!data.HasProfile) { data.Name = "Speckles"; data.HasProfile = true; GameDataStore.Save(data, "GameplayPreview"); }
            SessionState.SetString("EggCatcher.PreviewSlot", "GameplayPreview");
            SessionState.SetBool("EggCatcher.PreviewGame", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
