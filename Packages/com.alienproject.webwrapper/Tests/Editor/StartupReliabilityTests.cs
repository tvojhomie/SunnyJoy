using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class StartupReliabilityTests
    {
        private static readonly AppIdentity Identity =
            new AppIdentity("com.test.wrapper", "iOS", "123456789", "en-US");
        private static readonly CancellationToken NoCancellation = CancellationToken.None;

        [Test]
        public void StartupTimeout_FirstLaunchOffline_RemainsUnassignedAndShowsGame()
        {
            var store = new SerializedStore(new InstallState());
            var decision = LaunchTimeoutPolicy.Resolve(store, false);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.Load().Mode, Is.EqualTo(LaunchMode.Unknown));
        }

        [Test]
        public void StartupTimeout_ExistingWebMode_KeepsCachedUrlEvenWhenExpired()
        {
            var store = WebStore(expires: 1);
            var decision = LaunchTimeoutPolicy.Resolve(store, true);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.Load().CachedUrl, Is.EqualTo("https://example.com/cached"));
            Assert.That(store.Load().Mode, Is.EqualTo(LaunchMode.WebView));
            Assert.That(store.Load().ExpiresUnixSeconds, Is.EqualTo(1));
        }

        [Test]
        public void StartupTimeout_FirstLaunchOnline_DoesNotPermanentlySelectGameMode()
        {
            var store = new SerializedStore(new InstallState());
            var decision = LaunchTimeoutPolicy.Resolve(store, true);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.Load().Mode, Is.EqualTo(LaunchMode.Unknown));
            Assert.That(LaunchTimeoutPolicy.Resolve(store, false).Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
        }

        [Test]
        public async Task StartupTimeout_WhileInteractionIsPending_PreservesCallbackAndSavedGameMode()
        {
            var store = new SerializedStore(new InstallState
            {
                HandledPushMessageIds = new List<string> { "previous-message" },
                NotificationPromptDeferredUntilUnixSeconds = 900
            });
            var reply = new TaskCompletionSource<bool>();
            var callback = new QueuedCallbackClient(reply.Task);
            var processor = new PushOpenProcessor(store, callback);
            var delivery = processor.ProcessAsync(
                new PushOpenMessage("pending-message", "https://example.com/push"), "af-device", NoCancellation);
            LaunchTimeoutPolicy.Resolve(store, true);
            var pending = store.Load();
            Assert.That(pending.Mode, Is.EqualTo(LaunchMode.Unknown));
            Assert.That(pending.PendingInteractionCallbacks.Count, Is.EqualTo(1));
            Assert.That(pending.PendingInteractionCallbacks[0].MessageId, Is.EqualTo("pending-message"));
            Assert.That(pending.HandledPushMessageIds, Is.EquivalentTo(new[] { "previous-message" }));

            reply.SetResult(true);
            await delivery;
            var complete = store.Load();
            Assert.That(complete.Mode, Is.EqualTo(LaunchMode.Unknown));
            Assert.That(complete.PendingInteractionCallbacks, Is.Empty);
            Assert.That(complete.HandledPushMessageIds,
                Is.EquivalentTo(new[] { "previous-message", "pending-message" }));
            Assert.That(complete.NotificationPromptDeferredUntilUnixSeconds, Is.EqualTo(900));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttributionCannotSupplyFirebaseRegistration_WhenActualRegistrationIsIncomplete(bool tokenWithoutProject)
        {
            var attribution = new AttributionSnapshot("af-device", new[]
            {
                new AttributionPayload(new Dictionary<string, object>
                {
                    ["push_token"] = "untrusted-attribution-token",
                    ["firebase_project_id"] = "untrusted-attribution-project",
                    ["campaign"] = "summer"
                })
            });
            var registration = tokenWithoutProject
                ? new PushRegistration("actual-token", "")
                : PushRegistration.Empty;
            var request = ConfigPayloadBuilder.Build(attribution, registration, Identity);
            Assert.That(request.Values.ContainsKey("push_token"), Is.False);
            Assert.That(request.Values.ContainsKey("firebase_project_id"), Is.False);
            Assert.That(request.Values["campaign"], Is.EqualTo("summer"));
        }

        [Test]
        public void CompleteFirebaseRegistration_OverridesAttributionFields()
        {
            var attribution = new AttributionSnapshot("af-device", new[]
            {
                new AttributionPayload(new Dictionary<string, object>
                {
                    ["push_token"] = "untrusted-token",
                    ["firebase_project_id"] = "untrusted-project"
                })
            });
            var request = ConfigPayloadBuilder.Build(attribution,
                new PushRegistration("actual-token", "actual-project"), Identity);
            Assert.That(request.Values["push_token"], Is.EqualTo("actual-token"));
            Assert.That(request.Values["firebase_project_id"], Is.EqualTo("actual-project"));
        }

        [TestCase(LaunchDecisionKind.WebView)]
        [TestCase(LaunchDecisionKind.Wrapper)]
        [TestCase(LaunchDecisionKind.ConfigurationError)]
        public void PushNavigation_ValidUrlPrevailsOverNormalStartupDecision(LaunchDecisionKind normalKind)
        {
            var normal = normalKind == LaunchDecisionKind.WebView
                ? LaunchDecision.ShowWebView("https://example.com/normal", "normal")
                : normalKind == LaunchDecisionKind.Wrapper
                    ? LaunchDecision.ShowWrapper("native mode")
                    : LaunchDecision.ConfigurationError("startup timed out");
            var decision = LaunchNavigationPolicy.Resolve(normal, "https://example.com/push", true);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
            Assert.That(decision.Url, Is.EqualTo("https://example.com/push"));
        }

        [Test]
        public void PushNavigation_WhenOffline_KeepsOfflineInsteadOfOpeningNormalOrPushPage()
        {
            var normal = LaunchDecision.ShowOffline("offline");
            var decision = LaunchNavigationPolicy.Resolve(normal, "https://example.com/push", false);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Offline));
            Assert.That(decision.Url, Is.Empty);
        }

        [TestCase("")]
        [TestCase("javascript:alert(1)")]
        [TestCase("file:///private/test.html")]
        public void PushNavigation_InvalidUrlLeavesNormalDecisionUnchanged(string pushUrl)
        {
            var normal = LaunchDecision.ShowWrapper("native mode");
            Assert.That(LaunchNavigationPolicy.Resolve(normal, pushUrl, true), Is.SameAs(normal));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ConfigAndPushCallback_CompleteInEitherOrderWithoutLosingState(bool configFirst)
        {
            var store = new SerializedStore(new InstallState
            {
                Mode = LaunchMode.WebView,
                CachedUrl = "https://example.com/old",
                ExpiresUnixSeconds = 1,
                NotificationPromptDeferredUntilUnixSeconds = 900,
                NotificationSystemPermissionDenied = true
            });
            var configReply = new ConfigReply();
            var config = new QueuedConfigClient(configReply);
            var callbackReply = new TaskCompletionSource<bool>();
            var callback = new QueuedCallbackClient(callbackReply.Task);
            var launch = Coordinator(store, config, push: new PushRegistration("initial-token", "project"))
                .ResolveAsync(NoCancellation);
            var processor = new PushOpenProcessor(store, callback);
            var push = processor.ProcessAsync(
                new PushOpenMessage("opened-message", "https://example.com/push"), "af-device", NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
            Assert.That(callback.MessageIds.Count, Is.EqualTo(1));

            if (configFirst)
            {
                configReply.Complete(ConfigResult.Success("https://example.com/fresh", 500));
                await launch;
                callbackReply.SetResult(true);
                await push;
            }
            else
            {
                callbackReply.SetResult(true);
                await push;
                configReply.Complete(ConfigResult.Success("https://example.com/fresh", 500));
                await launch;
            }

            var state = store.Load();
            Assert.That(state.Mode, Is.EqualTo(LaunchMode.WebView));
            Assert.That(state.CachedUrl, Is.EqualTo("https://example.com/fresh"));
            Assert.That(state.ExpiresUnixSeconds, Is.EqualTo(500));
            Assert.That(state.LastRegisteredPushToken, Is.EqualTo("initial-token"));
            Assert.That(state.LastRegisteredFirebaseProjectId, Is.EqualTo("project"));
            Assert.That(state.HandledPushMessageIds, Is.EquivalentTo(new[] { "opened-message" }));
            Assert.That(state.PendingInteractionCallbacks, Is.Empty);
            Assert.That(state.NotificationPromptDeferredUntilUnixSeconds, Is.EqualTo(900));
            Assert.That(state.NotificationSystemPermissionDenied, Is.True);
        }

        [Test]
        public async Task CallbackCompletion_PreservesAnotherPushQueuedWhileRequestWasInFlight()
        {
            var store = WebStore();
            var firstReply = new TaskCompletionSource<bool>();
            var callback = new QueuedCallbackClient(firstReply.Task, Task.FromResult(true));
            var processor = new PushOpenProcessor(store, callback);
            var first = processor.ProcessAsync(
                new PushOpenMessage("first", "https://example.com/first"), "af-device", NoCancellation);
            processor.Enqueue(new PushOpenMessage("second", "https://example.com/second"), "af-device");
            firstReply.SetResult(true);
            await first;

            var state = store.Load();
            Assert.That(state.HandledPushMessageIds, Is.EquivalentTo(new[] { "first" }));
            Assert.That(state.PendingInteractionCallbacks.Count, Is.EqualTo(1));
            Assert.That(state.PendingInteractionCallbacks[0].MessageId, Is.EqualTo("second"));
            Assert.That(await processor.FlushPendingAsync(NoCancellation), Is.EqualTo(1));
            Assert.That(store.Load().HandledPushMessageIds, Is.EquivalentTo(new[] { "first", "second" }));
        }

        [Test]
        public async Task ConcurrentDuplicatePushes_SendInteractionOnlyOnce()
        {
            var store = WebStore();
            var reply = new TaskCompletionSource<bool>();
            var callback = new QueuedCallbackClient(reply.Task);
            var processor = new PushOpenProcessor(store, callback);
            var message = new PushOpenMessage("same-message", "https://example.com/push");
            var first = processor.ProcessAsync(message, "af-device", NoCancellation);
            var duplicate = processor.ProcessAsync(message, "af-device", NoCancellation);
            Assert.That(callback.MessageIds.Count, Is.EqualTo(1));
            reply.SetResult(true);

            Assert.That((await first).Kind, Is.EqualTo(PushOpenResultKind.OpenWebView));
            var duplicateResult = await duplicate;
            Assert.That(duplicateResult.Kind, Is.EqualTo(PushOpenResultKind.Duplicate));
            Assert.That(duplicateResult.CallbackDelivered, Is.True);
            Assert.That(callback.MessageIds.Count, Is.EqualTo(1));
            Assert.That(store.Load().HandledPushMessageIds, Is.EquivalentTo(new[] { "same-message" }));
        }

        [Test]
        public async Task ColdPush_UrlIsAvailableBeforeAttributionAndInteractionComplete()
        {
            var store = WebStore();
            var reply = new TaskCompletionSource<bool>();
            var callback = new QueuedCallbackClient(reply.Task);
            var processor = new PushOpenProcessor(store, callback);
            var navigation = processor.Enqueue(new PushOpenMessage("cold-push", "https://example.com/cold"));
            Assert.That(navigation.Kind, Is.EqualTo(PushOpenResultKind.OpenWebView));
            Assert.That(navigation.Url, Is.EqualTo("https://example.com/cold"));
            Assert.That(callback.MessageIds, Is.Empty);
            Assert.That(await processor.FlushPendingAsync(NoCancellation), Is.Zero);

            var flush = processor.FlushPendingAsync(NoCancellation, "late-af-device");
            Assert.That(flush.IsCompleted, Is.False);
            Assert.That(callback.AppsFlyerIds, Is.EquivalentTo(new[] { "late-af-device" }));
            Assert.That(navigation.Url, Is.EqualTo("https://example.com/cold"));
            reply.SetResult(true);
            Assert.That(await flush, Is.EqualTo(1));
            Assert.That(store.Load().PendingInteractionCallbacks, Is.Empty);
        }

        [Test]
        public async Task ExpiredCache_WhenNetworkFails_ShowsGameAndKeepsStoredWebMode()
        {
            var store = WebStore(expires: 1);
            var config = new QueuedConfigClient(ConfigReply.Completed(ConfigResult.NetworkUnavailable("offline")));
            var decision = await Coordinator(store, config).ResolveAsync(NoCancellation);
            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.Load().Mode, Is.EqualTo(LaunchMode.WebView));
            Assert.That(store.Load().CachedUrl, Is.EqualTo("https://example.com/cached"));
        }

        [Test]
        public async Task NetworkDisappearsDuringRefresh_DoesNotOpenCachedPageAfterEndpointFailure()
        {
            var store = WebStore(expires: 1);
            var connectivity = new MutableConnectivity();
            var reply = new ConfigReply();
            var config = new QueuedConfigClient(reply);
            var launch = Coordinator(store, config, connectivity).ResolveAsync(NoCancellation);
            connectivity.IsInternetReachable = false;
            reply.Complete(ConfigResult.Negative("connection lost"));
            Assert.That((await launch).Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
        }

        [TestCase(true, ConfigResultKind.Failure)]
        [TestCase(false, ConfigResultKind.NetworkUnavailable)]
        public void ConnectionFailure_DistinguishesEndpointErrorFromDisconnectedNetwork(
            bool reachable, ConfigResultKind expected)
        {
            var result = UnityWebRequestConfigClient.ClassifyConnectionFailure(reachable, "request timed out");
            Assert.That(result.Kind, Is.EqualTo(expected));
            Assert.That(result.Message, Is.EqualTo("request timed out"));
        }

        [Test]
        public async Task CachedValidWebMode_StillPostsLateRegistrationAndDeduplicatesIt()
        {
            var store = WebStore();
            var config = new QueuedConfigClient(ConfigReply.Completed(ConfigResult.Success("https://example.com/updated", 900)));
            var sync = Synchronizer(store, config);
            var launch = await Coordinator(store, config).ResolveAsync(NoCancellation);
            Assert.That(launch.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
            Assert.That(config.Requests, Is.Empty, "A valid cache skips launch config, but must not skip token updates.");
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests, Is.Empty);

            sync.Queue(new PushRegistration("late-token", "firebase-project"));
            await sync.FlushAsync(NoCancellation);
            sync.Queue(new PushRegistration("late-token", "firebase-project"));
            await sync.FlushAsync(NoCancellation);

            Assert.That(config.Requests.Count, Is.EqualTo(1));
            var payload = config.Requests[0].Values;
            Assert.That(payload["push_token"], Is.EqualTo("late-token"));
            Assert.That(payload["firebase_project_id"], Is.EqualTo("firebase-project"));
            Assert.That(payload["af_id"], Is.EqualTo("af-device"));
            Assert.That(payload["bundle_id"], Is.EqualTo(Identity.BundleId));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("late-token"));
            Assert.That(store.Load().CachedUrl, Is.EqualTo("https://example.com/updated"));
        }

        [Test]
        public async Task TokenRotatesDuringRequest_SecondTokenIsSentAfterFirstFinishes()
        {
            var store = WebStore();
            var firstReply = new ConfigReply();
            var secondReply = new ConfigReply();
            var config = new QueuedConfigClient(firstReply, secondReply);
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("old-token", "project"));
            var flush = sync.FlushAsync(NoCancellation);
            sync.Queue(new PushRegistration("rotated-token", "project"));
            firstReply.Complete(ConfigResult.Success("https://example.com/first", 500));
            var secondRequest = await Within(secondReply.Started.Task);
            Assert.That(secondRequest.Values["push_token"], Is.EqualTo("rotated-token"));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("old-token"));
            secondReply.Complete(ConfigResult.Success("https://example.com/second", 600));
            await flush;

            Assert.That(config.Requests.Count, Is.EqualTo(2));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("rotated-token"));
            Assert.That(store.Load().CachedUrl, Is.EqualTo("https://example.com/second"));
        }

        [Test]
        public async Task ConcurrentRegistrationFlushes_DoNotSendSameTokenTwice()
        {
            var store = WebStore();
            var reply = new ConfigReply();
            var config = new QueuedConfigClient(reply);
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("token", "project"));
            var first = sync.FlushAsync(NoCancellation);
            var second = sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
            reply.Complete(ConfigResult.Success("https://example.com/updated", 900));
            await Task.WhenAll(first, second);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task PermanentGameMode_NeverCallsConfigForTokenChanges()
        {
            var store = new SerializedStore(new InstallState { Mode = LaunchMode.Wrapper });
            var config = new QueuedConfigClient();
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("first-token", "project"));
            await sync.FlushAsync(NoCancellation);
            sync.Queue(new PushRegistration("rotated-token", "project"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests, Is.Empty);
            Assert.That(store.Load().Mode, Is.EqualTo(LaunchMode.Wrapper));
            Assert.That(store.Load().LastRegisteredPushToken, Is.Empty);
        }

        [Test]
        public async Task TokenArrivingBeforeFirstConfig_IsRetainedUntilWebModeIsKnown()
        {
            var store = new SerializedStore(new InstallState());
            var launchReply = new ConfigReply();
            var config = new QueuedConfigClient(launchReply,
                ConfigReply.Completed(ConfigResult.Success("https://example.com/registered", 900)));
            var sync = Synchronizer(store, config);
            var launch = Coordinator(store, config).ResolveAsync(NoCancellation);
            sync.Queue(new PushRegistration("new-token", "project"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
            Assert.That(config.Requests[0].Values.ContainsKey("push_token"), Is.False);
            launchReply.Complete(ConfigResult.Success("https://example.com/first", 800));
            await launch;
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(2));
            Assert.That(config.Requests[1].Values["push_token"], Is.EqualTo("new-token"));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("new-token"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FailedRegistrationResponse_PreservesTokenForNextRetry(bool networkUnavailable)
        {
            var store = WebStore();
            var failure = networkUnavailable ? ConfigResult.NetworkUnavailable("offline") : ConfigResult.Negative("server error");
            var config = new QueuedConfigClient(ConfigReply.Completed(failure),
                ConfigReply.Completed(ConfigResult.Success("https://example.com/updated", 900)));
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("pending-token", "project"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(store.Load().LastRegisteredPushToken, Is.Empty);
            Assert.That(store.Load().CachedUrl, Is.EqualTo("https://example.com/cached"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(2));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("pending-token"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task RegistrationTransportThrows_RetryKeepsLatestTokenAndReleasesGate()
        {
            var store = WebStore();
            var failure = new ConfigReply();
            var config = new QueuedConfigClient(failure,
                ConfigReply.Completed(ConfigResult.Success("https://example.com/updated", 900)));
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("old-token", "project"));
            var sending = sync.FlushAsync(NoCancellation);
            sync.Queue(new PushRegistration("latest-token", "project"));
            failure.Result.SetException(new InvalidOperationException("transport failed"));
            try { await sending; }
            catch (InvalidOperationException) {                                                     }
            Assert.That(store.Load().LastRegisteredPushToken, Is.Empty);
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(2));
            Assert.That(config.Requests[1].Values["push_token"], Is.EqualTo("latest-token"));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("latest-token"));
        }

        [Test]
        public async Task OfflineRegistration_RemainsQueuedUntilNetworkReturns()
        {
            var store = WebStore();
            var connectivity = new MutableConnectivity { IsInternetReachable = false };
            var config = new QueuedConfigClient(ConfigReply.Completed(ConfigResult.Success("https://example.com/updated", 900)));
            var sync = Synchronizer(store, config, connectivity);
            sync.Queue(new PushRegistration("queued-token", "project"));
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests, Is.Empty);
            connectivity.IsInternetReachable = true;
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
            Assert.That(store.Load().LastRegisteredPushToken, Is.EqualTo("queued-token"));
        }

        [Test]
        public async Task IncompleteRegistration_DoesNotReplacePreviouslyQueuedCompleteToken()
        {
            var store = WebStore();
            var config = new QueuedConfigClient(ConfigReply.Completed(ConfigResult.Success("https://example.com/updated", 900)));
            var sync = Synchronizer(store, config);
            sync.Queue(new PushRegistration("valid-token", "project"));
            sync.Queue(new PushRegistration("missing-project", ""));
            sync.Queue(PushRegistration.Empty);
            await sync.FlushAsync(NoCancellation);
            Assert.That(config.Requests.Count, Is.EqualTo(1));
            Assert.That(config.Requests[0].Values["push_token"], Is.EqualTo("valid-token"));
        }

        private static SerializedStore WebStore(long expires = 900)
        {
            return new SerializedStore(new InstallState
            {
                Mode = LaunchMode.WebView,
                CachedUrl = "https://example.com/cached",
                ExpiresUnixSeconds = expires
            });
        }

        private static LaunchCoordinator Coordinator(SerializedStore store, IConfigClient config,
            MutableConnectivity connectivity = null, PushRegistration push = null)
        {
            return new LaunchCoordinator(store, connectivity ?? new MutableConnectivity(),
                new Attribution(), new RegistrationProvider(push ?? PushRegistration.Empty), config,
                new Clock(), Identity);
        }

        private static PushRegistrationSynchronizer Synchronizer(SerializedStore store,
            IConfigClient config, MutableConnectivity connectivity = null)
        {
            return new PushRegistrationSynchronizer(store, config, new Attribution(),
                connectivity ?? new MutableConnectivity(), Identity);
        }

        private static async Task<T> Within<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.That(completed, Is.SameAs(task), "Expected asynchronous request did not start.");
            return await task;
        }

        private sealed class SerializedStore : IInstallStateStore
        {
            private string _json;
            public SerializedStore(InstallState state) => Save(state);
            public InstallState Load() => JsonUtility.FromJson<InstallState>(_json);
            public void Save(InstallState state) => _json = JsonUtility.ToJson(state);
            public void Clear() => Save(new InstallState());
        }

        private sealed class MutableConnectivity : IConnectivityService
        {
            public bool IsInternetReachable { get; set; } = true;
        }

        private sealed class Clock : IClock
        {
            public long UtcNowUnixSeconds => 100;
        }

        private sealed class Attribution : IAttributionProvider
        {
            public Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new AttributionSnapshot("af-device", Array.Empty<AttributionPayload>()));
        }

        private sealed class RegistrationProvider : IPushRegistrationProvider
        {
            private readonly PushRegistration _registration;
            public RegistrationProvider(PushRegistration registration) => _registration = registration;
            public Task<PushRegistration> GetRegistrationAsync(CancellationToken cancellationToken) =>
                Task.FromResult(_registration);
        }

        private sealed class ConfigReply
        {
            public readonly TaskCompletionSource<ConfigRequest> Started = new TaskCompletionSource<ConfigRequest>();
            public readonly TaskCompletionSource<ConfigResult> Result = new TaskCompletionSource<ConfigResult>();
            public void Complete(ConfigResult result) => Result.SetResult(result);
            public static ConfigReply Completed(ConfigResult result)
            {
                var reply = new ConfigReply();
                reply.Complete(result);
                return reply;
            }
        }

        private sealed class QueuedConfigClient : IConfigClient
        {
            private readonly Queue<ConfigReply> _replies;
            public readonly List<ConfigRequest> Requests = new List<ConfigRequest>();
            public QueuedConfigClient(params ConfigReply[] replies) => _replies = new Queue<ConfigReply>(replies);
            public Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Assert.That(_replies.Count, Is.GreaterThan(0), "Unexpected extra config request.");
                var reply = _replies.Dequeue();
                reply.Started.SetResult(request);
                return reply.Result.Task;
            }
        }

        private sealed class QueuedCallbackClient : IInteractionCallbackClient
        {
            private readonly Queue<Task<bool>> _replies;
            public readonly List<string> MessageIds = new List<string>();
            public readonly List<string> AppsFlyerIds = new List<string>();
            public QueuedCallbackClient(params Task<bool>[] replies) => _replies = new Queue<Task<bool>>(replies);
            public Task<bool> SendAsync(string messageId, string appsFlyerId, CancellationToken cancellationToken)
            {
                MessageIds.Add(messageId);
                AppsFlyerIds.Add(appsFlyerId);
                Assert.That(_replies.Count, Is.GreaterThan(0), "Unexpected duplicate interaction request.");
                return _replies.Dequeue();
            }
        }
    }
}
