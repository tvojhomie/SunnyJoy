using System;
using System.Linq;
using System.Reflection;
using AlienProject.WebWrapper.Integrations.UniWebView;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class WebViewNavigationPolicyTests
    {
        [Test]
        public void InstalledUniWebView_ReflectionBindingsMatchSdkContract()
        {
            var sdk = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(value => value.GetName().Name == "UniWebView-CSharp");
            if (sdk == null)
            {
                Assert.Ignore("The optional UniWebView SDK is not installed.");
            }

            var webView = sdk.GetType("UniWebView", true);
            foreach (var eventName in new[] { "OnPageStarted", "OnPageFinished", "OnLoadingErrorReceived", "OnShouldClose" })
            {
                AssertCallbackCompatible(webView.GetEvent(eventName)?.EventHandlerType, eventName);
            }

            var toolbarRegister = webView.GetMethod("RegisterOnEmbeddedToolbarItemAction");
            Assert.That(toolbarRegister, Is.Not.Null);
            AssertCallbackCompatible(toolbarRegister.GetParameters()[0].ParameterType, "OnToolbarAction");

            var manager = sdk.GetType("UniWebViewChannelMethodManager", true);
            Assert.That(manager.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static), Is.Not.Null);
            var register = manager.GetMethod("RegisterChannelMethod", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(register, Is.Not.Null);
            Assert.That(register.GetParameters().Select(value => value.ParameterType).ToArray(),
                Is.EqualTo(new[] { typeof(string), sdk.GetType("UniWebViewChannelMethod", true), typeof(Func<object, object>) }));
            Assert.That(Enum.IsDefined(sdk.GetType("UniWebViewChannelMethod", true), "RequestMediaCapturePermission"), Is.True);
            Assert.That(Enum.IsDefined(sdk.GetType("UniWebViewMediaCapturePermissionDecision", true), "Grant"), Is.True);
            Assert.That(Enum.IsDefined(sdk.GetType("UniWebViewMediaCapturePermissionDecision", true), "Prompt"), Is.True);
            var listener = webView.GetField("listener", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(listener, Is.Not.Null);
            Assert.That(listener.FieldType.GetProperty("Name")?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(sdk.GetType("UniWebViewChannelMethodMediaCapturePermission", true).GetProperty("Resources")?.PropertyType,
                Is.EqualTo(typeof(string[])));
        }

        private static void AssertCallbackCompatible(Type delegateType, string handlerName)
        {
            Assert.That(delegateType, Is.Not.Null, handlerName);
            var invoke = delegateType.GetMethod("Invoke");
            var handler = typeof(ReflectionUniWebViewAdapter).GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(handler, Is.Not.Null, handlerName);
            Assert.That(invoke.ReturnType, Is.EqualTo(handler.ReturnType), handlerName);
            var sdkParameters = invoke.GetParameters();
            var adapterParameters = handler.GetParameters();
            Assert.That(sdkParameters.Length, Is.EqualTo(adapterParameters.Length), handlerName);
            for (var index = 0; index < sdkParameters.Length; index++)
            {
                Assert.That(adapterParameters[index].ParameterType.IsAssignableFrom(sdkParameters[index].ParameterType),
                    Is.True, handlerName + " parameter " + index);
            }
        }

        [Test]
        public void AndroidUserAgent_RemovesOnlyWebViewMarkersAndPreservesDeviceVersions()
        {
            const string native = "Mozilla/5.0 (Linux; Android 14; Pixel 8 Build/UQ1A.240205.004; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/121.0.6167.178 Mobile Safari/537.36";
            const string expected = "Mozilla/5.0 (Linux; Android 14; Pixel 8 Build/UQ1A.240205.004) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.6167.178 Mobile Safari/537.36";

            Assert.That(WebViewNavigationPolicy.NormalizeUserAgent(native), Is.EqualTo(expected));
        }

        [TestCase(-10, "net::ERR_UNKNOWN_URL_SCHEME", "mobilenotes://")]
        [TestCase(-10, "net::ERR_UNKNOWN_URL_SCHEME", "maps://?q=Apple Park")]
        [TestCase(-1002, "unsupported URL", "googlechrome://")]
        [TestCase(-1002, "unsupported URL", "itms-beta://")]
        [TestCase(-999, "cancelled", "tel:+123456")]
        [TestCase(-1, "unavailable", "intent://maps/#Intent;scheme=geo;end")]
        [TestCase(-10, null, null)]
        [TestCase(-1002, null, "")]
        [TestCase(-1, "net::ERR_UNKNOWN_URL_SCHEME", null)]
        public void ExternalAppFailure_IsNotAnHttpPageFailure(int code, string message, string url)
        {
            Assert.IsTrue(WebViewNavigationPolicy.IsExternalNavigationError(code, message, url));
        }

        [TestCase(-2, "ERR_NAME_NOT_RESOLVED", "https://example.com/")]
        [TestCase(-10, "net::ERR_UNKNOWN_URL_SCHEME", "https://example.com/")]
        [TestCase(-11, "SSL error", null)]
        [TestCase(-1002, "unsupported", "file:///private/local")]
        [TestCase(-1002, "unsupported", "javascript:alert(1)")]
        [TestCase(-1002, "unsupported", "about:blank")]
        [TestCase(-1002, "unsupported", "data:text/html,hello")]
        [TestCase(-1, "unknown", null)]
        public void RealLoadingFailures_KeepNormalErrorHandling(int code, string message, string url)
        {
            Assert.IsFalse(WebViewNavigationPolicy.IsExternalNavigationError(code, message, url));
        }

        [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 18_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148")]
        [TestCase("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/121.0.6167.178 Mobile Safari/537.36")]
        [TestCase(null)]
        [TestCase("")]
        public void NativeUserAgentWithoutMarkers_IsNotFabricatedOrRewritten(string native)
        {
            Assert.That(WebViewNavigationPolicy.NormalizeUserAgent(native), Is.EqualTo(native));
        }

        [Test]
        public void ProtectedMediaOnly_IsGranted()
        {
            Assert.That(WebViewNavigationPolicy.ShouldAutoGrantMedia(new[] { "PROTECTED_MEDIA_ID" }), Is.True);
            Assert.That(WebViewNavigationPolicy.ShouldAutoGrantMedia(new[] { "android.webkit.resource.PROTECTED_MEDIA_ID" }), Is.True);
        }

        [TestCase("VIDEO")]
        [TestCase("AUDIO")]
        [TestCase("MIDI_SYSEX")]
        [TestCase("UNKNOWN")]
        [TestCase(null)]
        public void MixedProtectedMediaAndOtherPermission_StillPrompts(string otherResource)
        {
            Assert.That(WebViewNavigationPolicy.ShouldAutoGrantMedia(new[] { "PROTECTED_MEDIA_ID", otherResource }), Is.False);
        }

        [Test]
        public void MissingMediaResources_StillPrompt()
        {
            Assert.That(WebViewNavigationPolicy.ShouldAutoGrantMedia(null), Is.False);
            Assert.That(WebViewNavigationPolicy.ShouldAutoGrantMedia(new string[0]), Is.False);
        }

        [TestCase(-9, "redirect loop")]
        [TestCase(-1007, "too many HTTP redirects")]
        [TestCase(-1, "net::ERR_TOO_MANY_REDIRECTS")]
        public void RedirectLimit_RestartsAtActualFailingUrlOnlyOnce(int code, string message)
        {
            var recovery = new WebViewRedirectRecovery();
            Assert.That(recovery.TryGetRetryUrl(code, message, "https://final.example/page", "https://tracking.example/start", out var retry), Is.True);
            Assert.That(retry, Is.EqualTo("https://final.example/page"));
            Assert.That(recovery.TryGetRetryUrl(code, message, "https://next.example/loop", "https://tracking.example/start", out _), Is.False);
        }

        [Test]
        public void RedirectWithoutNativeFailingUrl_UsesLastStartedUrl()
        {
            var recovery = new WebViewRedirectRecovery();
            Assert.That(recovery.TryGetRetryUrl(-9, null, null, "https://last.example/page", out var retry), Is.True);
            Assert.That(retry, Is.EqualTo("https://last.example/page"));
        }

        [TestCase("file:///private/local")]
        [TestCase("javascript:alert(1)")]
        [TestCase("intent://somewhere")]
        public void RedirectRecovery_DoesNotLoadUnsafeNativeUrl(string failingUrl)
        {
            var recovery = new WebViewRedirectRecovery();
            Assert.That(recovery.TryGetRetryUrl(-9, null, failingUrl, "https://last.example/page", out _), Is.False);
        }

        [Test]
        public void NetworkError_DoesNotConsumeRedirectRetry()
        {
            var recovery = new WebViewRedirectRecovery();
            Assert.That(recovery.TryGetRetryUrl(-2, "ERR_NAME_NOT_RESOLVED", "https://missing.example", null, out _), Is.False);
            Assert.That(recovery.TryGetRetryUrl(-9, null, "https://final.example", null, out _), Is.True);
        }

        [Test]
        public void NewTopLevelNavigation_CanRecoverAgainAfterReset()
        {
            var recovery = new WebViewRedirectRecovery();
            Assert.That(recovery.TryGetRetryUrl(-9, null, "https://final.example", null, out _), Is.True);
            recovery.Reset();
            Assert.That(recovery.TryGetRetryUrl(-9, null, "https://other.example", null, out _), Is.True);
        }
    }
}
