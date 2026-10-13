using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class GameFallbackTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private WebWrapperController controller;
        private WebWrapperSettings settings;

        [SetUp]
        public void SetUp()
        {
            controller = new GameObject("Fallback test").AddComponent<WebWrapperController>();
            settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            typeof(WebWrapperSettings).GetField("useMockServices", Private).SetValue(settings, false);
            Set("_settings", settings);
        }

        [TearDown]
        public void TearDown()
        {
            Call("OnDestroy");
            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void MissingWebView_OpensGameWithoutOverlay()
        {
            Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com", "server"));
            AssertGame();
        }

        [Test]
        public async System.Threading.Tasks.Task DisabledWebView_SkipsRemoteStartupAndLocksGame()
        {
            typeof(WebWrapperSettings).GetField("webViewEnabled", Private).SetValue(settings, false);
            await (System.Threading.Tasks.Task)Call("RunAsync");
            AssertGame();
            Assert.That(Get("_gameSessionLocked"), Is.True);
            Assert.That(WebWrapperLaunchState.CurrentDecision.Reason, Does.Contain("disabled in WebWrapper App"));
            Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com/late", "late route"));
            AssertGame();
        }

        [Test]
        public void DisabledWebView_RejectsDirectWebDecision()
        {
            typeof(WebWrapperSettings).GetField("webViewEnabled", Private).SetValue(settings, false);
            Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com/", "server"));
            AssertGame();
            Assert.That(WebWrapperLaunchState.CurrentDecision.Reason, Does.Contain("disabled in WebWrapper App"));
        }

        [Test]
        public void OfflineAndConfigurationError_OpenGameWithoutOverlay()
        {
            Call("ApplyDecision", LaunchDecision.ShowOffline("offline"));
            AssertGame();
            Call("ApplyDecision", LaunchDecision.ConfigurationError("SDK missing"));
            AssertGame();
        }

        [Test]
        public void MissingSettings_StillOpensGame()
        {
            Set("_settings", null);
            Call("FallBackToGame", "missing settings");
            AssertGame();
        }

        [Test]
        public void NativeShowThrows_OpensGameAndHidesNativeView()
        {
            var previous = WebWrapperServices.WebViewAdapter;
            var native = new ThrowingWeb();
            try
            {
                Set("_webView", native);
                WebWrapperServices.WebViewAdapter = native;
                Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com", "server"));
                AssertGame();
                Assert.Greater(native.Hides, 0);
            }
            finally { WebWrapperServices.WebViewAdapter = previous; }
        }

        [Test]
        public void LoadingFailure_ClearsLoadingAndPendingPushRoute()
        {
            Set("_sessionPushUrl", "https://example.com/push");
            Set("_webLoading", true);
            Call("OnWebViewLoadFailed", "timeout");
            AssertGame();
            Assert.AreEqual(false, Get("_webLoading"));
            Assert.AreEqual("", Get("_sessionPushUrl"));
        }

        private void AssertGame()
        {
            Assert.AreEqual(LaunchDecisionKind.Wrapper, WebWrapperLaunchState.CurrentDecision.Kind);
            Assert.IsTrue(WebWrapperLaunchState.CanStartGame(true));
            Assert.AreEqual(false, Get("_showOverlay"));
        }

        [Test]
        public void LocalMenuCommit_CancelsStartup_AndRejectsLateRoutes()
        {
            using (var startup = new System.Threading.CancellationTokenSource())
            {
                Set("_startupCancellation", startup);
                controller.ContinueInGameForSession();
                Assert.That(startup.IsCancellationRequested, Is.True);
                AssertGame();
                Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com/late", "late server"));
                AssertGame();
                Call("OnWebViewLoadCompleted");
                Assert.That(controller.HasVisibleWebView, Is.False);
                Set("_startupCancellation", null);
            }
        }

        [Test]
        public void NormalGameMenuCommit_PreservesReason_AndLocksFurtherNavigation()
        {
            Call("ApplyDecision", LaunchDecision.ShowWrapper("server selected game"));
            controller.ContinueInGameForSession();
            Assert.That(WebWrapperLaunchState.CurrentDecision.Reason, Is.EqualTo("server selected game"));
            Call("ApplyDecision", LaunchDecision.ShowWebView("https://example.com/late", "late route"));
            AssertGame();
        }

        [Test]
        public void AlreadyVisibleWebView_IsNotInterruptedByLocalPreparation()
        {
            Set("_decision", LaunchDecision.ShowWebView("https://example.com/", "ready"));
            Set("_showOverlay", false);
            Set("_webLoading", false);
            controller.ContinueInGameForSession();
            Assert.That(controller.HasVisibleWebView, Is.True);
            Assert.That(Get("_gameSessionLocked"), Is.False);
        }
        private void Set(string name, object value) => controller.GetType().GetField(name, Private).SetValue(controller, value);
        private object Get(string name) => controller.GetType().GetField(name, Private).GetValue(controller);
        private object Call(string name, params object[] args) => controller.GetType().GetMethod(name, Private).Invoke(controller, args);
        private sealed class ThrowingWeb : IWebViewAdapter
        {
            public bool IsAvailable => true;
            public bool CanGoBack => false;
            public int Hides;
            public void Show(string url) => throw new System.InvalidOperationException("native failure");
            public void Hide() { Hides++; }
            public void GoBack() { }
        }
    }
}
