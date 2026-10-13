using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AlienProject.WebWrapper
{
    public sealed class UnityWebRequestConfigClient : IConfigClient
    {
        [Serializable]
        private sealed class ConfigResponseDto
        {
            public bool ok;
            public string url;
            public long expires;
            public string message;
        }

        private readonly string _endpoint;
        private readonly int _timeoutSeconds;
        private readonly IConnectivityService _connectivity;

        public UnityWebRequestConfigClient(string endpoint, int timeoutSeconds, IConnectivityService connectivity = null)
        {
            _endpoint = endpoint;
            _timeoutSeconds = Math.Max(0, timeoutSeconds);
            _connectivity = connectivity ?? new UnityConnectivityService();
        }

        public async Task<ConfigResult> FetchAsync(
            ConfigRequest request,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                return ConfigResult.Failure("The config endpoint is not a valid HTTP(S) URL.");
            }

            var body = Encoding.UTF8.GetBytes(JsonObjectWriter.Serialize(request.Values));
            if (WebWrapperDiagnostics.Enabled)
                WebWrapperDiagnostics.Record("Config POST", WebWrapperDiagnostics.SafeUrl(_endpoint) + "\n" +
                    WebWrapperDiagnostics.DescribeRequest(request.Values));
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            using (var webRequest = new UnityWebRequest(endpointUri, UnityWebRequest.kHttpVerbPOST))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(body);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = _timeoutSeconds;
                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.SetRequestHeader("Content-Type", "application/json");

#if UNITY_EDITOR
                WebWrapperEditorLiveSession.RecordRequest(request, _endpoint);
                var started = System.Diagnostics.Stopwatch.StartNew();
#endif
                await SendAsync(webRequest, cancellationToken);
                WebWrapperDiagnostics.Record("Config response", "HTTP " + webRequest.responseCode + "; " +
                    webRequest.result + "; " + elapsed.ElapsedMilliseconds + " ms; " +
                    webRequest.downloadedBytes + " bytes; " + webRequest.error);
#if UNITY_EDITOR
                WebWrapperEditorLiveSession.Trace("Server response: HTTP " + webRequest.responseCode + ", " +
                    webRequest.result + ", " + started.ElapsedMilliseconds + " ms.");
#endif
                if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                {
                    return ClassifyConnectionFailure(_connectivity.IsInternetReachable, webRequest.error);
                }

                if (webRequest.responseCode != 200 ||
                    webRequest.result == UnityWebRequest.Result.ProtocolError ||
                    webRequest.result == UnityWebRequest.Result.DataProcessingError)
                {
                    return ConfigResult.Failure(
                        $"Config request failed with HTTP {webRequest.responseCode}: {webRequest.error}");
                }

                ConfigResponseDto response;
                try
                {
                    response = JsonUtility.FromJson<ConfigResponseDto>(webRequest.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    WebWrapperDiagnostics.Record("Config JSON", "Invalid JSON. Raw body omitted for privacy. " + exception.Message);
                    return ConfigResult.Failure("Config response is not valid JSON: " + exception.Message);
                }

                if (WebWrapperDiagnostics.Enabled)
                    WebWrapperDiagnostics.Record("Config JSON", response == null ? "null response" :
                        "ok=" + response.ok + "; url=" + WebWrapperDiagnostics.SafeUrl(response.url) +
                        "; expires=" + response.expires + "; message=" + WebWrapperDiagnostics.Sanitize(response.message));
                if (response != null && response.ok && !string.IsNullOrWhiteSpace(response.url))
                {
#if UNITY_EDITOR
                    WebWrapperEditorLiveSession.Trace("JSON: ok=true; URL=" + WebWrapperEditorLiveSession.DescribeUrl(response.url) + "; expires=" + response.expires);
#endif
                    return ConfigResult.Success(response.url, response.expires);
                }

                if (response == null || response.ok)
                    return ConfigResult.Failure("Config response has no usable WebView URL.");
                return ConfigResult.Negative(response.message ?? "Config response rejected WebView mode.");
            }
        }

        public static ConfigResult ClassifyConnectionFailure(bool networkReachable, string error)
        {

            return networkReachable ? ConfigResult.Failure(error) : ConfigResult.NetworkUnavailable(error);
        }

        internal static Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var operation = request.SendWebRequest();
            if (operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            if (!cancellationToken.CanBeCanceled)
            {
                return completion.Task;
            }

            return AwaitWithCancellationAsync(request, completion.Task, cancellationToken);
        }

        private static async Task AwaitWithCancellationAsync(
            UnityWebRequest request,
            Task completion,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => request.Abort()))
            {
                await completion;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
