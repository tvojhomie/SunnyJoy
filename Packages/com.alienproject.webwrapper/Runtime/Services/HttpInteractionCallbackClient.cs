using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace AlienProject.WebWrapper
{
    public sealed class HttpInteractionCallbackClient : IInteractionCallbackClient
    {
        private readonly string _configEndpoint;
        private readonly int _timeoutSeconds;

        public HttpInteractionCallbackClient(string configEndpoint, int timeoutSeconds)
        {
            _configEndpoint = configEndpoint;
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public async Task<bool> SendAsync(
            string messageId,
            string appsFlyerId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(appsFlyerId))
            {
                return false;
            }

            var uri = InteractionCallbackRequestBuilder.BuildUri(_configEndpoint, messageId);
            var body = Encoding.UTF8.GetBytes(InteractionCallbackRequestBuilder.BuildBody(appsFlyerId));
            using (var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPUT))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = _timeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Content-Type", "application/json");
                await UnityWebRequestConfigClient.SendAsync(request, cancellationToken);
                return request.result == UnityWebRequest.Result.Success &&
                       request.responseCode >= 200 &&
                       request.responseCode < 300;
            }
        }
    }
}
