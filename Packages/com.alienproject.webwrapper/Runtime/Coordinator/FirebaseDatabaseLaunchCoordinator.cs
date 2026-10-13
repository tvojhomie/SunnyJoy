using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{

    public sealed class FirebaseDatabaseLaunchCoordinator : ILaunchCoordinator
    {
        private readonly IConnectivityService _connectivity;
        private readonly IConfigClient _client;

        public FirebaseDatabaseLaunchCoordinator(IConnectivityService connectivity, IConfigClient client)
        {
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<LaunchDecision> ResolveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_connectivity.IsInternetReachable)
                return LaunchDecision.ShowWrapper("Firebase DB unavailable offline; playing the game for this session.");
            try
            {
                var result = await _client.FetchAsync(
                    new ConfigRequest(new Dictionary<string, object>()), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (result?.Kind == ConfigResultKind.Success &&
                    Uri.TryCreate(result.Url, UriKind.Absolute, out var url) &&
                    url.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(url.UserInfo))
                    return LaunchDecision.ShowWebView(result.Url, "Firebase DB supplied the WebView URL.");
                return LaunchDecision.ShowWrapper("Firebase DB session fallback: " +
                    (result?.Message ?? "No configuration received."));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return LaunchDecision.ShowWrapper("Firebase DB request failed; playing the game for this session.");
            }
        }
    }
}
