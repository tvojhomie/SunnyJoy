using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public sealed class LaunchCoordinator : ILaunchCoordinator
    {
        private readonly IInstallStateStore _stateStore;
        private readonly IConnectivityService _connectivity;
        private readonly IAttributionProvider _attributionProvider;
        private readonly IPushRegistrationProvider _pushProvider;
        private readonly IConfigClient _configClient;
        private readonly IClock _clock;
        private readonly AppIdentity _identity;

        public LaunchCoordinator(
            IInstallStateStore stateStore,
            IConnectivityService connectivity,
            IAttributionProvider attributionProvider,
            IPushRegistrationProvider pushProvider,
            IConfigClient configClient,
            IClock clock,
            AppIdentity identity)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            _attributionProvider = attributionProvider ?? throw new ArgumentNullException(nameof(attributionProvider));
            _pushProvider = pushProvider ?? throw new ArgumentNullException(nameof(pushProvider));
            _configClient = configClient ?? throw new ArgumentNullException(nameof(configClient));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public async Task<LaunchDecision> ResolveAsync(CancellationToken cancellationToken)
        {
            var state = _stateStore.Load() ?? new InstallState();
            switch (state.Mode)
            {
                case LaunchMode.Wrapper:
                    return LaunchDecision.ShowWrapper("The installation is permanently assigned to wrapper mode.");
                case LaunchMode.WebView:
                    return await ResolveWebViewModeAsync(state, cancellationToken);
                default:
                    return await ResolveFirstLaunchAsync(state, cancellationToken);
            }
        }

        private async Task<LaunchDecision> ResolveFirstLaunchAsync(
            InstallState state,
            CancellationToken cancellationToken)
        {
            if (!_connectivity.IsInternetReachable)
            {
                return LaunchDecision.ShowWrapper("Offline; playing the game for this session.");
            }

            var request = await BuildRequestAsync(cancellationToken);
            var result = await FetchSafelyAsync(request, cancellationToken);

            if (result.Kind == ConfigResultKind.NetworkUnavailable || result.Kind == ConfigResultKind.Failure)
            {
                return LaunchDecision.ShowWrapper("Config unavailable; session fallback: " + result.Message);
            }

            if (result.Kind == ConfigResultKind.Success && IsHttpUrl(result.Url))
            {
                SaveWebViewResult(result, request);
                return LaunchDecision.ShowWebView(result.Url, "The config endpoint selected WebView mode.");
            }

            if (result.Kind == ConfigResultKind.Success)
                return LaunchDecision.ShowWrapper("Invalid server URL; playing the game for this session.");

            _stateStore.Update(current => current.Mode = LaunchMode.Wrapper);
            return LaunchDecision.ShowWrapper(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "The config endpoint rejected WebView mode."
                    : result.Message);
        }

        private async Task<LaunchDecision> ResolveWebViewModeAsync(
            InstallState state,
            CancellationToken cancellationToken)
        {
            if (!IsHttpUrl(state.CachedUrl))
            {
                return LaunchDecision.ShowWrapper("Invalid cached URL; playing the game for this session.");
            }

            if (!_connectivity.IsInternetReachable)
            {
                return LaunchDecision.ShowWrapper("Offline; playing the game for this session.");
            }

            if (state.ExpiresUnixSeconds > _clock.UtcNowUnixSeconds)
            {
                return LaunchDecision.ShowWebView(state.CachedUrl, "The cached URL is still valid.");
            }

            var request = await BuildRequestAsync(cancellationToken);
            var result = await FetchSafelyAsync(request, cancellationToken);
            if (result.Kind == ConfigResultKind.NetworkUnavailable || !_connectivity.IsInternetReachable)
            {
                return LaunchDecision.ShowWrapper("Offline during refresh; playing the game for this session.");
            }
            if (result.Kind == ConfigResultKind.Success && IsHttpUrl(result.Url))
            {
                SaveWebViewResult(result, request);
                return LaunchDecision.ShowWebView(result.Url, "The expired URL was refreshed.");
            }

            return LaunchDecision.ShowWrapper("Refresh failed; playing the game for this session.");
        }

        private void SaveWebViewResult(ConfigResult result, ConfigRequest request)
        {
            _stateStore.Update(current =>
            {
                current.Mode = LaunchMode.WebView;
                current.CachedUrl = result.Url;
                current.ExpiresUnixSeconds = result.ExpiresUnixSeconds;
                if (request.Values.TryGetValue("push_token", out var token) &&
                    request.Values.TryGetValue("firebase_project_id", out var project))
                {
                    current.LastRegisteredPushToken = token as string ?? string.Empty;
                    current.LastRegisteredFirebaseProjectId = project as string ?? string.Empty;
                }
            });
        }

        private async Task<ConfigRequest> BuildRequestAsync(CancellationToken cancellationToken)
        {
            var attributionTask = GetAttributionSafelyAsync(cancellationToken);
            var pushTask = GetPushSafelyAsync(cancellationToken);
            await Task.WhenAll(attributionTask, pushTask);
            return ConfigPayloadBuilder.Build(attributionTask.Result, pushTask.Result, _identity);
        }

        private async Task<AttributionSnapshot> GetAttributionSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _attributionProvider.GetSnapshotAsync(cancellationToken) ?? AttributionSnapshot.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return AttributionSnapshot.Empty;
            }
        }

        private async Task<PushRegistration> GetPushSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _pushProvider.GetRegistrationAsync(cancellationToken) ?? PushRegistration.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return PushRegistration.Empty;
            }
        }

        private async Task<ConfigResult> FetchSafelyAsync(
            ConfigRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _configClient.FetchAsync(request, cancellationToken) ??
                       ConfigResult.Failure("The config endpoint returned no result.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ConfigResult.Failure(exception.Message);
            }
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
