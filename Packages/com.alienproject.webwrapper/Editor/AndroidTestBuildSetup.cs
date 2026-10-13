using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    public static class AndroidTestBuildSetup
    {
        [MenuItem("Tools/Alien Project/Prepare Android Test APK")]
        public static void Prepare() => PrepareForBuild(true);

        [MenuItem("Tools/Alien Project/Prepare Android Non-Development APK")]
        public static void PrepareNonDevelopment() => PrepareForBuild(false);

        private static bool PrepareForBuild(bool development)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Android test] Exit Play Mode before preparing the APK.");
                return false;
            }
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled))
            {
                Debug.LogError("[WebWrapper] Add your startup scene to Build Profiles.");
                return false;
            }
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Android test] Could not activate Android. Install Android Build Support in Unity Hub.");
                return false;
            }
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            WebWrapperAppSetup.PrepareBuild(BuildTarget.Android);
            AssetDatabase.SaveAssets();
            var issue = FirebaseIssue();
            if (issue != null) Debug.LogWarning("[Android test] " + issue);
            Debug.Log("[Android test] Prepared ARM64 " + (development ? "Development APK with Logs" : "non-Development APK without Logs") + ". No APK was built. " +
                "Use File > Build Profiles > Android > Build. Firebase: " + (issue ?? "configuration matches"));
            return true;
        }

        [MenuItem("Tools/Alien Project/Build Android Non-Development APK")]
        public static void BuildNonDevelopment()
        {
            if (!PrepareForBuild(false)) return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var directory = Path.GetFullPath("Builds/Android");
            Directory.CreateDirectory(directory);
            var name = string.Concat(PlayerSettings.productName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            if (string.IsNullOrEmpty(name)) name = "App";
            var output = Path.Combine(directory, name + ".apk");
            if (File.Exists(output)) output = Path.Combine(directory,
                name + "-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            });
            var summary = report.summary;
            File.WriteAllText("Library/WebWrapperAndroidBuild.txt",
                "Result: " + summary.result + "\nAPK: " + output + "\nOptions: " + summary.options +
                "\nBuild output bytes: " + summary.totalSize +
                "\nAPK bytes: " + (File.Exists(output) ? new FileInfo(output).Length : 0) +
                "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings + "\n");
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Non-Development APK failed; see Library/WebWrapperAndroidBuild.txt and Unity Console.");
            Debug.Log("[Android test] Non-Development APK built: " + output);
        }

        public static string FirebaseIssue()
        {
            var bundle = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var files = Directory.GetFiles(Application.dataPath, "google-services.json", SearchOption.AllDirectories);
            if (files.Length != 1) return "Keep exactly one Android google-services.json under Assets for " + bundle +
                ". Generated FirebaseApp.androidlib resources are not a replacement.";
            return FirebaseConfigurationValidator.AndroidMatches(File.ReadAllText(files[0]), bundle)
                ? null : "google-services.json does not contain an Android client for " + bundle + ". Download the correct file from Firebase; do not edit its IDs by hand.";
        }
    }

    internal sealed class AndroidFirebaseBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (settings == null || settings.UseMockServices) return;
            var issue = AndroidTestBuildSetup.FirebaseIssue();
            if (issue != null) throw new BuildFailedException("[WebWrapper] " + issue);
        }
    }
}
