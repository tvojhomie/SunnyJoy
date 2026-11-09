using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SunnyJoy.Tests
{
    public static class SunnyJoyTestMenu
    {
        private static TestRunnerApi api;
        private static Results callbacks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KeepTestPlayerLoopRunning()
        {

            if (SessionState.GetBool("SunnyJoy.TestsRunning", false)) Application.runInBackground = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreCallbacks()
        {
            if (!SessionState.GetBool("SunnyJoy.TestsRunning", false)) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = ScriptableObject.CreateInstance<Results>();
            api.RegisterCallbacks(callbacks);
        }

        [MenuItem("Tools/SunnyJoy/Run UI and Data Tests")]
        public static void Run() => RunFilter(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "SunnyJoy.Editor.Tests" } });

        [MenuItem("Tools/SunnyJoy/Run Screen Adaptation Tests")]
        public static void RunLayout() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "SunnyJoy.Tests.ResponsiveLayoutTests", "SunnyJoy.Tests.RoundStatusLayoutTests" } });

        [MenuItem("Tools/SunnyJoy/Run Gameplay UI Tests")]
        public static void RunGameplay() => RunFilter(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "SunnyJoy.Tests.GameplayFlowTests", "SunnyJoy.Tests.RoundStatusLayoutTests", "SunnyJoy.Tests.ExpandedRoundTests" } });

        private static void RunFilter(Filter filter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the current scene and exit Play Mode before running UI tests.");
            SessionState.SetBool("SunnyJoy.TestsRunning", true);
            SessionState.SetBool("SunnyJoy.TestsPreviousRunInBackground", Application.runInBackground);
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
                if (!SessionState.GetBool("SunnyJoy.TestsRunning", false)) return;
                SessionState.SetBool("SunnyJoy.TestsRunning", false);
                Application.runInBackground = SessionState.GetBool("SunnyJoy.TestsPreviousRunInBackground", false);
                var report = new StringBuilder("SunnyJoy UI and Data Tests\n");
                report.AppendLine("Status: " + result.TestStatus);
                report.AppendLine("Passed: " + result.PassCount + "; Failed: " + result.FailCount + "; Skipped: " + result.SkipCount);
                AppendFailures(result, report);
                File.WriteAllText("Library/SunnyJoyTestResults.txt", report.ToString());
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
