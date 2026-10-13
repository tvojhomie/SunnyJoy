using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienProject.WebWrapper.Editor
{
    [InitializeOnLoad]
    public static class WebWrapperAppSetup
    {
        public const string SettingsPath = "Assets/AlienProject/WebWrapper/Resources/WebWrapperSettings.asset";
        private static bool _syncing;

        static WebWrapperAppSetup()
        {
            WebWrapperApp.EditorConfigurationChanged += app =>
            {
                if (!app.gameObject.scene.IsValid() || string.IsNullOrEmpty(app.gameObject.scene.path) ||
                    EditorSceneManager.IsPreviewScene(app.gameObject.scene)) return;
                if (FindInLoadedScenes().Length == 1) Synchronize(app);
            };
            EditorSceneManager.sceneSaved += scene =>
            {
                var apps = InScene(scene);
                if (apps.Length == 1 && FindInLoadedScenes().Length == 1) Synchronize(apps[0]);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                var apps = FindInLoadedScenes();
                var existing = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
                if (apps.Length == 0 && string.IsNullOrEmpty(existing?.SourceSceneGuid)) return;
                if (apps.Length != 1 || string.IsNullOrEmpty(apps[0].gameObject.scene.path))
                {
                    Debug.LogError("[WebWrapper] Play requires one active WebWrapperApp in a saved startup scene.");
                    EditorApplication.isPlaying = false;
                    return;
                }
                Synchronize(apps[0]);
            };
        }

        public static WebWrapperApp[] FindInLoadedScenes() => UnityEngine.Object.FindObjectsByType<WebWrapperApp>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(a => a.enabled &&
                !EditorUtility.IsPersistent(a) && !EditorSceneManager.IsPreviewScene(a.gameObject.scene)).ToArray();

        private static WebWrapperApp[] InScene(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<WebWrapperApp>(true))
            .Where(app => app.enabled && app.gameObject.activeInHierarchy).ToArray();

        [MenuItem("Tools/Alien Project/Add Web Wrapper App to Scene")]
        public static void AddToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var apps = FindInLoadedScenes();
            if (apps.Length > 0) { Selection.activeGameObject = apps[0].gameObject; return; }
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[WebWrapper] Save the startup scene before adding WebWrapperApp.");
                return;
            }
            var host = new GameObject("WebWrapper App");
            Undo.RegisterCreatedObjectUndo(host, "Add WebWrapper App");
            var app = Undo.AddComponent<WebWrapperApp>(host);
            var old = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (old != null) ImportCurrentSettings(app, old);
            Synchronize(app);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = host;
            Debug.Log("[WebWrapper] Scene component created. Current connection values preserved; mock/HTTP data removed from generated settings.");
        }

        private static void ImportCurrentSettings(WebWrapperApp app, WebWrapperSettings old)
        {
            var values = new SerializedObject(app);
            values.FindProperty("webViewEnabled").boolValue = old.WebViewEnabled;
            void Set(string field, string value) => values.FindProperty(field).stringValue = value ?? string.Empty;
            Set("applicationName", PlayerSettings.productName);
            Set("bundleIdentifier", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            Set("appleAppId", old.AppsFlyerIosAppId);
            Set("firebaseDatabaseUrl", old.FirebaseDatabaseUrl);
            Set("firebaseConfigPath", old.FirebaseConfigPath);
            Set("appsFlyerDevKey", old.AppsFlyerDevKey);
            Set("oneLinkDomain", old.OneLinkDomain);
            Set("iosUrlScheme", old.IosUrlScheme);
            Set("privacyPolicyUrl", old.PrivacyPolicyUrl);
            values.ApplyModifiedPropertiesWithoutUndo();
        }

        public static WebWrapperSettings Synchronize(WebWrapperApp app)
        {
            if (_syncing || app == null) return null;
            _syncing = true;
            try
            {
                var paths = AssetDatabase.FindAssets("t:WebWrapperSettings").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.Contains("/Resources/") && Path.GetFileNameWithoutExtension(p) == WebWrapperSettings.ResourceName).ToArray();
                if (paths.Length > 1) throw new InvalidOperationException("Keep exactly one Resources/WebWrapperSettings asset; duplicate configurations are ambiguous.");
                var path = paths.Length == 1 ? paths[0] : SettingsPath;
                var settings = AssetDatabase.LoadAssetAtPath<WebWrapperSettings>(path);
                var fresh = ScriptableObject.CreateInstance<WebWrapperSettings>();
                fresh.name = settings != null ? settings.name : WebWrapperSettings.ResourceName;
                fresh.ConfigureFromApp(app, AssetDatabase.AssetPathToGUID(app.gameObject.scene.path));
                if (settings == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    AssetDatabase.CreateAsset(fresh, path);
                    settings = fresh;
                }
                else
                {
                    if (EditorJsonUtility.ToJson(settings) != EditorJsonUtility.ToJson(fresh))
                    {
                        EditorUtility.CopySerialized(fresh, settings);
                        EditorUtility.SetDirty(settings);
                    }
                    UnityEngine.Object.DestroyImmediate(fresh);
                }
                if (ValidBundle(app.BundleIdentifier))
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, app.BundleIdentifier.Trim());
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, app.BundleIdentifier.Trim());
                }
                if (!string.IsNullOrWhiteSpace(app.ApplicationName)) PlayerSettings.productName = app.ApplicationName.Trim();
                AssetDatabase.SaveAssets();
                return settings;
            }
            finally { _syncing = false; }
        }

        public static string[] Validate(WebWrapperApp app, bool ios)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(app.ApplicationName)) issues.Add("Application Name is required.");
            if (!ValidBundle(app.BundleIdentifier)) issues.Add("Bundle Identifier must be a reverse-DNS application ID.");
            if (!FirebaseDatabaseConfigClient.TryBuildEndpoint(app.FirebaseDatabaseUrl.Trim(), app.FirebaseConfigPath.Trim(), out _))
                issues.Add("Enter the Firebase Database URL and config node path (enabled/url).");
            if (string.IsNullOrWhiteSpace(app.AppsFlyerDevKey)) issues.Add("AppsFlyer Dev Key is required.");
            if (ios && !StoreIdNormalizer.IsValidIos(app.AppleAppId.Trim())) issues.Add("Apple App ID must be numeric.");
            if (!string.IsNullOrWhiteSpace(app.PrivacyPolicyUrl) && !Https(app.PrivacyPolicyUrl)) issues.Add("Privacy Policy must be an HTTPS URL.");
            if (!string.IsNullOrWhiteSpace(app.OneLinkDomain) &&
                !Regex.IsMatch(app.OneLinkDomain.Trim(), @"^[a-zA-Z0-9-]+\.onelink\.me$")) issues.Add("OneLink Domain must be a host such as yourapp.onelink.me, without https:// or a path.");
            if (!string.IsNullOrWhiteSpace(app.IosUrlScheme) &&
                !Regex.IsMatch(app.IosUrlScheme.Trim(), @"^[a-zA-Z][a-zA-Z0-9+.-]*$")) issues.Add("iOS URL Scheme must not contain ://.");
            foreach (var root in new[] { app.GameRoot, app.LoadingRoot })
                if (root != null && app.transform.IsChildOf(root.transform)) issues.Add("Game/Loading roots must not contain the WebWrapperApp component.");
            if (app.GameRoot != null && app.LoadingRoot != null &&
                (app.GameRoot.transform.IsChildOf(app.LoadingRoot.transform) || app.LoadingRoot.transform.IsChildOf(app.GameRoot.transform)))
                issues.Add("Game Root and Loading Root must be separate, non-nested roots.");
            if (app.GameRoot != null && app.GameRoot.activeSelf) issues.Add("Save Game Root inactive so its Awake/audio cannot run before the routing decision.");
            return issues.ToArray();
        }

        public static bool ValidBundle(string value) => !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(value.Trim(), @"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$");
        private static bool Https(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

        public static void PrepareBuild(BuildTarget target)
        {
            var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            if (first == null) throw new BuildFailedException("[WebWrapper] Add the startup scene to Build Profiles.");
            var loaded = SceneManager.GetSceneByPath(first.path);
            var preview = !loaded.IsValid() || !loaded.isLoaded;
            var scene = preview ? EditorSceneManager.OpenPreviewScene(first.path) : loaded;
            try
            {
                var apps = InScene(scene);
                var old = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
                if (apps.Length == 0 && string.IsNullOrEmpty(old?.SourceSceneGuid)) return;
                if (apps.Length != 1) throw new BuildFailedException("[WebWrapper] The FIRST build scene needs exactly one active WebWrapperApp.");
                var issues = Validate(apps[0], target == BuildTarget.iOS).ToList();
                foreach (var name in new[] { "AppsFlyerSDK.AppsFlyer", "Firebase.Messaging.FirebaseMessaging", "UniWebView" })
                    if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(name, false) != null)) issues.Add("Install SDK: " + name);
                if (issues.Count > 0) throw new BuildFailedException("[WebWrapper]\n" + string.Join("\n", issues));
                Synchronize(apps[0]);
                if (target == BuildTarget.Android)
                {
                    PlayerSettings.Android.forceInternetPermission = true;
                    PlayerSettings.Android.startInFullscreen = true;
                    PlayerSettings.Android.renderOutsideSafeArea = true;
                    PlayerSettings.Android.optimizedFramePacing = true;
                    PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                    var issue = AndroidTestBuildSetup.FirebaseIssue();
                    if (issue != null) throw new BuildFailedException(issue);
                }
                if (target == BuildTarget.iOS)
                {
                    var files = Directory.GetFiles(Application.dataPath, "GoogleService-Info.plist", SearchOption.AllDirectories);
                    if (files.Length != 1 || !FirebaseConfigurationValidator.IosMatches(File.ReadAllText(files[0]), apps[0].BundleIdentifier.Trim()))
                        throw new BuildFailedException("[WebWrapper] Add this app's GoogleService-Info.plist. Its BUNDLE_ID must match the scene component.");
                }
            }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    internal sealed class WebWrapperAppBuildPreparation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => WebWrapperAppSetup.PrepareBuild(report.summary.platform);
    }

    internal sealed class WebWrapperAppSceneGuard : IProcessSceneWithReport
    {
        public int callbackOrder => -1000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            var apps = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WebWrapperApp>(true))
                .Where(app => app.enabled && app.gameObject.activeInHierarchy).ToArray();
            if (apps.Length == 0) return;
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (apps.Length != 1 || settings == null || settings.SourceSceneGuid != AssetDatabase.AssetPathToGUID(scene.path))
                throw new BuildFailedException("[WebWrapper] A second or unsynchronized WebWrapperApp was included in the build. Keep one component in the first build scene.");
        }
    }
}
