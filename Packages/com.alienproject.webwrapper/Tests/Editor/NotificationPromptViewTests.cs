using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class NotificationPromptViewTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private WebWrapperController controller;
        private WebWrapperSettings settings;
        private MemoryStore store;
        private Clock clock;
        private Permission permission;
        private View view;
        private Web web;
        private NotificationFlowCoordinator flow;
        private INotificationPromptView previousView;
        private IWebViewAdapter previousWeb;

        [SetUp]
        public void SetUp()
        {
            previousView = WebWrapperServices.NotificationPromptView;
            previousWeb = WebWrapperServices.WebViewAdapter;
            controller = new GameObject("Push prompt test").AddComponent<WebWrapperController>();
            settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            Set(settings, "useMockServices", false);
            store = new MemoryStore();
            clock = new Clock();
            permission = new Permission();
            view = new View();
            web = new Web();
            flow = new NotificationFlowCoordinator(store, permission, clock, 3);
            Set(controller, "_settings", settings);
            Set(controller, "_notificationFlow", flow);
            Set(controller, "_cancellation", new CancellationTokenSource());
            Set(controller, "_webView", web);
            WebWrapperServices.WebViewAdapter = web;
            WebWrapperServices.NotificationPromptView = view;
            Apply(LaunchDecision.ShowWrapper("Test game"));
            WebWrapperLaunchState.NotifyGameReady();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller != null)
            {
                Call(controller, "OnDestroy");
                Object.DestroyImmediate(controller.gameObject);
            }
            Object.DestroyImmediate(settings);
            WebWrapperServices.NotificationPromptView = previousView;
            WebWrapperServices.WebViewAdapter = previousWeb;
        }

        [Test]
        public void PolicyUrl_DoesNotInjectLegacyWebViewPrivacyButton()
        {
            Set(settings, "privacyPolicyUrl", "https://example.com/privacy");
            Apply(LaunchDecision.ShowWebView("https://example.com/", "Test web"));
            Assert.That(web.Shows, Is.EqualTo(1));
            Assert.That(web.PrivacyConfigurations, Is.Zero);
            Assert.That(settings.PrivacyPolicyUrl, Is.EqualTo("https://example.com/privacy"));
        }

        [Test]
        public void Skip_DoesNotRequestSystemPermission_AndDefersForThreeDays()
        {
            Assert.That(Show(), Is.True);
            Assert.That(view.Visible, Is.True);
            Assert.That(permission.Requests, Is.Zero);
            var staleAllow = view.Allow;
            var staleSkip = view.Skip;
            view.Skip();
            Assert.That(web.Shows, Is.Zero);
            Assert.That(view.Visible, Is.False);
            Assert.That(store.State.NotificationPromptDeferredUntilUnixSeconds, Is.EqualTo(100 + 3 * 86400));
            staleAllow();
            staleSkip();
            Assert.That(permission.Requests, Is.Zero);
            Assert.That(web.Shows, Is.Zero);
            Assert.That(Get(controller, "_showOverlay"), Is.False);
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.True);
            Assert.That(Show(), Is.False);
            clock.UtcNowUnixSeconds += 3 * 86400;
            Assert.That(Show(), Is.True);
        }

        [TestCase(NotificationPermissionStatus.Granted)]
        [TestCase(NotificationPermissionStatus.DeniedPermanently)]
        public async Task Allow_WaitsOnceForSystemResponse_ThenReturnsToOpenGame(NotificationPermissionStatus result)
        {
            Assert.That(Show(), Is.True);
            view.Allow();
            view.Allow();
            view.Skip();
            Assert.That(permission.Requests, Is.EqualTo(1));
            Assert.That(view.Busy, Is.True);
            Assert.That(web.Shows, Is.Zero);
            Assert.That(store.State.NotificationPromptDeferredUntilUnixSeconds, Is.Zero);
            permission.Complete(result);
            await Task.Yield();
            Assert.That(view.Visible, Is.False);
            Assert.That(web.Shows, Is.Zero);
            Assert.That(Get(controller, "_showOverlay"), Is.False);
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.True);
            Assert.That(store.State.NotificationSystemPermissionDenied,
                Is.EqualTo(result != NotificationPermissionStatus.Granted));
            Assert.That(Show(), Is.False);
        }

        [Test]
        public void WebDecision_OpensImmediately_WithoutPromptOrPermission()
        {
            Apply(LaunchDecision.ShowWebView("https://example.com/", "Test web"));
            WebWrapperLaunchState.NotifyGameReady();
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(Show(), Is.False);
            Assert.That(view.Visible, Is.False);
            Assert.That(permission.Requests, Is.Zero);
            Assert.That(web.Shows, Is.EqualTo(1));
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.False);
        }

        [Test]
        public void GameDecision_WaitsForVisibleMenu_ThenShowsOnlyOnce()
        {
            typeof(WebWrapperLaunchState).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Apply(LaunchDecision.ShowWrapper("Game loading"));
            Assert.That(WebWrapperLaunchState.CanStartGame(true), Is.True, "The prompt must not block game initialization.");
            Assert.That(Show(), Is.False);
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(view.Visible, Is.False);
            WebWrapperLaunchState.NotifyGameReady();
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(view.Visible, Is.True);
            Assert.That(permission.Requests, Is.Zero, "Only pressing Yes can request system permission.");
            view.Skip();
            clock.UtcNowUnixSeconds += 3 * 86400;
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(view.Visible, Is.False, "No repeated prompt in the same game session.");
            Assert.That(web.Shows, Is.Zero);
        }

        [Test]
        public void StartupOverlay_BlocksPrompt_ButGameFallbackCanPromptAfterMenuWasReady()
        {
            Set(controller, "_running", true);
            Assert.That(Show(), Is.False);
            Set(controller, "_running", false);
            Set(controller, "_showOverlay", true);
            Assert.That(Show(), Is.False);
            Apply(LaunchDecision.ConfigurationError("Test error"));
            Assert.That(WebWrapperLaunchState.CurrentDecision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(Show(), Is.True, "Fallback has returned to the already visible game menu.");
            Assert.That(permission.Requests, Is.Zero);
        }

        [Test]
        public void WebRoute_DismissesGamePrompt_AndIgnoresStaleButtons()
        {
            Assert.That(Show(), Is.True);
            var staleAllow = view.Allow;
            var staleSkip = view.Skip;
            Apply(LaunchDecision.ShowWebView("https://example.com/", "Push link"));
            staleAllow();
            staleSkip();
            Assert.That(view.Visible, Is.False);
            Assert.That(permission.Requests, Is.Zero);
            Assert.That(web.Shows, Is.EqualTo(1));
            Assert.That(Show(), Is.False);
        }

        [Test]
        public async Task PermissionCompletedAfterWebRoute_DoesNotReopenOrReloadAnything()
        {
            Assert.That(Show(), Is.True);
            view.Allow();
            Apply(LaunchDecision.ShowWebView("https://example.com/", "Push link"));
            permission.Complete(NotificationPermissionStatus.Granted);
            await Task.Yield();
            Assert.That(view.Visible, Is.False);
            Assert.That(web.Shows, Is.EqualTo(1));
            Assert.That(WebWrapperLaunchState.CurrentDecision.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
        }

        [TestCase(NotificationPermissionStatus.Granted)]
        [TestCase(NotificationPermissionStatus.DeniedPermanently)]
        public void ExistingSystemChoice_DoesNotShowPrompt(NotificationPermissionStatus status)
        {
            permission.Status = status;
            Assert.That(Show(), Is.False);
        }

        [Test]
        public void LateView_AndDestroyedView_KeepFallbackAvailable()
        {
            WebWrapperServices.NotificationPromptView = null;
            Assert.That(Show(), Is.True);
            Assert.That(Get(controller, "_notificationPromptView"), Is.Null);
            WebWrapperServices.NotificationPromptView = view;
            Call(controller, "UpdateNotificationPromptView");
            Assert.That(view.Visible, Is.True);
            view.IsAvailable = false;
            Call(controller, "UpdateNotificationPromptView");
            Assert.That(view.Visible, Is.False);
            Assert.That(Get(controller, "_notificationPromptView"), Is.Null);
            Call(controller, "SkipNotificationPermission");
            Assert.That(web.Shows, Is.Zero);
        }

        [Test]
        public async Task FailedPermissionRequest_ReturnsToGameWithoutOpeningWebView()
        {
            Assert.That(Show(), Is.True);
            view.Allow();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                "[WebWrapper] Notification permission request failed: Test failure");
            permission.Fail();
            await Task.Yield();
            Assert.That(view.Visible, Is.False);
            Assert.That(web.Shows, Is.Zero);
            Assert.That(Get(controller, "_showOverlay"), Is.False);
        }

        [Test]
        public async Task DestroyDuringPermissionRequest_DoesNotOpenWebView()
        {
            Assert.That(Show(), Is.True);
            view.Allow();
            Call(controller, "OnDestroy");
            Object.DestroyImmediate(controller.gameObject);
            permission.Complete(NotificationPermissionStatus.Granted);
            await Task.Yield();
            Assert.That(view.Visible, Is.False);
            Assert.That(web.Shows, Is.Zero);
        }

        private bool Show() => (bool)Call(controller, "TryShowNotificationPrompt");
        private void Apply(LaunchDecision decision)
        {
            Set(controller, "_decision", decision);
            Call(controller, "ApplyDecision", decision);
        }
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

        private sealed class MemoryStore : IInstallStateStore
        {
            public InstallState State = new InstallState();
            public InstallState Load() => State;
            public void Save(InstallState state) => State = state;
            public void Clear() => State = new InstallState();
        }
        private sealed class Clock : IClock { public long UtcNowUnixSeconds { get; set; } = 100; }
        private sealed class Permission : INotificationPermissionProvider
        {
            public NotificationPermissionStatus Status { get; set; }
            public int Requests;
            private readonly TaskCompletionSource<NotificationPermissionStatus> response = new TaskCompletionSource<NotificationPermissionStatus>();
            public Task<NotificationPermissionStatus> RequestAsync(CancellationToken cancellationToken) { Requests++; return response.Task; }
            public void Complete(NotificationPermissionStatus status) { Status = status; response.SetResult(status); }
            public void Fail() => response.SetException(new InvalidOperationException("Test failure"));
        }
        private sealed class View : INotificationPromptView
        {
            public bool IsAvailable { get; set; } = true;
            public bool Visible, Busy;
            public Action Allow, Skip;
            public void Show(Action allow, Action skip) { Visible = true; Allow = allow; Skip = skip; }
            public void Hide() { Visible = false; }
            public void SetBusy(bool busy) { Busy = busy; }
        }
        private sealed class Web : IWebViewAdapter, IWebViewPrivacyControls
        {
            public bool IsAvailable => true;
            public bool CanGoBack => false;
            public int Shows;
            public int PrivacyConfigurations;
            public void ConfigurePrivacy(string url, string title) { PrivacyConfigurations++; }
            public void Show(string url) { Shows++; }
            public void Hide() { }
            public void GoBack() { }
        }
    }
}
