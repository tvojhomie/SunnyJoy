using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class FirebaseDatabaseTests
    {
        private const string Database = "https://example-default-rtdb.firebaseio.com";
        private const string Page = "https://example.com/test/";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(0)]
        [TestCase(8)]
        public void ConfigClients_PreserveZeroAsUnlimitedRequestTimeout(int seconds)
        {
            var firebase = new FirebaseDatabaseConfigClient(Database, "apps/game/webview", seconds);
            var http = new UnityWebRequestConfigClient("https://example.com/config", seconds);
            Assert.AreEqual(seconds, firebase.GetType().GetField("_timeoutSeconds", Private).GetValue(firebase));
            Assert.AreEqual(seconds, http.GetType().GetField("_timeoutSeconds", Private).GetValue(http));
        }

        [TestCase(Database)]
        [TestCase("https://example.europe-west1.firebasedatabase.app/")]
        public void Endpoint_ReadsExactlyOneConfigNode(string database)
        {
            Assert.IsTrue(FirebaseDatabaseConfigClient.TryBuildEndpoint(database, "/apps/sunny_joy/webview/", out var uri));
            Assert.AreEqual("/apps/sunny_joy/webview.json", uri.AbsolutePath);
            Assert.IsEmpty(uri.Query);
        }

        [TestCase("http://example.firebaseio.com", "apps/game")]
        [TestCase("https://example.com", "apps/game")]
        [TestCase("https://example.firebaseio.com.attacker.com", "apps/game")]
        [TestCase(Database + "?auth=secret", "apps/game")]
        [TestCase("https://user:secret@example.firebaseio.com", "apps/game")]
        [TestCase(Database + "/existing/path", "apps/game")]
        [TestCase(Database, "")]
        [TestCase(Database, "../.settings/rules")]
        [TestCase(Database, "apps/com.example.game")]
        [TestCase(Database, "apps//game")]
        [TestCase(Database, "apps/game.json?auth=secret")]
        public void Endpoint_RejectsCredentialsTraversalAndWrongHosts(string database, string path)
        {
            Assert.IsFalse(FirebaseDatabaseConfigClient.TryBuildEndpoint(database, path, out _));
        }

        [Test]
        public void ValidConfig_ReturnsExactRemoteUrlWithoutPermanentCache()
        {
            var result = FirebaseDatabaseConfigClient.ParseResponse("{\"enabled\":true,\"url\":\"" + Page + "?qa=1\"}");
            Assert.AreEqual(ConfigResultKind.Success, result.Kind);
            Assert.AreEqual(Page + "?qa=1", result.Url);
            Assert.AreEqual(0, result.ExpiresUnixSeconds);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("null")]
        [TestCase("<html>error</html>")]
        [TestCase("{broken")]
        [TestCase("[]")]
        [TestCase("{\"enabled\":true}")]
        [TestCase("{\"enabled\":true,\"url\":\"javascript:alert(1)\"}")]
        [TestCase("{\"enabled\":true,\"url\":\"http://example.com\"}")]
        [TestCase("{\"enabled\":true,\"url\":\"https://user:secret@example.com\"}")]
        public void EmptyInvalidOrUnsafeConfig_FailsSafely(string json)
        {
            Assert.AreNotEqual(ConfigResultKind.Success, FirebaseDatabaseConfigClient.ParseResponse(json).Kind);
        }

        [Test]
        public async Task DisabledThenEnabled_RechecksWithoutPermanentGameAssignment()
        {
            var client = new StubClient { Result = FirebaseDatabaseConfigClient.ParseResponse("{\"enabled\":false}") };
            var coordinator = new FirebaseDatabaseLaunchCoordinator(new Connectivity(true), client);
            Assert.AreEqual(LaunchDecisionKind.Wrapper, (await coordinator.ResolveAsync(CancellationToken.None)).Kind);
            client.Result = ConfigResult.Success(Page, 0);
            var decision = await coordinator.ResolveAsync(CancellationToken.None);
            Assert.AreEqual(LaunchDecisionKind.WebView, decision.Kind);
            Assert.AreEqual(Page, decision.Url);
            Assert.AreEqual(2, client.Calls);
            Assert.IsEmpty(client.Request.Values, "No attribution, token or device fields may be uploaded to DB.");
        }

        [Test]
        public async Task Offline_NeverCallsDatabase()
        {
            var client = new StubClient();
            var decision = await new FirebaseDatabaseLaunchCoordinator(new Connectivity(false), client).ResolveAsync(CancellationToken.None);
            Assert.AreEqual(LaunchDecisionKind.Wrapper, decision.Kind);
            Assert.AreEqual(0, client.Calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task NullResponseOrException_OpensGame(bool throws)
        {
            var client = new StubClient { Throws = throws };
            var decision = await new FirebaseDatabaseLaunchCoordinator(new Connectivity(true), client).ResolveAsync(CancellationToken.None);
            Assert.AreEqual(LaunchDecisionKind.Wrapper, decision.Kind);
        }

        [Test]
        public void CancelledLaunch_DoesNotContinueToWebView()
        {
            var client = new StubClient { Result = ConfigResult.Success(Page, 0) };
            var coordinator = new FirebaseDatabaseLaunchCoordinator(new Connectivity(true), client);
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),
                async () => await coordinator.ResolveAsync(new CancellationToken(true)));
            Assert.AreEqual(0, client.Calls);
        }

        [Test]
        public async Task DatabaseMode_DoesNotRequireAppsFlyerOrReadLegacyRoute()
        {
            var host = new GameObject("Firebase route test");
            var settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            var previous = WebWrapperServices.AttributionProvider;
            try
            {
                typeof(WebWrapperSettings).GetField("useMockServices", Private).SetValue(settings, false);
                typeof(WebWrapperSettings).GetField("configSource", Private).SetValue(settings, RemoteConfigSource.FirebaseRealtimeDatabase);
                var controller = host.AddComponent<WebWrapperController>();
                typeof(WebWrapperController).GetField("_settings", Private).SetValue(controller, settings);
                WebWrapperServices.AttributionProvider = null;
                var coordinator = (ILaunchCoordinator)typeof(WebWrapperController).GetMethod("CreateCoordinator", Private)
                    .Invoke(controller, new object[] { new SystemClock(), new AppIdentity("com.test.app", "Android", "com.test.app", "en-US") });
                Assert.IsInstanceOf<FirebaseDatabaseLaunchCoordinator>(coordinator);

                Assert.AreEqual(LaunchDecisionKind.Wrapper, (await coordinator.ResolveAsync(CancellationToken.None)).Kind);
                typeof(WebWrapperController).GetMethod("ConfigureNotificationServices", Private).Invoke(controller, null);
                Assert.IsNull(typeof(WebWrapperController).GetField("_registrationSync", Private).GetValue(controller));
            }
            finally
            {
                WebWrapperServices.AttributionProvider = previous;
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public async Task PushWithoutPhp_DeduplicatesWithoutCreatingHttpCallbacks()
        {
            var state = new MemoryStore();
            var processor = new PushOpenProcessor(state, null);
            var message = new PushOpenMessage("test-message", Page);
            Assert.AreEqual(PushOpenResultKind.OpenWebView, processor.Enqueue(message).Kind);
            Assert.AreEqual(PushOpenResultKind.Duplicate, processor.Enqueue(message).Kind);
            Assert.IsEmpty(state.Load().PendingInteractionCallbacks);
            Assert.AreEqual(0, await processor.FlushPendingAsync(CancellationToken.None));
        }

        private sealed class Connectivity : IConnectivityService
        {
            public bool IsInternetReachable { get; }
            public Connectivity(bool online) { IsInternetReachable = online; }
        }
        private sealed class StubClient : IConfigClient
        {
            public ConfigResult Result;
            public bool Throws;
            public int Calls;
            public ConfigRequest Request;
            public Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken token)
            {
                Calls++;
                Request = request;
                if (Throws) throw new InvalidOperationException("network error");
                return Task.FromResult(Result);
            }
        }
        private sealed class MemoryStore : IInstallStateStore
        {
            private InstallState state = new InstallState();
            public InstallState Load() => state;
            public void Save(InstallState value) => state = value;
            public void Clear() => state = new InstallState();
        }
    }
}
