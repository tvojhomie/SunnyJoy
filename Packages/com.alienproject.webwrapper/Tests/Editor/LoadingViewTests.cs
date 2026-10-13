using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class LoadingViewTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private WebWrapperController controller;
        private WebWrapperSettings settings;
        private ILoadingView previous;
        private View view;

        [SetUp]
        public void SetUp()
        {
            previous = WebWrapperServices.LoadingView;
            controller = new GameObject("Loading view test").AddComponent<WebWrapperController>();
            settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            Set(settings, "useMockServices", false);
            Set(controller, "_settings", settings);
            Set(controller, "_running", true);
            view = new View();
            WebWrapperServices.LoadingView = view;
        }

        [TearDown]
        public void TearDown()
        {
            Call("OnDestroy");
            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(settings);
            WebWrapperServices.LoadingView = previous;
        }

        [Test]
        public void ServerAndPageLoading_UseHostViewWithoutRestartingAnimation()
        {
            Assert.That(UpdateView(), Is.True);
            Assert.That(UpdateView(), Is.True);
            Set(controller, "_running", false);
            Call("OnWebViewLoadStarted");
            Assert.That(UpdateView(), Is.True);
            Assert.That(view.Shows, Is.EqualTo(1));
            Call("OnWebViewLoadCompleted");
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Error_OpensGameAndReleasesLoadingWithoutErrorOverlay()
        {
            UpdateView();
            Set(controller, "_running", false);
            controller.GetType().GetMethod("OnWebViewLoadFailed", Private).Invoke(controller, new object[] { "Test error" });
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
            Assert.That(Get("_showOverlay"), Is.False);
            Assert.That(WebWrapperLaunchState.CurrentDecision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(WebWrapperLaunchState.CanStartGame(true), Is.True);
        }

        [Test]
        public void PushPrompt_HidesLoading_AndPageLoadingRestoresIt()
        {
            UpdateView();
            Set(controller, "_showNotificationPrompt", true);
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
            Set(controller, "_showNotificationPrompt", false);
            Assert.That(UpdateView(), Is.True);
        }

        [Test]
        public void GameRoute_ReleasesHostLoading()
        {
            UpdateView();
            Set(controller, "_showOverlay", false);
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void LateRegistrationAndUnavailableView_RestoreFallback()
        {
            WebWrapperServices.LoadingView = null;
            Assert.That(UpdateView(), Is.False);
            WebWrapperServices.LoadingView = view;
            Assert.That(UpdateView(), Is.True);
            view.IsAvailable = false;
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
            view.IsAvailable = true;
            Assert.That(UpdateView(), Is.True);
        }

        [Test]
        public void ReplacedView_ReleasesPreviousPresenter()
        {
            UpdateView();
            var replacement = new View();
            WebWrapperServices.LoadingView = replacement;
            Assert.That(UpdateView(), Is.True);
            Assert.That(view.Visible, Is.False);
            Assert.That(replacement.Visible, Is.True);
        }

        [Test]
        public void MockHarness_KeepsDiagnosticsInsteadOfHostLoader()
        {
            Set(settings, "useMockServices", true);
            Assert.That(UpdateView(), Is.False);
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Destroy_ReleasesHostLoading()
        {
            UpdateView();
            Call("OnDestroy");
            Assert.That(view.Visible, Is.False);
        }

        private bool UpdateView() => (bool)Call("UpdateLoadingView");
        private object Call(string name) => controller.GetType().GetMethod(name, Private).Invoke(controller, null);
        private object Get(string name) => controller.GetType().GetField(name, Private).GetValue(controller);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private sealed class View : ILoadingView
        {
            public bool IsAvailable { get; set; } = true;
            public bool Visible;
            public int Shows;
            public void Show() { Shows++; Visible = true; }
            public void Hide() { Visible = false; }
        }
    }
}
