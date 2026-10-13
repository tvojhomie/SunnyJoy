using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EggCatcher.Editor
{
    public static class EggCatcherAssetMaintenance
    {
        private static readonly Dictionary<string, string> UiNames = new Dictionary<string, string>
        {
            { "Background", "Menu_MeadowBackground" }, { "icon", "AppIcon_GoldenEgg" }, { "push-1", "PushPermission_Illustration" },
            { "Game/Button-1", "Game/Button_Restart" }, { "Game/Button-2", "Game/Button_MainMenu" },
            { "Game/Button-3", "Game/Button_PlayAgain" }, { "Game/Button", "Game/Button_Resume" },
            { "Game/Chicken", "Game/Character_GameOver" }, { "Game/Group 11", "Game/Setting_MusicLabel" },
            { "Game/Group 12", "Game/Setting_SoundsLabel" }, { "Game/Group 13", "Game/Setting_VibrationLabel" },
            { "Game/Modal-1", "Game/Dialog_QuitRound" }, { "Game/Modal", "Game/Dialog_RestartRound" },
            { "Game/Panel-1", "Game/Panel_RoundResults" }, { "Game/Panel", "Game/Panel_Pause" },
            { "Game/RecordPlaque", "Game/Ribbon_NewRecord" }, { "Game/Title ribbon 1", "Game/Ribbon_Paused" },
            { "Game/Title ribbon", "Game/Ribbon_GameOver" },
            { "Menu/AvatarCard", "Menu/Avatar_Classic" }, { "Menu/AvatarCard-1", "Menu/Avatar_Chick" },
            { "Menu/AvatarCard-2", "Menu/Avatar_Pirate" }, { "Menu/AvatarCard-3", "Menu/Avatar_Ninja" },
            { "Menu/AvatarCard-4", "Menu/Avatar_Astronaut" }, { "Menu/AvatarCard-5", "Menu/Avatar_Golden" },
            { "Menu/AvatarCard-6", "Menu/Avatar_Crow" }, { "Menu/AvatarCard-7", "Menu/Avatar_Worm" },
            { "Menu/Button-1", "Menu/Button_PrimaryGreen" }, { "Menu/Button-2", "Menu/Button_Disabled" },
            { "Menu/Button", "Menu/Button_Play" }, { "Menu/Field", "Menu/Input_PlayerName" },
            { "Menu/GrainCounter 1", "Menu/Pill_ProfileBackground" }, { "Menu/GrainCounter", "Menu/Pill_EggBalance" },
            { "Menu/Group 4", "Menu/Title_CreateProfile" }, { "Menu/Group 6", "Menu/Background_MeadowDimmed" },
            { "Menu/IconButton", "Menu/Button_Back" }, { "Menu/Loader", "Menu/Loading_Track" },
            { "Menu/Logo", "Menu/Logo_EggCatcher" }, { "Menu/Panel", "Menu/Panel_CreateProfile" },
            { "Menu/Record", "Menu/Pill_BestScore" }, { "Menu/Rectangle", "Menu/Loading_Fill" },
            { "Menu/Skin=classic, State=stand", "Menu/Character_ClassicHero" },
            { "Menu/Tile Settings", "Menu/Button_Settings" }, { "Menu/Tile Skins", "Menu/Button_Skins" },
            { "Menu/Tile Stats", "Menu/Button_Stats" },
            { "Profile/Bar", "Profile/XP_Track" }, { "Profile/Bottom sheet 1", "Profile/Sheet_PhotoSource" },
            { "Profile/Button 1", "Profile/Button_WideBlue" }, { "Profile/Button-1 1", "Profile/Button_ResetRed" },
            { "Profile/Button-1", "Profile/Button_SaveGreen" }, { "Profile/Button", "Profile/Button_CancelBlue" },
            { "Profile/Confirm", "Profile/Button_ConfirmRed" }, { "Profile/Current look", "Profile/Panel_CurrentLook" },
            { "Profile/Frame 1", "Profile/Icon_EditPhoto" }, { "Profile/Frame 2", "Profile/Icon_Selected" },
            { "Profile/Frame", "Profile/Pill_JoinedDate" }, { "Profile/Group 10", "Profile/Stats_EmptyState" },
            { "Profile/Group 7", "Profile/Avatar_LockedOverlay" },
            { "Profile/Icon circle", "Profile/StatIcon_BestScore" }, { "Profile/Icon circle-1", "Profile/StatIcon_GamesPlayed" },
            { "Profile/Icon circle-2", "Profile/StatIcon_TotalScore" }, { "Profile/Icon circle-3", "Profile/StatIcon_AverageScore" },
            { "Profile/Icon circle-4", "Profile/StatIcon_PlayTime" }, { "Profile/Icon circle-5", "Profile/StatIcon_EggsCaught" },
            { "Profile/Icon circle-6", "Profile/StatIcon_BestStreak" }, { "Profile/Icon circle-7", "Profile/StatIcon_EggsSpent" },
            { "Profile/IconBg", "Profile/SummaryIcon_Eggs" }, { "Profile/IconBg-1", "Profile/SummaryIcon_Games" },
            { "Profile/IconBg-2", "Profile/SummaryIcon_BestScore" }, { "Profile/Level title", "Profile/Pill_LevelTitle" },
            { "Profile/Level", "Profile/Badge_LevelNumber" }, { "Profile/Modal 1", "Profile/Dialog_ResetProgress" },
            { "Profile/Option Choose from gallery", "Profile/PhotoOption_Gallery" },
            { "Profile/Option Remove photo", "Profile/PhotoOption_Remove" }, { "Profile/Option Take a photo", "Profile/PhotoOption_Camera" },
            { "Profile/Panel", "Profile/Panel_PlayerSummary" }, { "Profile/Photo/Fill", "Profile/Photo/Zoom_Fill" },
            { "Profile/Photo/Frame", "Profile/Photo/Hint_DragAndZoom" }, { "Profile/Photo/Group 9", "Profile/Photo/Crop_CircleMask" },
            { "Profile/Photo/Knob", "Profile/Photo/Zoom_Handle" }, { "Profile/Photo/Track", "Profile/Photo/Zoom_Track" },
            { "Profile/Photo/bg", "Profile/Photo/Background_Navy" }, { "Profile/Photo/zoom_in", "Profile/Photo/Icon_ZoomIn" },
            { "Profile/Photo/zoom_out", "Profile/Photo/Icon_ZoomOut" }, { "Profile/Preview Skin", "Profile/Frame_SkinPreview" },
            { "Profile/Progress", "Profile/XP_Fill" }, { "Profile/Scrim", "Profile/Overlay_Dimmed" },
            { "Profile/Stat Best score", "Profile/Card_Statistic" }, { "Profile/calendar_month", "Profile/Icon_Calendar" },
            { "Profile/chevron_right", "Profile/Icon_ChevronRight" }, { "Profile/menu", "Profile/Background_Meadow" },
            { "Skins/Action-1", "Skins/Button_BuyWithEggs" }, { "Skins/Action-2", "Skins/Button_Selected" },
            { "Skins/Action-3", "Skins/Button_Select" }, { "Skins/Action", "Skins/Button_Locked" },
            { "Skins/Frame 17", "Skins/HowToPlay_Rules" }, { "Skins/Modal-1", "Skins/Dialog_NotEnoughEggs" },
            { "Skins/Modal", "Skins/Dialog_ConfirmPurchase" }, { "Skins/Preview", "Skins/Preview_Meadow" },
            { "Skins/Row How to play", "Skins/Setting_HowToPlay" }, { "Skins/Row Music", "Skins/Setting_Music" },
            { "Skins/Row Sounds", "Skins/Setting_Sounds" }, { "Skins/Row Vibration", "Skins/Setting_Vibration" },
            { "Skins/SkinCard-1", "Skins/Card_Default" }, { "Skins/SkinCard", "Skins/Card_Selected" },
            { "Skins/Tabs-1", "Skins/Tabs_BackgroundsSelected" }, { "Skins/Tabs", "Skins/Tabs_CharactersSelected" },
            { "Skins/Toggle-1", "Skins/Toggle_Off" }, { "Skins/Toggle", "Skins/Toggle_On" }, { "Skins/lock", "Skins/Icon_Lock" }
        };

        private static readonly Dictionary<string, string> SpriteNames = new Dictionary<string, string>
        {
            { "Bonus active", "HUD_MagnetActive" }, { "Bonus no active", "HUD_MagnetInactive" },
            { "Frame", "Hint_MoveLeftRight" }, { "Hearts", "HUD_HealthBackground" },
            { "IconButton", "Button_Pause" }, { "Score", "HUD_ScoreBackground" }, { "Timer", "HUD_PowerupTimerRing" },
            { "obj_heart", "Pickup_Heart" }, { "obj_heart_empty", "HUD_HeartEmpty" },
            { "Items/balloon", "Items/Balloon_PowerupCarrier" }, { "Items/cloud", "Items/Cloud_Drifting" },
            { "Items/crow", "Items/Crow_HazardCarrier" }, { "Items/icon_grain_bag", "Items/Icon_EggBasket" },
            { "Items/obj_apple_rotten", "Items/Hazard_RottenApple" }, { "Items/obj_bomb", "Items/Hazard_Bomb" },
            { "Items/obj_corn", "Items/Pickup_Corn" }, { "Items/obj_grain", "Items/Pickup_Egg" },
            { "Items/obj_magnet", "Items/Pickup_Magnet" }, { "Items/obj_stone", "Items/Hazard_Rock" },
            { "Items/obj_worm", "Items/Pickup_Worm" }, { "Items/truck", "Items/Truck_EggCarrier" }
        };

        public static string Destination(string path)
        {
            var pairs = path.StartsWith("Assets/0_UI/") ? UiNames : SpriteNames;
            string prefix = path.StartsWith("Assets/0_UI/") ? "Assets/0_UI/" : "Assets/0_Sprites/";
            string key = path.Substring(prefix.Length, path.Length - prefix.Length - 4);
            if (pairs.TryGetValue(key, out var value)) return prefix + value + ".png";
            if (key.StartsWith("BG/Theme=")) return prefix + "BG/Gameplay_" + Title(key.Substring(9)) + ".png";
            if (key.Contains("Skin="))
            {
                int skinStart = key.IndexOf("Skin=", StringComparison.Ordinal) + 5;
                string skin = key.Substring(skinStart, key.IndexOf(",", StringComparison.Ordinal) - skinStart);
                string state = key.Substring(key.IndexOf("State=", StringComparison.Ordinal) + 6);
                string name = Title(skin) + "_" + (state == "stand" ? "Idle" : state == "hurt" ? "Hurt" : state == "run_left" ? "RunLeft" : "RunRight");
                return prefix + "Players/" + (prefix.Contains("0_Sprites") ? Title(skin) + "/" : "") + name + ".png";
            }
            return path;
        }

        private static string Title(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

        [MenuItem("Tools/Egg Catcher/Optimize Assets and Build Size")]
        public static void Optimize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the scene and exit Play Mode first.");
            Directory.CreateDirectory("Reports");
            if (!File.Exists("Reports/assets-before.tsv")) Audit("Reports/assets-before.tsv");
            var mappings = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/0_UI", "Assets/0_Sprites" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png"))
                .ToDictionary(p => p, Destination);
            var report = new StringBuilder("Old path\tNew path\tGUID\n");
            foreach (var pair in mappings)
            {
                if (pair.Key == pair.Value) continue;
                string guid = AssetDatabase.AssetPathToGUID(pair.Key);
                EnsureFolder(Path.GetDirectoryName(pair.Value).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(pair.Key, pair.Value);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (AssetDatabase.AssetPathToGUID(pair.Value) != guid) throw new InvalidOperationException("GUID changed: " + pair.Value);
                report.AppendLine(pair.Key + "\t" + pair.Value + "\t" + guid);
            }
            if (mappings.Any(pair => pair.Key != pair.Value)) File.WriteAllText("Reports/asset-renames.tsv", report.ToString());
            foreach (var file in Directory.GetFiles("Assets/EggCatcher", "*.cs", SearchOption.AllDirectories))
            {
                if (file.EndsWith("EggCatcherAssetMaintenance.cs")) continue;
                string original = File.ReadAllText(file), updated = original;
                foreach (var pair in mappings) updated = updated.Replace(pair.Key, pair.Value);
                if (updated != original) File.WriteAllText(file, updated);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in mappings.Values)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.isReadable = false;
                    importer.mipmapEnabled = false;
                    bool background = path.Contains("/BG/") || path.Contains("Background_Meadow") || path.Contains("Menu_Meadow");
                    foreach (var platform in new[] { "Android", "iPhone" })
                    {
                        var settings = importer.GetPlatformTextureSettings(platform);
                        settings.name = platform;
                        settings.overridden = true;
                        settings.maxTextureSize = path.Contains("/Players/") ? 512 : 2048;
                        settings.format = background ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
                        settings.compressionQuality = 100;
                        importer.SetPlatformTextureSettings(settings);
                    }
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Medium);
                PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            }
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Audit("Reports/assets-after.tsv");
            Debug.Log("Asset optimization completed. GUIDs retained. Reports/assets-before.tsv and assets-after.tsv.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Audit(string destination)
        {
            var report = new StringBuilder("Path\tWidth\tHeight\tRuntimeBytes\tFormat\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/0_UI", "Assets/0_Sprites" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                report.AppendLine(path + "\t" + texture.width + "\t" + texture.height + "\t" +
                    UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) + "\t" + texture.format);
            }
            File.WriteAllText(destination, report.ToString());
        }

        [MenuItem("Tools/Egg Catcher/Build Optimized Android APK")]
        public static void BuildApk()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the scene and exit Play Mode first.");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/Android/SunnyJoy-optimized.apk",
                target = BuildTarget.Android, options = BuildOptions.CompressWithLz4HC
            });
            Directory.CreateDirectory("Reports");
            var summary = new StringBuilder("Result: " + report.summary.result + "\nTotalBuildOutputBytes: " + report.summary.totalSize +
                "\nApkBytes: " + (File.Exists("Builds/Android/SunnyJoy-optimized.apk") ? new FileInfo("Builds/Android/SunnyJoy-optimized.apk").Length : 0) + "\n");
            foreach (var asset in report.packedAssets.SelectMany(p => p.contents).OrderByDescending(p => p.packedSize).Take(40))
                summary.AppendLine(asset.packedSize + "\t" + asset.sourceAssetPath);
            File.WriteAllText("Reports/optimized-build.txt", summary.ToString());
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed; inspect Console.");
        }
    }
}
