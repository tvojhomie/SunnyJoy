using System.Reflection;
using AlienProject.WebWrapper.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class WebWrapperAppTests
    {
        private GameObject host;
        private WebWrapperApp app;
        private WebWrapperSettings settings;

        [SetUp] public void Setup()
        {
            host = new GameObject("Scene configuration unit test");
            app = host.AddComponent<WebWrapperApp>();
            settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            Set(app, "applicationName", "Sample Game");
            Set(app, "bundleIdentifier", "com.example.game");
            Set(app, "firebaseDatabaseUrl", "https://example-default-rtdb.firebaseio.com/");
            Set(app, "firebaseConfigPath", "apps/game/webview");
            Set(app, "appsFlyerDevKey", "unit-test-only");
            Set(app, "appleAppId", "1234567890");
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
        }

        [Test] public void Defaults_HaveNoMockOrExampleEndpoints()
        {
            Assert.IsTrue(app.WebViewEnabled);
            Assert.IsTrue(settings.WebViewEnabled);
            Assert.IsFalse(settings.UseMockServices);
            Assert.IsTrue(settings.UsesFirebaseDatabase);
            Assert.IsEmpty(settings.ConfigEndpoint);
            Assert.IsEmpty(settings.MockWebUrl);
            Assert.IsEmpty(settings.FirebaseDatabaseUrl);
            Assert.IsEmpty(settings.AppsFlyerDevKey);
            Assert.AreEqual(0, settings.RequestTimeoutSeconds);
            Assert.AreEqual(8, settings.BackgroundRequestTimeoutSeconds);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Generate_CopiesWebViewSwitchWithoutDisablingBackgroundServices(bool enabled)
        {
            Set(app, "webViewEnabled", enabled);
            settings.ConfigureFromApp(app, "scene-guid");
            Assert.AreEqual(enabled, settings.WebViewEnabled);
            Assert.IsTrue(settings.AutoStart);
            Assert.IsTrue(settings.UsesFirebaseDatabase);
        }

        [Test] public void Generate_UsesSceneValuesAndErasesLegacyData()
        {
            Set(settings, "configEndpoint", "https://old.invalid/config.php");
            Set(settings, "mockWebUrl", "https://old.invalid/offer");
            Set(settings, "useMockServices", true);
            Set(settings, "appsFlyerDebugLogging", true);
            Set(settings, "requestTimeoutSeconds", 8);
            settings.ConfigureFromApp(app, "scene-guid");
            Assert.IsTrue(settings.AutoStart);
            Assert.IsTrue(settings.UsesFirebaseDatabase);
            Assert.IsFalse(settings.UseMockServices);
            Assert.IsFalse(settings.VerboseLogging);
            Assert.IsFalse(settings.AppsFlyerDebugLogging);
            Assert.AreEqual(0, settings.RequestTimeoutSeconds);
            Assert.AreEqual(8, settings.BackgroundRequestTimeoutSeconds);
            Assert.IsEmpty(settings.ConfigEndpoint);
            Assert.IsEmpty(settings.MockWebUrl);
            Assert.AreEqual("scene-guid", settings.SourceSceneGuid);
            Assert.AreEqual("com.example.game", settings.AndroidStoreId);
            Assert.AreEqual("1234567890", settings.IosStoreId);
            Assert.AreEqual(settings.IosStoreId, settings.AppsFlyerIosAppId);
            Assert.AreEqual("unit-test-only", settings.AppsFlyerDevKey);
            Assert.AreEqual("https://example-default-rtdb.firebaseio.com/apps/game/webview.json", settings.ActiveConfigAddress);
        }

        [Test] public void Generate_NormalizesAppleIdAndEmptyOptionalFields()
        {
            Set(app, "appleAppId", " id1234567890 ");
            settings.ConfigureFromApp(app, "scene-guid");
            Assert.AreEqual("1234567890", settings.AppsFlyerIosAppId);
            Assert.IsEmpty(settings.OneLinkDomain);
            Assert.IsEmpty(settings.PrivacyPolicyUrl);
            Assert.IsEmpty(settings.IosUrlScheme);
        }

        [Test] public void Android_AppleIdIsOptional_WhileIosRequiresIt()
        {
            Set(app, "appleAppId", "");
            Assert.IsEmpty(WebWrapperAppSetup.Validate(app, false));
            Assert.IsNotEmpty(WebWrapperAppSetup.Validate(app, true));
        }

        [TestCase("bundleIdentifier", "bad id")]
        [TestCase("firebaseDatabaseUrl", "https://unrelated.invalid")]
        [TestCase("firebaseConfigPath", "apps/com.game.name/webview")]
        [TestCase("appsFlyerDevKey", "")]
        [TestCase("oneLinkDomain", "https://example.onelink.me/path")]
        [TestCase("iosUrlScheme", "scheme://")]
        [TestCase("privacyPolicyUrl", "javascript:alert(1)")]
        public void InvalidConfiguration_IsReportedWithoutExposingValues(string field, string value)
        {
            Set(app, field, value);
            Assert.IsNotEmpty(WebWrapperAppSetup.Validate(app, false));
        }

        [Test] public void GameRootCannotContainBootstrap()
        {
            Set(app, "gameRoot", host);
            Assert.IsNotEmpty(WebWrapperAppSetup.Validate(app, false));
        }

        [Test] public void GenerationChangesKeysOnNextApp_WithoutOldIdentityLeaking()
        {
            settings.ConfigureFromApp(app, "first");
            Set(app, "bundleIdentifier", "com.example.another");
            Set(app, "appsFlyerDevKey", "second-test-value");
            settings.ConfigureFromApp(app, "second");
            Assert.AreEqual("com.example.another", settings.AndroidStoreId);
            Assert.AreEqual("second-test-value", settings.AppsFlyerDevKey);
            Assert.AreEqual("second", settings.SourceSceneGuid);
        }

        [Test] public void GameRoot_StaysOffForWebAndRestoresWithoutRepeatingInitialization()
        {
            var root = new GameObject("Game root");
            root.SetActive(false);
            Set(app, "gameRoot", root);
            var gameReadyCount = 0;
            var ready = (UnityEngine.Events.UnityEvent)typeof(WebWrapperApp)
                .GetField("onGameReady", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            ready.AddListener(() => gameReadyCount++);
            void Route(bool game) => typeof(WebWrapperApp).GetMethod("ApplyGameVisibility", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(app, new object[] { game });
            try
            {
                Route(false); Assert.IsFalse(root.activeSelf);
                Route(true); Assert.IsTrue(root.activeSelf);
                Route(false); Assert.IsFalse(root.activeSelf);
                Route(true); Assert.IsTrue(root.activeSelf);
                Assert.AreEqual(1, gameReadyCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
