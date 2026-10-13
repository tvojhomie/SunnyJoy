using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace EggCatcher.Tests
{
    public static class EggCatcherTestMenu
    {
        private static TestRunnerApi api;
        private static Results callbacks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KeepTestPlayerLoopRunning()
        {

            if (SessionState.GetBool("EggCatcher.TestsRunning", false)) Application.runInBackground = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreCallbacks()
        {
            if (!SessionState.GetBool("EggCatcher.TestsRunning", false)) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = ScriptableObject.CreateInstance<Results>();
            api.RegisterCallbacks(callbacks);
        }

        [MenuItem("Tools/Egg Catcher/Run UI and Data Tests")]
        public static void Run() => RunFilter(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "EggCatcher.Editor.Tests" } });

        [MenuItem("Tools/Egg Catcher/Run Startup and Push UI Tests")]
        public static void RunStartup() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "EggCatcher.Tests.PushPanelTests", "EggCatcher.Tests.LaunchGateTests" } });

        [MenuItem("Tools/Egg Catcher/Run Android Console Tests")]
        public static void RunConsole() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "EggCatcher.Tests.AndroidConsoleTests" } });

        [MenuItem("Tools/Egg Catcher/Run Screen Adaptation Tests")]
        public static void RunLayout() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "EggCatcher.Tests.ResponsiveLayoutTests", "EggCatcher.Tests.RoundStatusLayoutTests" } });

        [MenuItem("Tools/Egg Catcher/Run Gameplay UI Tests")]
        public static void RunGameplay() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "EggCatcher.Tests.GameplayFlowTests", "EggCatcher.Tests.RoundStatusLayoutTests", "EggCatcher.Tests.ExpandedRoundTests" } });

        [MenuItem("Tools/Egg Catcher/Run Privacy Policy UI Tests")]
        public static void RunPrivacy() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "EggCatcher.Tests.PrivacyPanelTests" } });

        private static void RunFilter(Filter filter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the current scene and exit Play Mode before running UI tests.");
            SessionState.SetBool("EggCatcher.TestsRunning", true);
            SessionState.SetBool("EggCatcher.TestsPreviousRunInBackground", Application.runInBackground);
            RestoreCallbacks();
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Results : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("EggCatcher.TestsRunning", false)) return;
                SessionState.SetBool("EggCatcher.TestsRunning", false);
                Application.runInBackground = SessionState.GetBool("EggCatcher.TestsPreviousRunInBackground", false);
                var report = new StringBuilder("Egg Catcher UI and Data Tests\n");
                report.AppendLine("Status: " + result.TestStatus);
                report.AppendLine("Passed: " + result.PassCount + "; Failed: " + result.FailCount + "; Skipped: " + result.SkipCount);
                AppendFailures(result, report);
                File.WriteAllText("Library/EggCatcherTestResults.txt", report.ToString());
                Debug.Log(report.ToString());
                if (api != null && callbacks != null) api.UnregisterCallbacks(callbacks);
            }
            private static void AppendFailures(ITestResultAdaptor result, StringBuilder report)
            {
                if (result.FailCount > 0 && !result.HasChildren) report.AppendLine(result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
                foreach (var child in result.Children) AppendFailures(child, report);
            }
        }
    }
}
