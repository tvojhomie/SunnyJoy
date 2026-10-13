using System.Collections;
using System.Reflection;
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
    public sealed class LaunchGateTests
    {
        private const string Slot = "LaunchGateTest";
        private string serverProfileSlot;
        private string installationKey;
        private string previousInstallationId;
        private bool hadInstallationId;
        private WebWrapperSettings disabledWebSettings;
        private IWebViewAdapter previousWebView;
        private bool replacedWebView;

        [UnityTest]
        public IEnumerator EditorServerSession_DisabledWebView_NewProfileLeavesLoading() =>
            DisabledWebViewStartsGame(false);

        [UnityTest]
        public IEnumerator EditorServerSession_DisabledWebView_SavedProfileOpensMenu() =>
            DisabledWebViewStartsGame(true);

        private IEnumerator DisabledWebViewStartsGame(bool hasProfile)
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Object.FindFirstObjectByType<EggCatcherApp>().enabled = false;
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            WebWrapperBootstrap.SetEditorPreview(false);
            installationKey = "AlienProject.WebWrapper.EditorLive.LastInstall." + WebWrapperEditorLiveSession.BundleId;
            hadInstallationId = PlayerPrefs.HasKey(installationKey);
            previousInstallationId = PlayerPrefs.GetString(installationKey, string.Empty);
            PlayerPrefs.SetString(installationKey, "unity-editor-" + System.Guid.NewGuid().ToString("N"));
            SessionState.SetBool("AlienProject.WebWrapper.EditorLive.Enabled", true);
            serverProfileSlot = "ServerTest_" + WebWrapperEditorLiveSession.InstallationId;
            GameDataStore.Save(new EggCatcherData { Name = hasProfile ? "Local player" : "",
                HasProfile = hasProfile, MusicEnabled = false }, serverProfileSlot);

            var controller = new GameObject("Disabled WebView startup test").AddComponent<WebWrapperController>();
            controller.enabled = false;
            disabledWebSettings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            typeof(WebWrapperSettings).GetField("webViewEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(disabledWebSettings, false);
            typeof(WebWrapperController).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, disabledWebSettings);
            LogAssert.Expect(LogType.Log, "[WebWrapper.EditorLive] Applying route: Wrapper.");
            var startup = (System.Threading.Tasks.Task)typeof(WebWrapperController)
                .GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            while (!startup.IsCompleted) yield return null;
            startup.GetAwaiter().GetResult();
            Assert.That(WebWrapperLaunchState.CurrentDecision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(WebWrapperLaunchState.CurrentDecision.Reason, Does.Contain("disabled in WebWrapper App"));

            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            app.enabled = true;
            var destination = app.transform.Find(hasProfile ? "MainMenu" : "CreateProfilePanel");
            float deadline = Time.realtimeSinceStartup + 4;
            while (!destination.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(destination.gameObject.activeInHierarchy, Is.True);
            Assert.That(app.transform.Find("LoadingPanel").gameObject.activeSelf, Is.False);
            Assert.That(app.LocalPreparationProgress, Is.EqualTo(1));
            Assert.That(typeof(EggCatcherApp).GetField("profileSlot", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(app), Is.EqualTo(serverProfileSlot));
            Assert.That(app.Profile.HasProfile, Is.EqualTo(hasProfile));
            Assert.That(controller.HasVisibleWebView, Is.False);
            ScreenCapture.CaptureScreenshot(hasProfile ? "Library/EggCatcherDisabledWebMenu.png" :
                "Library/EggCatcherDisabledWebProfile.png");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SlowServer_WaitsForLateWebDecision_AndSlowPage() => SlowServerStartsSelectedRoute(true);

        [UnityTest]
        public IEnumerator SlowServer_WaitsForExplicitGameDecision() => SlowServerStartsSelectedRoute(false);

        private IEnumerator SlowServerStartsSelectedRoute(bool openWeb)
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Object.FindFirstObjectByType<EggCatcherApp>().enabled = false;
            GameDataStore.Save(new EggCatcherData { Name = "Slow server", HasProfile = true, MusicEnabled = true }, Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            WebWrapperBootstrap.SetEditorPreview(false);
            var controller = new GameObject("Slow server test").AddComponent<WebWrapperController>();
            controller.enabled = false;
            disabledWebSettings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            SetController(controller, "_settings", disabledWebSettings);
            SetController(controller, "_running", true);
            var web = new DeferredWebView();
            previousWebView = WebWrapperServices.WebViewAdapter;
            replacedWebView = true;
            WebWrapperServices.WebViewAdapter = web;
            SetController(controller, "_webView", web);
            var client = new DeferredConfigClient();
            var pending = new FirebaseDatabaseLaunchCoordinator(new OnlineConnectivity(), client)
                .ResolveAsync(System.Threading.CancellationToken.None);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            float started = Time.realtimeSinceStartup;
            app.enabled = true;
            while (Time.realtimeSinceStartup - started < 11f)
            {
                yield return null;
                CallController(controller, "LateUpdate");
                AssertBlocked(app);
                Assert.That(pending.IsCompleted, Is.False);
            }
            Assert.That(app.LocalPreparationProgress, Is.EqualTo(1));
            Assert.That(WebWrapperLaunchState.CurrentDecision, Is.Null);
            client.Response.SetResult(openWeb ? ConfigResult.Success("https://example.com/late", 0) :
                ConfigResult.Negative("Server selected the game"));
            while (!pending.IsCompleted) yield return null;
            CallController(controller, "ApplyDecision", pending.GetAwaiter().GetResult());
            SetController(controller, "_running", false);
            yield return null;
            if (openWeb)
            {
                Assert.That(web.Url, Is.EqualTo("https://example.com/late"));
                started = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - started < 11f)
                {
                    CallController(controller, "Update");
                    CallController(controller, "LateUpdate");
                    AssertBlocked(app);
                    Assert.That(WebWrapperLaunchState.CurrentDecision.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
                    Assert.That(controller.HasVisibleWebView, Is.False);
                    yield return null;
                }
                web.Complete();
                CallController(controller, "LateUpdate");
                yield return null;
                Assert.That(controller.HasVisibleWebView, Is.True);
                Assert.That(app.transform.Find("LoadingPanel").gameObject.activeSelf, Is.False);
                Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.False);
                Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.False);
                foreach (var source in app.GetComponents<AudioSource>()) Assert.That(source.isPlaying, Is.False);
            }
            else
            {
                CallController(controller, "LateUpdate");
                float deadline = Time.realtimeSinceStartup + 3f;
                while (!app.transform.Find("MainMenu").gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.True);
                Assert.That(app.transform.Find("LoadingPanel").gameObject.activeSelf, Is.False);
                Assert.That(web.Url, Is.Null);
            }
            Object.Destroy(controller.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetController(WebWrapperController controller, string field, object value) =>
            typeof(WebWrapperController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

        private static object CallController(WebWrapperController controller, string method, params object[] args) =>
            typeof(WebWrapperController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);

        private sealed class OnlineConnectivity : IConnectivityService
        {
            public bool IsInternetReachable => true;
        }

        private sealed class DeferredConfigClient : IConfigClient
        {
            public readonly System.Threading.Tasks.TaskCompletionSource<ConfigResult> Response =
                new System.Threading.Tasks.TaskCompletionSource<ConfigResult>();
            public System.Threading.Tasks.Task<ConfigResult> FetchAsync(ConfigRequest request,
                System.Threading.CancellationToken cancellationToken) => Response.Task;
        }

        private sealed class DeferredWebView : IWebViewAdapter, IWebViewLifecycleSource
        {
            public bool IsAvailable => true;
            public bool CanGoBack => false;
            public string Url;
            public event System.Action LoadStarted;
            public event System.Action LoadCompleted;
            public event System.Action<string> LoadFailed;
            public void Show(string url) { Url = url; LoadStarted?.Invoke(); }
            public void Complete() => LoadCompleted?.Invoke();
            public void Fail(string error) => LoadFailed?.Invoke(error);
            public void Hide() { }
            public void GoBack() { }
        }

        [UnityTest]
        public IEnumerator GameAndMusic_WaitThroughLoadingOfflineAndWeb_UntilGameDecision()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Object.FindFirstObjectByType<EggCatcherApp>().enabled = false;
            GameDataStore.Save(new EggCatcherData { Name = "Gate test", HasProfile = true, MusicEnabled = true }, Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();

            Assert.That(Object.FindFirstObjectByType<WebWrapperController>(), Is.Null);
            WebWrapperBootstrap.SetEditorPreview(false);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            app.enabled = true;
            yield return null;
            AssertBlocked(app);
            var loading = app.GetComponent<EggCatcherLoadingPanel>();
            Assert.That(WebWrapperServices.LoadingView, Is.SameAs(loading));
            Assert.That(loading.IsAvailable, Is.True);
            loading.Show();
            yield return null;
            var loadingText = app.transform.Find("LoadingPanel/SafeAreaContent/LoaingTXT").GetComponent<Text>();
            var bar = app.transform.Find("LoadingPanel/SafeAreaContent/Loader");
            Assert.That(bar.gameObject.activeSelf, Is.False);
            Assert.That(loadingText.text, Does.Match(@"\ALoading\.{1,3}\z"));
            string firstFrame = loadingText.text;
            float animationDeadline = Time.realtimeSinceStartup + 2;
            while (loadingText.text == firstFrame && Time.realtimeSinceStartup < animationDeadline)
                yield return null;
            Assert.That(loadingText.text, Is.Not.EqualTo(firstFrame), "Loading dots must animate.");
            Assert.That(loadingText.text, Does.Match(@"\ALoading\.{1,3}\z"));
            int loadingIndex = app.transform.Find("LoadingPanel").GetSiblingIndex();
            foreach (Transform child in app.transform)
                if (child.name != "LoadingPanel" && child.name != EggCatcherViewport.MarginsName)
                    Assert.That(child.GetSiblingIndex(), Is.LessThan(loadingIndex), "Loading must cover every game panel.");

            ScreenCapture.CaptureScreenshot("Library/EggCatcherServerLoading.png");
            Publish(LaunchDecision.ShowOffline("Test offline"));
            yield return null;
            AssertBlocked(app);
            loading.Hide();
            Assert.That(app.transform.Find("LoadingPanel").gameObject.activeSelf, Is.False);
            loading.Show();
            Publish(LaunchDecision.ShowWebView("https://example.com/", "Test web"));
            yield return new WaitForSecondsRealtime(.8f);
            AssertBlocked(app);
            Publish(LaunchDecision.ShowWrapper("Test game"));
            loading.Hide();
            Assert.That(app.transform.Find("LoadingPanel").gameObject.activeSelf, Is.True);

            float deadline = Time.realtimeSinceStartup + 3;
            while (!app.transform.Find("MainMenu").gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.True);
            Assert.That(app.GetComponent<EggCatcherAudio>(), Is.Not.Null);
            Assert.That(app.GetComponent<EggCatcherAudio>().MusicEnabled, Is.True);
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.False, "Do not show PushPanel in the first menu frame.");
            deadline = Time.realtimeSinceStartup + 3;
            while (!WebWrapperLaunchState.IsGameReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.True, "Visible menu releases the deferred game-only prompt.");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBlocked(EggCatcherApp app)
        {
            Assert.That(WebWrapperLaunchState.IsGameReady, Is.False);
            Assert.That(app.transform.Find("PushPanel").gameObject.activeInHierarchy, Is.False);
            Assert.That(app.transform.Find("LoadingPanel").gameObject.activeInHierarchy, Is.True);
            Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.False);
            Assert.That(app.transform.Find("CreateProfilePanel").gameObject.activeInHierarchy, Is.False);
            foreach (var source in app.GetComponents<AudioSource>())
                Assert.That(source.isPlaying, Is.False, "Prepared audio must not start before the game decision.");
            var game = Object.FindFirstObjectByType<EggCatcherGame>(FindObjectsInactive.Include);
            Assert.That(game.gameObject.activeSelf, Is.False);
        }

        private static void Publish(LaunchDecision decision) => typeof(WebWrapperLaunchState)
            .GetMethod("Publish", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { decision });

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (replacedWebView)
            {
                WebWrapperServices.WebViewAdapter = previousWebView;
                replacedWebView = false;
            }
            if (!string.IsNullOrEmpty(serverProfileSlot))
            {
                WebWrapperEditorLiveSession.End();
                GameDataStore.Reset(serverProfileSlot);
                if (hadInstallationId) PlayerPrefs.SetString(installationKey, previousInstallationId);
                else PlayerPrefs.DeleteKey(installationKey);
                PlayerPrefs.Save();
                serverProfileSlot = null;
            }
            if (disabledWebSettings != null) Object.DestroyImmediate(disabledWebSettings);
            WebWrapperBootstrap.SetEditorPreview(true);
            if (Application.isPlaying) yield return new ExitPlayMode();
            WebWrapperBootstrap.SetEditorPreview(false);
            GameDataStore.Reset(Slot);
            SessionState.EraseString("EggCatcher.PreviewSlot");
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
        }
    }
}
