using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlienProject.WebWrapper.Editor;
using AlienProject.WebWrapper.Integrations.UniWebView;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class LaunchCoordinatorTests
    {
        private static readonly AppIdentity Identity =
            new AppIdentity("com.test.app", "Android", "com.test.app", "en-US");

        [Test]
        public void FirstLaunchWithoutInternet_RemainsUnknown()
        {
            var store = new MemoryStore();
            var coordinator = CreateCoordinator(
                store,
                false,
                ConfigResult.Success("https://example.com", 200));

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.State.Mode, Is.EqualTo(LaunchMode.Unknown));
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void PositiveFirstResponse_PersistsWebViewMode()
        {
            var store = new MemoryStore();
            var coordinator = CreateCoordinator(
                store,
                true,
                ConfigResult.Success("https://example.com/offer", 200));

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
            Assert.That(store.State.Mode, Is.EqualTo(LaunchMode.WebView));
            Assert.That(store.State.CachedUrl, Is.EqualTo("https://example.com/offer"));
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void NegativeFirstResponse_PersistsWrapperMode()
        {
            var store = new MemoryStore();
            var coordinator = CreateCoordinator(store, true, ConfigResult.Negative("No data"));

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.State.Mode, Is.EqualTo(LaunchMode.Wrapper));
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void TransportFailure_OpensGameWithoutPermanentlyAssigningIt()
        {
            var store = new MemoryStore();
            var coordinator = CreateCoordinator(store, true, ConfigResult.Failure("HTTP 500"));
            Assert.That(coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult().Kind,
                Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.State.Mode, Is.EqualTo(LaunchMode.Unknown));
            Assert.That(store.SaveCount, Is.Zero);
            var retry = CreateCoordinator(store, true, ConfigResult.Success("https://example.com", 200));
            Assert.That(retry.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult().Kind,
                Is.EqualTo(LaunchDecisionKind.WebView));
        }

        [Test]
        public void WrapperMode_DoesNotCallConfigAgain()
        {
            var store = new MemoryStore { State = new InstallState { Mode = LaunchMode.Wrapper } };
            var config = new FakeConfigClient(ConfigResult.Success("https://example.com", 200));
            var coordinator = CreateCoordinator(store, true, config);

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(config.CallCount, Is.Zero);
        }

        [Test]
        public void ValidCachedUrl_DoesNotRefreshBeforeExpiry()
        {
            var store = new MemoryStore
            {
                State = new InstallState
                {
                    Mode = LaunchMode.WebView,
                    CachedUrl = "https://cached.example",
                    ExpiresUnixSeconds = 101
                }
            };
            var config = new FakeConfigClient(ConfigResult.Negative("Should not be called"));
            var coordinator = CreateCoordinator(store, true, config);

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.WebView));
            Assert.That(decision.Url, Is.EqualTo("https://cached.example"));
            Assert.That(config.CallCount, Is.Zero);
        }

        [Test]
        public void ExpiredCachedUrl_IsPreservedButGameOpensWhenRefreshFails()
        {
            var store = new MemoryStore
            {
                State = new InstallState
                {
                    Mode = LaunchMode.WebView,
                    CachedUrl = "https://cached.example",
                    ExpiresUnixSeconds = 99
                }
            };
            var coordinator = CreateCoordinator(store, true, ConfigResult.Negative("Server error"));

            var decision = coordinator.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(decision.Kind, Is.EqualTo(LaunchDecisionKind.Wrapper));
            Assert.That(store.State.CachedUrl, Is.EqualTo("https://cached.example"));
        }

        [Test]
        public void PayloadMerge_PreservesFirstAttributionValue()
        {
            var conversion = new AttributionPayload(new Dictionary<string, object>
            {
                ["media_source"] = "conversion",
                ["af_status"] = "Non-organic"
            });
            var deepLink = new AttributionPayload(new Dictionary<string, object>
            {
                ["media_source"] = "deep-link",
                ["deep_link_value"] = "offer"
            });
            var snapshot = new AttributionSnapshot("af-id", new[] { conversion, deepLink });

            var request = ConfigPayloadBuilder.Build(snapshot, PushRegistration.Empty, Identity);

            Assert.That(request.Values["media_source"], Is.EqualTo("conversion"));
            Assert.That(request.Values["deep_link_value"], Is.EqualTo("offer"));
            Assert.That(request.Values["af_id"], Is.EqualTo("af-id"));
            Assert.That(request.Values.ContainsKey("push_token"), Is.False);
        }

        [Test]
        public void NotificationPrompt_CanBeDeferredForThreeDays()
        {
            var state = new InstallState();
            NotificationPromptPolicy.Defer(state, 1000, 3);

            Assert.That(NotificationPromptPolicy.ShouldShow(state, false, true, 1000 + 3 * 86400 - 1), Is.False);
            Assert.That(NotificationPromptPolicy.ShouldShow(state, false, true, 1000 + 3 * 86400), Is.True);
        }

        [Test]
        public void InteractionCallback_EncodesFirebaseMessageId()
        {
            var uri = InteractionCallbackRequestBuilder.BuildUri(
                "https://example.com/config.php",
                "0:1500415314455276%31bd1c9631bd1c96");

            Assert.That(uri.AbsoluteUri, Does.Contain("interaction.php?message_id="));
            Assert.That(uri.Query, Does.Contain("%3A"));
            Assert.That(uri.Query, Does.Contain("%2531"));
        }

        [Test]
        public void JsonWriter_PreservesPrimitiveTypes()
        {
            var json = JsonObjectWriter.Serialize(new Dictionary<string, object>
            {
                ["text"] = "hello",
                ["number"] = 42,
                ["flag"] = true,
                ["empty"] = null
            });

            Assert.That(json, Does.Contain("\"text\":\"hello\""));
            Assert.That(json, Does.Contain("\"number\":42"));
            Assert.That(json, Does.Contain("\"flag\":true"));
            Assert.That(json, Does.Contain("\"empty\":null"));
        }

        [Test]
        public void PushOpen_UsesOneTimeUrlAndMarksDeliveredCallback()
        {
            var store = new MemoryStore();
            var callback = new FakeInteractionCallback(true);
            var processor = new PushOpenProcessor(store, callback);

            var result = processor.ProcessAsync(
                    new PushOpenMessage("0:message%id", "https://push.example/offer"),
                    "af-id",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.Kind, Is.EqualTo(PushOpenResultKind.OpenWebView));
            Assert.That(result.Url, Is.EqualTo("https://push.example/offer"));
            Assert.That(result.CallbackDelivered, Is.True);
            Assert.That(store.State.CachedUrl, Is.Empty);
            Assert.That(store.State.HandledPushMessageIds, Does.Contain("0:message%id"));
            Assert.That(store.State.PendingInteractionCallbacks, Is.Empty);
        }

        [Test]
        public void FailedPushCallback_RemainsQueuedForRetry()
        {
            var store = new MemoryStore();
            var processor = new PushOpenProcessor(store, new FakeInteractionCallback(false));

            var result = processor.ProcessAsync(
                    new PushOpenMessage("message-id", "https://push.example"),
                    "af-id",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.Kind, Is.EqualTo(PushOpenResultKind.OpenWebView));
            Assert.That(result.CallbackDelivered, Is.False);
            Assert.That(store.State.PendingInteractionCallbacks.Count, Is.EqualTo(1));
            Assert.That(store.State.HandledPushMessageIds, Is.Empty);
        }

        [Test]
        public void DeliveredPush_IsIgnoredWhenOpenedAgain()
        {
            var store = new MemoryStore
            {
                State = new InstallState
                {
                    HandledPushMessageIds = new List<string> { "already-handled" }
                }
            };
            var callback = new FakeInteractionCallback(true);
            var processor = new PushOpenProcessor(store, callback);

            var result = processor.ProcessAsync(
                    new PushOpenMessage("already-handled", "https://push.example"),
                    "af-id",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.Kind, Is.EqualTo(PushOpenResultKind.Duplicate));
            Assert.That(callback.CallCount, Is.Zero);
        }

        [Test]
        public void PendingPush_IsNotOpenedTwice()
        {
            var store = new MemoryStore();
            var callback = new FakeInteractionCallback(false);
            var processor = new PushOpenProcessor(store, callback);

            processor.ProcessAsync(
                    new PushOpenMessage("pending-message", "https://push.example/once"),
                    "af-id",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var second = processor.ProcessAsync(
                    new PushOpenMessage("pending-message", "https://push.example/once"),
                    "af-id",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(second.Kind, Is.EqualTo(PushOpenResultKind.Duplicate));
            Assert.That(second.CallbackDelivered, Is.False);
            Assert.That(store.State.PendingInteractionCallbacks.Count, Is.EqualTo(1));
            Assert.That(callback.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void PushMapper_OnlyMapsOpenedNotifications()
        {
            var data = new Dictionary<string, string>
            {
                ["message_id"] = "fallback-id",
                ["url"] = "https://push.example/mapped"
            };

            Assert.That(
                PushOpenMessageMapper.FromPayload(false, "message-id", null, data),
                Is.Null);

            var mapped = PushOpenMessageMapper.FromPayload(true, null, null, data);
            Assert.That(mapped, Is.Not.Null);
            Assert.That(mapped.MessageId, Is.EqualTo("fallback-id"));
            Assert.That(mapped.Url, Is.EqualTo("https://push.example/mapped"));
        }

        [Test]
        public void NotificationFlow_SkipDefersCustomPrompt()
        {
            var store = new MemoryStore();
            var flow = new NotificationFlowCoordinator(
                store,
                new FakePermissionProvider(NotificationPermissionStatus.Unknown),
                new FakeClock(),
                3);

            Assert.That(flow.ShouldShowCustomPrompt(), Is.True);
            flow.Skip();
            Assert.That(flow.ShouldShowCustomPrompt(), Is.False);
            Assert.That(store.State.NotificationPromptDeferredUntilUnixSeconds, Is.EqualTo(100 + 3 * 86400));
        }

        [Test]
        public void NotificationFlow_SystemDenialPermanentlySuppressesCustomPrompt()
        {
            var store = new MemoryStore();
            var flow = new NotificationFlowCoordinator(
                store,
                new FakePermissionProvider(NotificationPermissionStatus.DeniedCanAskAgain),
                new FakeClock(),
                3);

            var result = flow.RequestSystemPermissionAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result, Is.EqualTo(NotificationPermissionStatus.DeniedCanAskAgain));
            Assert.That(store.State.NotificationSystemPermissionDenied, Is.True);
            Assert.That(flow.ShouldShowCustomPrompt(), Is.False);
        }

        [TestCase("84435554334", "id84435554334")]
        [TestCase("id84435554334", "id84435554334")]
        [TestCase(" ID84435554334 ", "ID84435554334")]
        public void IosStoreId_IsNormalized(string input, string expected)
        {
            Assert.That(StoreIdNormalizer.NormalizeIos(input), Is.EqualTo(expected));
            Assert.That(StoreIdNormalizer.IsValidIos(input), Is.True);
        }

#if UNITY_ANDROID
        [Test]
        public void AndroidBuildConfigurator_WritesFcmMetadataAndResources()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "WebWrapperAndroidBuildTest-" + System.Guid.NewGuid().ToString("N"));
            var manifestPath = System.IO.Path.Combine(root, "src/main/AndroidManifest.xml");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifestPath));
            System.IO.File.WriteAllText(
                manifestPath,
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>");
            var settings = ScriptableObject.CreateInstance<WebWrapperSettings>();

            try
            {
                AndroidNotificationBuildConfigurator.Configure(root, settings);

                var manifest = System.IO.File.ReadAllText(manifestPath);
                Assert.That(manifest, Does.Contain("default_notification_icon"));
                Assert.That(manifest, Does.Contain("default_notification_color"));
                Assert.That(manifest, Does.Contain("default_notification_channel_id"));
                Assert.That(manifest, Does.Contain("firebase_messaging_installation_id_enabled"));
                Assert.That(manifest, Does.Contain("custom_notification_android_activity"));
                Assert.That(manifest, Does.Contain("com.google.firebase.MessagingUnityPlayerActivity"));
                Assert.That(
                    System.IO.File.Exists(System.IO.Path.Combine(
                        root,
                        "src/main/res/drawable/webwrapper_notification_icon.xml")),
                    Is.True);
                Assert.That(
                    System.IO.File.Exists(System.IO.Path.Combine(
                        root,
                        "src/main/res/values/webwrapper_notification_colors.xml")),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (System.IO.Directory.Exists(root))
                {
                    System.IO.Directory.Delete(root, true);
                }
            }
        }
#endif

        [Test]
        public void UniWebViewAdapter_IsSafeWithOrWithoutPaidSdk()
        {
            var host = new GameObject("UniWebView adapter test");
            try
            {
                var adapter = host.AddComponent<ReflectionUniWebViewAdapter>();
                Assert.DoesNotThrow(() =>
                {
                    _ = adapter.IsAvailable;
                });
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static LaunchCoordinator CreateCoordinator(
            MemoryStore store,
            bool internetReachable,
            ConfigResult result)
        {
            return CreateCoordinator(store, internetReachable, new FakeConfigClient(result));
        }

        private static LaunchCoordinator CreateCoordinator(
            MemoryStore store,
            bool internetReachable,
            FakeConfigClient config)
        {
            return new LaunchCoordinator(
                store,
                new FakeConnectivity(internetReachable),
                new FakeAttribution(),
                new FakePush(),
                config,
                new FakeClock(),
                Identity);
        }

        private sealed class MemoryStore : IInstallStateStore
        {
            public InstallState State = new InstallState();
            public int SaveCount;

            public InstallState Load() => State;

            public void Save(InstallState state)
            {
                State = state;
                SaveCount++;
            }

            public void Clear()
            {
                State = new InstallState();
            }
        }

        private sealed class FakeConnectivity : IConnectivityService
        {
            public FakeConnectivity(bool value) => IsInternetReachable = value;
            public bool IsInternetReachable { get; }
        }

        private sealed class FakeAttribution : IAttributionProvider
        {
            public Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            {
                var data = new Dictionary<string, object> { ["af_status"] = "Non-organic" };
                return Task.FromResult(new AttributionSnapshot(
                    "af-id",
                    new[] { new AttributionPayload(data) }));
            }
        }

        private sealed class FakePush : IPushRegistrationProvider
        {
            public Task<PushRegistration> GetRegistrationAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(PushRegistration.Empty);
            }
        }

        private sealed class FakeConfigClient : IConfigClient
        {
            private readonly ConfigResult _result;

            public FakeConfigClient(ConfigResult result) => _result = result;
            public int CallCount { get; private set; }

            public Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeClock : IClock
        {
            public long UtcNowUnixSeconds => 100;
        }

        private sealed class FakeInteractionCallback : IInteractionCallbackClient
        {
            private readonly bool _result;

            public FakeInteractionCallback(bool result) => _result = result;
            public int CallCount { get; private set; }

            public Task<bool> SendAsync(
                string messageId,
                string appsFlyerId,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakePermissionProvider : INotificationPermissionProvider
        {
            private readonly NotificationPermissionStatus _requestResult;

            public FakePermissionProvider(NotificationPermissionStatus status)
            {
                Status = status;
                _requestResult = status == NotificationPermissionStatus.Unknown
                    ? NotificationPermissionStatus.Granted
                    : status;
            }
            public NotificationPermissionStatus Status { get; private set; }

            public Task<NotificationPermissionStatus> RequestAsync(CancellationToken cancellationToken)
            {
                Status = _requestResult;
                return Task.FromResult(Status);
            }
        }
    }
}
