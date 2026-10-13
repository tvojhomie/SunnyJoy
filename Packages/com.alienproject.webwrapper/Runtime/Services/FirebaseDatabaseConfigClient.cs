using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AlienProject.WebWrapper
{

    public sealed class FirebaseDatabaseConfigClient : IConfigClient
    {
        [Serializable]
        private sealed class ConfigDto
        {
            public bool enabled;
            public string url;
        }

        private readonly string _databaseUrl;
        private readonly string _path;
        private readonly int _timeoutSeconds;
        private readonly IConnectivityService _connectivity;

        public FirebaseDatabaseConfigClient(string databaseUrl, string path, int timeoutSeconds,
            IConnectivityService connectivity = null)
        {
            _databaseUrl = databaseUrl;
            _path = path;
            _timeoutSeconds = Math.Max(0, timeoutSeconds);
            _connectivity = connectivity ?? new UnityConnectivityService();
        }

        public async Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken cancellationToken)
        {

            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBuildEndpoint(_databaseUrl, _path, out var endpoint))
                return ConfigResult.Failure("Invalid Firebase Database URL or config path.");

            Trace("Firebase DB GET", endpoint.AbsoluteUri);
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            using (var webRequest = UnityWebRequest.Get(endpoint))
            {
                webRequest.timeout = _timeoutSeconds;
                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.SetRequestHeader("Cache-Control", "no-cache");
                await UnityWebRequestConfigClient.SendAsync(webRequest, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Trace("Firebase DB response", "HTTP " + webRequest.responseCode + "; " +
                    webRequest.result + "; " + elapsed.ElapsedMilliseconds + " ms; " +
                    webRequest.downloadedBytes + " bytes");

                if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    return UnityWebRequestConfigClient.ClassifyConnectionFailure(
                        _connectivity.IsInternetReachable, "Firebase DB connection failed: " + webRequest.error);
                if (webRequest.responseCode != 200 || webRequest.result != UnityWebRequest.Result.Success)
                    return ConfigResult.Failure("Firebase DB HTTP " + webRequest.responseCode +
                        (webRequest.responseCode == 401 || webRequest.responseCode == 403
                            ? ": read access denied. Check rules for this config node."
                            : ": request failed."));

                var result = ParseResponse(webRequest.downloadHandler.text);
                Trace("Firebase DB config", result.Kind == ConfigResultKind.Success
                    ? "enabled=true; url=" + WebWrapperDiagnostics.SafeUrl(result.Url)
                    : result.Message);
                return result;
            }
        }

        public static bool TryBuildEndpoint(string databaseUrl, string path, out Uri endpoint)
        {
            endpoint = null;
            if (!Uri.TryCreate(databaseUrl?.Trim(), UriKind.Absolute, out var root) ||
                root.Scheme != Uri.UriSchemeHttps || !root.IsDefaultPort ||
                !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) ||
                !string.IsNullOrEmpty(root.Fragment) || root.AbsolutePath != "/" ||
                !(root.Host.EndsWith(".firebaseio.com", StringComparison.OrdinalIgnoreCase) ||
                  root.Host.EndsWith(".firebasedatabase.app", StringComparison.OrdinalIgnoreCase)))
                return false;

            var node = path?.Trim().Trim('/');
            if (string.IsNullOrEmpty(node) || node.Length > 512) return false;
            foreach (var segment in node.Split('/'))
                if (!Regex.IsMatch(segment, "\\A[A-Za-z0-9_-]+\\z")) return false;
            endpoint = new Uri(root, node + ".json");
            return true;
        }

        public static ConfigResult ParseResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                return ConfigResult.Failure("Firebase DB config node is empty; playing the game for this session.");
            if (json.Length > 16384 || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return ConfigResult.Failure("Firebase DB config must be a small JSON object with enabled and url.");
            try
            {
                var config = JsonUtility.FromJson<ConfigDto>(json);
                if (config == null) return ConfigResult.Failure("Firebase DB config is missing.");
                if (!config.enabled) return ConfigResult.Negative("Firebase DB WebView is disabled; playing the game for this session.");
                if (!Uri.TryCreate(config.url?.Trim(), UriKind.Absolute, out var url) ||
                    url.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(url.Host) ||
                    !string.IsNullOrEmpty(url.UserInfo))
                    return ConfigResult.Failure("Firebase DB has no valid HTTPS WebView URL.");
                return ConfigResult.Success(url.AbsoluteUri, 0);
            }
            catch (Exception)
            {

                return ConfigResult.Failure("Firebase DB config is not valid JSON.");
            }
        }

        private static void Trace(string category, string message)
        {
            WebWrapperDiagnostics.Record(category, message);
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace(category + ": " + message);
#endif
        }
    }
}
