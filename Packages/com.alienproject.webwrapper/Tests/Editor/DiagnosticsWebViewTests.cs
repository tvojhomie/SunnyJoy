using System;
using System.Collections.Generic;
using System.Reflection;
using AlienProject.WebWrapper.Integrations.UniWebView;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class DiagnosticsFakeNativeView : MonoBehaviour
    {
        public int Shows;
        public int Hides;
        public int Loads;
        public DiagnosticsFakeToolbar EmbeddedToolbar { get; } = new DiagnosticsFakeToolbar();
        public void Show() { Shows++; }
        public void Hide() { Hides++; }
        public void Load(string url) { Loads++; }
    }

    public sealed class DiagnosticsFakeToolbar
    {
        public int Hides;
        public void Hide() { Hides++; }
    }

    public sealed class DiagnosticsWebViewTests
    {
        private GameObject host;
        private ReflectionUniWebViewAdapter adapter;
        private DiagnosticsFakeNativeView view;
        private IWebViewAdapter previous;

        [SetUp]
        public void SetUp()
        {
            previous = WebWrapperServices.WebViewAdapter;
            host = new GameObject("Diagnostic native view test");
            adapter = host.AddComponent<ReflectionUniWebViewAdapter>();
            view = host.AddComponent<DiagnosticsFakeNativeView>();
            Set("_webView", view);
            Set("_webViewType", typeof(DiagnosticsFakeNativeView));
            Set("_showRequested", true);
            Set("_nativeVisible", true);
        }

        [TearDown]
        public void TearDown()
        {

            Set("_webView", null);
            UnityEngine.Object.DestroyImmediate(host);
            WebWrapperServices.WebViewAdapter = previous;
            WebWrapperDiagnostics.ConsoleOpen = false;
        }

        [Test]
        public void ReleaseWebView_HidesNativeToolbar_WithoutPrivacyAction()
        {
            Assert.That(WebWrapperDiagnostics.Enabled, Is.False);
            Call("ConfigureEmbeddedToolbar");
            Assert.That(view.EmbeddedToolbar.Hides, Is.EqualTo(1));
            Assert.That((object)adapter, Is.Not.InstanceOf<IWebViewPrivacyControls>());
            var handled = typeof(ReflectionUniWebViewAdapter).GetMethod("OnToolbarAction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(adapter, new object[] { new ToolbarAction { Identifier = "webwrapper.privacy" } });
            Assert.That(handled, Is.False);
        }

        private sealed class ToolbarAction
        {
            public string Identifier { get; set; }
        }

        [Test]
        public void AndroidFullscreenFrame_DoesNotLeaveHostVisibleAbovePage()
        {
            var safe = new Rect(0, 60, 1080, 2250);
            Assert.AreEqual(new Rect(0, 0, 1080, 2400),
                ReflectionUniWebViewAdapter.CalculateViewportFrame(safe, 1080, 2400, 0, true));
        }

        [TestCase(90, 2250)]
        [TestCase(0, 2310)]
        public void AndroidLandscapeFrame_FillsViewportWithEitherCutoutSide(int left, int width)
        {
            var safe = new Rect(left, 0, width, 1080);
            Assert.AreEqual(new Rect(0, 0, 2400, 1080),
                ReflectionUniWebViewAdapter.CalculateViewportFrame(safe, 2400, 1080, 0, true));
        }

        [Test]
        public void AndroidDevelopmentFrame_OnlyReservesBottomDiagnostics()
        {
            var safe = new Rect(0, 60, 1080, 2250);
            Assert.AreEqual(new Rect(0, 0, 1080, 2256),
                ReflectionUniWebViewAdapter.CalculateViewportFrame(safe, 1080, 2400, 144, true));
        }

        [Test]
        public void IosViewportFrame_PreservesExistingSafeAreaPolicy()
        {
            var safe = new Rect(0, 60, 1080, 2250);
            Assert.AreEqual(new Rect(0, 90, 1080, 2250),
                ReflectionUniWebViewAdapter.CalculateViewportFrame(safe, 1080, 2400, 0, false));
        }

        [Test]
        public void NonDevelopmentFrame_FillsSafeAreaWithoutDebugGap()
        {
            var safe = new Rect(0, 60, 1080, 2250);
            var frame = ReflectionUniWebViewAdapter.CalculateNativeFrame(safe, 2400, 0);
            Assert.AreEqual(new Rect(0, 90, 1080, 2250), frame);
        }

        [Test]
        public void DevelopmentFrame_ReservesOnlyDiagnosticToolbar()
        {
            var safe = new Rect(0, 60, 1080, 2250);
            var frame = ReflectionUniWebViewAdapter.CalculateNativeFrame(safe, 2400, 144);
            Assert.AreEqual(new Rect(0, 90, 1080, 2106), frame);
        }

        [Test]
        public void LandscapeFrame_RespectsSideCutoutWithoutPortraitGap()
        {
            var safe = new Rect(90, 0, 2250, 1080);
            Assert.AreEqual(safe, ReflectionUniWebViewAdapter.CalculateNativeFrame(safe, 1080, 0));
        }

        [Test]
        public void OpenAndCloseConsole_HidesAndRestoresWithoutLoading()
        {
            WebWrapperDiagnostics.ConsoleOpen = true;
            Tick(); Tick();
            Assert.AreEqual(1, view.Hides);
            WebWrapperDiagnostics.ConsoleOpen = false;
            Tick(); Tick();
            Assert.AreEqual(1, view.Shows);
            Assert.AreEqual(0, view.Loads);
        }

        [Test]
        public void ClosingConsole_DoesNotReopenHiddenRoute()
        {
            WebWrapperDiagnostics.ConsoleOpen = true;
            Tick();
            Set("_showRequested", false);
            WebWrapperDiagnostics.ConsoleOpen = false;
            Tick();
            Assert.AreEqual(0, view.Shows);
        }

        [Test]
        public void PageFinishesWhileConsoleOpen_RemainsHiddenUntilClosed()
        {
            WebWrapperDiagnostics.ConsoleOpen = true;
            typeof(ReflectionUniWebViewAdapter).GetMethod("OnPageFinished", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(adapter, new object[] { view, 200, "https://example.com/" });
            Assert.AreEqual(0, view.Shows);
            WebWrapperDiagnostics.ConsoleOpen = false;
            Tick();
            Assert.AreEqual(1, view.Shows);
            Assert.AreEqual(0, view.Loads);
        }

        [TestCase("mobilenotes://")]
        [TestCase("maps://?q=Apple Park")]
        [TestCase("googlechrome://")]
        [TestCase("itms-beta://")]
        public void MissingExternalApp_PreservesVisiblePageAndCompletesPendingLoading(string url)
        {
            int failures = 0, completed = 0;
            adapter.LoadFailed += _ => failures++;
            adapter.LoadCompleted += () => completed++;
            Set("_loading", true);
            Error(-10, "net::ERR_UNKNOWN_URL_SCHEME", url);
            Assert.AreEqual(0, failures);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(0, view.Hides);
            Assert.AreEqual(0, view.Loads);
            Assert.AreEqual(false, Get("_loading"));
            Assert.AreEqual(true, Get("_nativeVisible"));
            Call("OnPageStarted", view, "https://example.com/next");
            Call("OnPageFinished", view, 200, "https://example.com/next");
            Assert.AreEqual(2, completed);
        }

        [Test]
        public void ExternalFailureWhileConsoleOpen_KeepsConsoleAndDoesNotReloadPage()
        {
            WebWrapperDiagnostics.ConsoleOpen = true;
            Error(-1002, "unsupported URL", "itms-beta://");
            Assert.AreEqual(0, view.Shows);
            WebWrapperDiagnostics.ConsoleOpen = false;
            Tick();
            Assert.AreEqual(1, view.Shows);
            Assert.AreEqual(0, view.Loads);
        }

        [Test]
        public void ExternalSchemeWithoutPayload_PreservesVisiblePage()
        {
            int failures = 0;
            adapter.LoadFailed += _ => failures++;
            Call("OnLoadingErrorReceived", view, -10, "net::ERR_UNKNOWN_URL_SCHEME", null);
            Assert.AreEqual(0, failures);
            Assert.AreEqual(0, view.Hides);
        }

        [TestCase(-10, "itms-beta://", false)]
        [TestCase(-2, "https://example.com/", true)]
        [TestCase(-11, "https://example.com/", true)]
        public void InitialPageOrNetworkFailure_StillReportsFailure(int code, string url, bool visible)
        {
            int failures = 0;
            adapter.LoadFailed += _ => failures++;
            Set("_nativeVisible", visible);
            Error(code, "failed", url);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(1, view.Hides);
        }

        [Test]
        public void HiddenRoute_IgnoresExternalAppCallback()
        {
            Set("_showRequested", false);
            Error(-10, "unsupported", "googlechrome://");
            Assert.AreEqual(0, view.Shows);
        }

        private sealed class NativeError
        {
            public Dictionary<string, object> Extra { get; } = new Dictionary<string, object>();
        }
        private void Error(int code, string message, string url)
        {
            var payload = new NativeError();
            payload.Extra["failingURL"] = url;
            Call("OnLoadingErrorReceived", view, code, message, payload);
        }
        private void Call(string name, params object[] args) => typeof(ReflectionUniWebViewAdapter)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, args);
        private object Get(string name) => typeof(ReflectionUniWebViewAdapter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);

        private void Tick() => typeof(ReflectionUniWebViewAdapter)
            .GetMethod("UpdateDiagnosticsVisibility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
        private void Set(string field, object value) => typeof(ReflectionUniWebViewAdapter)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, value);
    }
}
