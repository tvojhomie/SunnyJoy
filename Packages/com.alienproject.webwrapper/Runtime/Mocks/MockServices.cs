using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public sealed class MockConnectivityService : IConnectivityService
    {
        private readonly WebWrapperMockScenario _scenario;

        public MockConnectivityService(WebWrapperMockScenario scenario)
        {
            _scenario = scenario;
        }

        public bool IsInternetReachable => _scenario != WebWrapperMockScenario.NoInternet;
    }

    public sealed class MockAttributionProvider : IAttributionProvider
    {
        private readonly WebWrapperMockScenario _scenario;

        public MockAttributionProvider(WebWrapperMockScenario scenario)
        {
            _scenario = scenario;
        }

        public Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _scenario == WebWrapperMockScenario.NonOrganicSuccess
                ? "Non-organic"
                : "Organic";

            var conversion = new Dictionary<string, object>
            {
                ["af_status"] = status,
                ["is_first_launch"] = true,
                ["media_source"] = status == "Non-organic" ? "Mock Campaign" : "organic"
            };
            var deepLink = new Dictionary<string, object>
            {
                ["deep_link_value"] = "mock-offer",
                ["media_source"] = "This duplicate must not replace conversion data"
            };
            var payloads = new List<AttributionPayload>
            {
                new AttributionPayload(conversion),
                new AttributionPayload(deepLink)
            };
            return Task.FromResult(new AttributionSnapshot("mock-appsflyer-id", payloads));
        }
    }

    public sealed class MockPushRegistrationProvider : IPushRegistrationProvider
    {
        public Task<PushRegistration> GetRegistrationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PushRegistration("mock-push-token", "mock-firebase-project"));
        }
    }

    public sealed class MockConfigClient : IConfigClient
    {
        private readonly WebWrapperMockScenario _scenario;
        private readonly string _url;
        private readonly int _lifetimeSeconds;
        private readonly IClock _clock;

        public MockConfigClient(
            WebWrapperMockScenario scenario,
            string url,
            int lifetimeSeconds,
            IClock clock)
        {
            _scenario = scenario;
            _url = url;
            _lifetimeSeconds = lifetimeSeconds;
            _clock = clock;
        }

        public Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_scenario == WebWrapperMockScenario.NoInternet)
            {
                return Task.FromResult(ConfigResult.NetworkUnavailable("Mock device is offline."));
            }

            if (_scenario == WebWrapperMockScenario.ServerError)
            {
                return Task.FromResult(ConfigResult.Negative("Mock endpoint returned HTTP 500."));
            }

            if (request.Values.TryGetValue("af_status", out var value) &&
                string.Equals(value as string, "Non-organic", System.StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ConfigResult.Success(
                    _url,
                    _clock.UtcNowUnixSeconds + _lifetimeSeconds));
            }

            return Task.FromResult(ConfigResult.Negative("Mock endpoint rejected an organic install."));
        }
    }
}
