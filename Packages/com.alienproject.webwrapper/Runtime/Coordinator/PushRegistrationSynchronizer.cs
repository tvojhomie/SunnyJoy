using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public sealed class PushRegistrationSynchronizer
    {
        private readonly IInstallStateStore _store;
        private readonly IConfigClient _client;
        private readonly IAttributionProvider _attribution;
        private readonly IConnectivityService _connectivity;
        private readonly AppIdentity _identity;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private PushRegistration _latest;

        public PushRegistrationSynchronizer(IInstallStateStore store, IConfigClient client,
            IAttributionProvider attribution, IConnectivityService connectivity, AppIdentity identity)
        {
            _store = store; _client = client; _attribution = attribution;
            _connectivity = connectivity; _identity = identity;
        }

        public void Queue(PushRegistration registration)
        {
            if (registration != null && registration.IsComplete) _latest = registration;
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                while (_latest != null && _connectivity.IsInternetReachable)
                {
                    var registration = _latest;
                    var state = _store.Load() ?? new InstallState();

                    if (state.Mode != LaunchMode.WebView) return;
                    if (state.LastRegisteredPushToken == registration.Token &&
                        state.LastRegisteredFirebaseProjectId == registration.FirebaseProjectId)
                    {
                        if (ReferenceEquals(_latest, registration)) _latest = null;
                        continue;
                    }
                    var attribution = await _attribution.GetSnapshotAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(attribution?.AppsFlyerId)) return;
                    var request = ConfigPayloadBuilder.Build(attribution, registration, _identity);
                    var result = await _client.FetchAsync(request, cancellationToken);
                    if (result == null || result.Kind != ConfigResultKind.Success) return;
                    _store.Update(current =>
                    {
                        current.LastRegisteredPushToken = registration.Token;
                        current.LastRegisteredFirebaseProjectId = registration.FirebaseProjectId;
                        if (current.Mode == LaunchMode.WebView && Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                        {
                            current.CachedUrl = result.Url;
                            current.ExpiresUnixSeconds = result.ExpiresUnixSeconds;
                        }
                    });
                    if (ReferenceEquals(_latest, registration)) _latest = null;
                }
            }
            finally { _gate.Release(); }
        }
    }
}
