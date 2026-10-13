using System.Reflection;
using System.Collections;
using AlienProject.WebWrapper;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace EggCatcher.Tests
{
    public sealed class AndroidConsoleTests
    {
        [UnityTest]
        public IEnumerator Console_RenderedInPlayMode_HasVisibleControls()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Object.FindFirstObjectByType<EggCatcherApp>().enabled = false;
            yield return new EnterPlayMode();
            var host = new GameObject("Console preview test", typeof(RectTransform));
            host.AddComponent<AndroidTestConsole>();
            typeof(WebWrapperDiagnostics).GetMethod("Append", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "TEST", "Console UI preview only — no network request.\nFallback and SDK registration messages appear here on device." });
            WebWrapperDiagnostics.ConsoleOpen = true;
            yield return null;
            yield return null;
            var panel = host.transform.Find("Safe Area/Console");
            Assert.IsTrue(panel.gameObject.activeInHierarchy);
            Assert.IsTrue(panel.Find("Copy").GetComponent<Button>().IsInteractable());
            Assert.Greater(panel.GetComponent<RectTransform>().rect.width, 100);
            Assert.Greater(panel.GetComponent<RectTransform>().rect.height, 100);
            ScreenCapture.CaptureScreenshot("Library/AndroidConsolePreview.png");
            yield return null;
            yield return null;
            panel.Find("Close").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(WebWrapperDiagnostics.ConsoleOpen);
            Object.Destroy(host);
            WebWrapperDiagnostics.Clear();
            yield return new ExitPlayMode();
        }

        [Test]
        public void Console_HasBlockingOverlayAndToggleDoesNotChangeRoute()
        {
            var host = new GameObject("Console test");
            var decision = WebWrapperLaunchState.CurrentDecision;
            try
            {
                var console = host.AddComponent<AndroidTestConsole>();
                typeof(AndroidTestConsole).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(console, null);
                var panel = host.transform.Find("Safe Area/Console");
                Assert.IsFalse(panel.gameObject.activeSelf);
                Assert.IsTrue(panel.GetComponent<Image>().raycastTarget);
                Assert.AreEqual(32760, host.GetComponent<Canvas>().sortingOrder);
                Assert.IsFalse(panel.Find("Viewport/Log Text").GetComponent<Text>().supportRichText);
                host.transform.Find("Safe Area/Logs (test APK)").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(panel.gameObject.activeSelf);
                Assert.IsTrue(WebWrapperDiagnostics.ConsoleOpen);
                panel.Find("Close").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(WebWrapperDiagnostics.ConsoleOpen);
                Assert.AreSame(decision, WebWrapperLaunchState.CurrentDecision);
            }
            finally { Object.DestroyImmediate(host); WebWrapperDiagnostics.ConsoleOpen = false; }
        }
    }
}
