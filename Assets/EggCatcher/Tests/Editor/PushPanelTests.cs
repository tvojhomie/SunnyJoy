using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AlienProject.WebWrapper;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EggCatcher.Tests
{
    public sealed class PushPanelTests
    {
        private const string Slot = "PushPanelTest";
        private WebWrapperSettings testSettings;

        [UnityTest]
        public IEnumerator GameRoute_WaitsForProfileAndMenu_BeforeShowingPrompt()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Object.FindFirstObjectByType<EggCatcherApp>().enabled = false;
            GameDataStore.Reset(Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            WebWrapperBootstrap.SetEditorPreview(false);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            Assert.That(Object.FindFirstObjectByType<WebWrapperController>(), Is.Null, "UI tests must bypass automatic network startup.");

            var controller = new GameObject("Game push UI test").AddComponent<WebWrapperController>();
            controller.enabled = false;
            testSettings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            Set(testSettings, "useMockServices", false);
            var permission = new Permission();
            Set(controller, "_settings", testSettings);
            Set(controller, "_notificationFlow", new NotificationFlowCoordinator(new Store(), permission, new Clock(), 3));
            var decision = LaunchDecision.ShowWrapper("UI test game");
            Set(controller, "_decision", decision);
            Call(controller, "ApplyDecision", decision);
            app.enabled = true;
            var deadline = Time.realtimeSinceStartup + 3;
            while (!app.transform.Find("CreateProfilePanel").gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(1);
            Assert.That(app.transform.Find("CreateProfilePanel").gameObject.activeInHierarchy, Is.True,
                "Expected profile after game decision. Decision=" + WebWrapperLaunchState.CurrentDecision?.Kind +
                "; enabled=" + app.enabled + "; menu=" + app.transform.Find("MainMenu").gameObject.activeSelf +
                "; loading=" + app.transform.Find("LoadingPanel").gameObject.activeSelf);
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.False);
            Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.False);

            var profile = app.transform.Find("CreateProfilePanel/SafeAreaContent");
            profile.Find("Panel/InputField (Legacy)").GetComponent<InputField>().text = "Speckles";
            profile.Find("Panel/Avatars/1").GetComponent<Button>().onClick.Invoke();
            profile.Find("StartBtn").GetComponent<Button>().onClick.Invoke();
            Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.True);
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.False);
            deadline = Time.realtimeSinceStartup + 3;
            while (!WebWrapperLaunchState.IsGameReady && Time.realtimeSinceStartup < deadline) yield return null;
            Call(controller, "UpdatePendingNotificationPrompt");
            Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.True);
            Assert.That(app.transform.Find("LoadingPanel").gameObject.activeInHierarchy, Is.False);
            Assert.That(permission.Requests, Is.Zero);
            app.transform.Find("PushPanel/SafeAreaContent/Skip").GetComponent<Button>().onClick.Invoke();
            Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.False);
            Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.True);
            Assert.That(permission.Requests, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuthoredPanel_RegistersAndSurvivesScreenChanges_AndDebouncesButtons()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            yield return new WaitForSecondsRealtime(.9f);

            {
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.enabled, Is.True);
            var view = app.GetComponent<EggCatcherPushPanel>();
            Assert.That(view, Is.Not.Null, "App Awake must attach its notification presenter.");
            var panel = app.transform.Find("PushPanel");
            var yes = panel.Find("SafeAreaContent/Yes").GetComponent<Button>();
            var skip = panel.Find("SafeAreaContent/Skip").GetComponent<Button>();
            Assert.That(WebWrapperServices.NotificationPromptView, Is.SameAs(view));
            Assert.That(panel.gameObject.activeSelf, Is.False, "Game-only startup must not ask for push permission.");
            var allows = 0;
            var skips = 0;
            view.Show(() => allows++, () => skips++);
            var showScreen = typeof(EggCatcherApp).GetMethod("ShowScreen", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showScreen, Is.Not.Null);
            showScreen.Invoke(app, new object[] { "MainMenu" });
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.GetSiblingIndex(), Is.EqualTo(app.transform.childCount - 1));
            yes.onClick.Invoke();
            yes.onClick.Invoke();
            skip.onClick.Invoke();
            Assert.That(allows, Is.EqualTo(1));
            Assert.That(skips, Is.Zero);
            Assert.That(yes.interactable || skip.interactable, Is.False);
            Assert.That(yes.GetComponentInChildren<Text>().text, Is.EqualTo("Please wait..."));
            view.Hide();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            view.Show(() => allows++, () => skips++);
            skip.onClick.Invoke();
            skip.onClick.Invoke();
            yes.onClick.Invoke();
            Assert.That(skips, Is.EqualTo(1));
            Assert.That(allows, Is.EqualTo(1));
            view.enabled = false;
            Assert.That(WebWrapperServices.NotificationPromptView, Is.Null);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testSettings != null) Object.DestroyImmediate(testSettings);
            WebWrapperBootstrap.SetEditorPreview(false);
            GameDataStore.Reset(Slot);
            SessionState.EraseString("EggCatcher.PreviewSlot");
        }

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private sealed class Store : IInstallStateStore
        {
            private InstallState state = new InstallState();
            public InstallState Load() => state;
            public void Save(InstallState value) => state = value;
            public void Clear() => state = new InstallState();
        }
        private sealed class Clock : IClock { public long UtcNowUnixSeconds => 100; }
        private sealed class Permission : INotificationPermissionProvider
        {
            public int Requests;
            public NotificationPermissionStatus Status => NotificationPermissionStatus.Unknown;
            public Task<NotificationPermissionStatus> RequestAsync(CancellationToken token)
            { Requests++; return Task.FromResult(NotificationPermissionStatus.Granted); }
        }
    }
}
