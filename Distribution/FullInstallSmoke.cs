// Staging-only integration test. Never exported in the distributable.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FullInstallSmoke
{
    private const string Pending = "WebWrapper.FullInstallSmoke.Pending";
    private const string Result = "Library/WebWrapperInstaller-result.txt";
    private const string Report = "Library/WebWrapperFullInstallSmoke.txt";
    static FullInstallSmoke()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Poll;
    }

    public static void Run()
    {
        Assert(Application.isBatchMode, "Must run in the isolated batch smoke project.");
        SessionState.SetString("Smoke.Product", PlayerSettings.productName);
        SessionState.SetString("Smoke.Bundle", PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
        SessionState.SetString("Smoke.Deadline", DateTime.UtcNow.AddMinutes(12).Ticks.ToString());
        SessionState.SetBool(Pending, true);
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Find("AlienProject.WebWrapperInstaller.WebWrapperPackageInstaller")
            .GetMethod("InstallForBatchValidation").Invoke(null, null);
    }

    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString("Smoke.Deadline", "0")))
                throw new Exception("Timed out waiting for full SDK installation/compilation.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Result)) return;
            var result = File.ReadAllText(Result);
            if (result.StartsWith("FAILED:")) throw new Exception(result);
            if (!result.StartsWith("SUCCESS:")) return;

            Assert(PlayerSettings.productName == SessionState.GetString("Smoke.Product", ""), "Installer changed product name.");
            Assert(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) == SessionState.GetString("Smoke.Bundle", ""), "Installer changed bundle ID.");
            foreach (var type in new[] { "UniWebView", "AppsFlyerSDK.AppsFlyer", "Firebase.FirebaseApp",
                         "Firebase.Messaging.FirebaseMessaging", "Unity.Notifications.NotificationCenter",
                         "AlienProject.WebWrapper.WebWrapperApp" }) Find(type);
            foreach (var asset in new[] { "Assets/Plugins/Android/UniWebView.aar", "Assets/Plugins/iOS/UniWebView.xcframework",
                         "Assets/Plugins/UniWebView.bundle" })
            {
                Assert(File.Exists(asset) || Directory.Exists(asset), "Missing native SDK: " + asset);
                Assert(AssetImporter.GetAtPath(asset) is PluginImporter, "Missing native importer: " + asset);
            }
            Assert(!Directory.Exists("Assets/UniWebView/Demo"), "Demo scene included.");
            Assert(!Directory.Exists("Assets/EggCatcher"), "Host game included.");
            Assert(!Directory.GetFiles("Assets", "google-services.json", SearchOption.AllDirectories).Any(), "Firebase credentials included.");
            Assert(!Directory.GetFiles("Assets", "GoogleService-Info.plist", SearchOption.AllDirectories).Any(), "iOS Firebase credentials included.");
            Assert(!File.Exists("Assets/AlienProject/WebWrapper/Resources/WebWrapperSettings.asset"), "Host settings included.");
            var manifest = File.ReadAllText("Packages/manifest.json");
            Assert(manifest.Contains("file:../WebWrapperPackages/"), "Package paths are not portable.");
            Assert(!manifest.Contains("/Users/") && !manifest.Contains("/private/tmp/"), "Absolute host path leaked into manifest.");

            CheckSceneSetup();
            SessionState.EraseBool(Pending);
            EditorApplication.update -= Poll;
            var text = "PASS: Imported full unitypackage into an empty project; installed all eight local UPM packages and UniWebView; " +
                       "compiled all SDKs; preserved host identity; verified native Android/iOS/macOS importers, relative package paths, " +
                       "no game/demo/Firebase app files, blank defaults, scene component -> generated settings, and missing-Firebase build guard. " +
                       "No real credentials or runtime/network requests used.\n";
            File.WriteAllText(Report, text);
            Debug.Log(text);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.EraseBool(Pending);
            File.WriteAllText(Report, "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckSceneSetup()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var app = new GameObject("WebWrapper App").AddComponent(Find("AlienProject.WebWrapper.WebWrapperApp"));
        var so = new SerializedObject(app);
        foreach (var field in new[] { "appleAppId", "firebaseDatabaseUrl", "firebaseConfigPath", "appsFlyerDevKey", "oneLinkDomain", "iosUrlScheme", "privacyPolicyUrl" })
            Assert(string.IsNullOrEmpty(so.FindProperty(field).stringValue), "Nonblank per-app default: " + field);
        so.FindProperty("applicationName").stringValue = "Migration Fixture";
        so.FindProperty("bundleIdentifier").stringValue = "com.migration.fixture";
        so.FindProperty("firebaseDatabaseUrl").stringValue = "https://migration-fixture-default-rtdb.firebaseio.com";
        so.FindProperty("firebaseConfigPath").stringValue = "apps/fixture/webview";
        so.FindProperty("appsFlyerDevKey").stringValue = "LOCAL_VALIDATION_ONLY";
        var game = new GameObject("Game Root");
        game.SetActive(false);
        so.FindProperty("gameRoot").objectReferenceValue = game;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, "Assets/MigrationFixture.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/MigrationFixture.unity", true) };
        var setup = Find("AlienProject.WebWrapper.Editor.WebWrapperAppSetup");
        var settings = (UnityEngine.Object)setup.GetMethod("Synchronize").Invoke(null, new object[] { app });
        var values = new SerializedObject(settings);
        Assert(values.FindProperty("autoStart").boolValue, "Auto Start disabled.");
        Assert(!values.FindProperty("useMockServices").boolValue, "Mock mode enabled.");
        Assert(string.IsNullOrEmpty(values.FindProperty("configEndpoint").stringValue), "Legacy endpoint persisted.");
        Assert(string.IsNullOrEmpty(values.FindProperty("mockWebUrl").stringValue), "Mock URL persisted.");
        Assert(((string[])setup.GetMethod("Validate").Invoke(null, new object[] { app, false })).Length == 0, "Valid Android configuration rejected.");
        try
        {
            setup.GetMethod("PrepareBuild").Invoke(null, new object[] { BuildTarget.Android });
            throw new Exception("Build with no Firebase json should be rejected.");
        }
        catch (TargetInvocationException error)
        {
            Assert(error.InnerException.Message.Contains("google-services.json"), "Unexpected build guard: " + error.InnerException.Message);
        }
    }

    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null) ?? throw new Exception("Missing compiled type: " + name);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
}
