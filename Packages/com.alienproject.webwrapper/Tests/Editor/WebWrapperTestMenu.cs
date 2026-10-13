using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public static class WebWrapperTestMenu
    {
        public const string ResultPath = "Library/WebWrapperTestResults.txt";

        private static TestRunnerApi _api;
        private static TestCallbacks _callbacks;

        [MenuItem("Tools/Alien Project/Run Web Wrapper Tests")]
        public static void Run()
        {
            if (File.Exists(ResultPath))
            {
                File.Delete(ResultPath);
            }

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = ScriptableObject.CreateInstance<TestCallbacks>();
            _api.RegisterCallbacks(_callbacks);
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "AlienProject.WebWrapper.Editor.Tests" }
            };
            _api.Execute(new ExecutionSettings(filter));
            Debug.Log("[WebWrapper] Edit Mode test run started.");
        }

        private sealed class TestCallbacks : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new StringBuilder();
                report.AppendLine("Web Wrapper Edit Mode Tests");
                report.AppendLine("Status: " + result.TestStatus);
                report.AppendLine("Passed: " + result.PassCount);
                report.AppendLine("Failed: " + result.FailCount);
                report.AppendLine("Skipped: " + result.SkipCount);
                AppendFailures(result, report);
                File.WriteAllText(ResultPath, report.ToString());
                Debug.Log("[WebWrapper] Test run finished.\n" + report);

                if (_api != null && _callbacks != null)
                {
                    _api.UnregisterCallbacks(_callbacks);
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder report)
            {
                if (result.FailCount > 0 && !result.Children.Any())
                {
                    report.AppendLine();
                    report.AppendLine(result.FullName);
                    report.AppendLine(result.Message);
                    report.AppendLine(result.StackTrace);
                }

                foreach (var child in result.Children)
                {
                    AppendFailures(child, report);
                }
            }
        }
    }
}
