using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public sealed class PushOpenProcessor
    {
        private const int MaxHandledMessageIds = 100;
        private readonly IInstallStateStore _stateStore;
        private readonly IInteractionCallbackClient _callbackClient;
        private readonly SemaphoreSlim _deliveryGate = new SemaphoreSlim(1, 1);

        public PushOpenProcessor(IInstallStateStore stateStore, IInteractionCallbackClient callbackClient)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));

            _callbackClient = callbackClient;
        }

        public PushOpenResult Enqueue(PushOpenMessage message, string appsFlyerId = "")
        {
            if (message == null || string.IsNullOrWhiteSpace(message.MessageId)) return PushOpenResult.Ignored();
            if (_callbackClient == null) return EnqueueWithoutCallback(message);
            var duplicate = false;
            var delivered = false;
            _stateStore.Update(state =>
            {
                EnsureCollections(state);
                delivered = state.HandledPushMessageIds.Contains(message.MessageId);
                var pending = state.PendingInteractionCallbacks.FirstOrDefault(p => p.MessageId == message.MessageId);
                duplicate = delivered || pending != null;
                if (delivered) return;
                if (pending == null)
                {
                    pending = new PendingInteractionCallback { MessageId = message.MessageId };
                    state.PendingInteractionCallbacks.Add(pending);
                }
                if (!string.IsNullOrWhiteSpace(appsFlyerId)) pending.AppsFlyerId = appsFlyerId;
            });
            if (duplicate) return PushOpenResult.Duplicate(delivered);
            return IsHttpUrl(message.Url) ? PushOpenResult.OpenWebView(message.Url, false) : PushOpenResult.Ignored();
        }

        public async Task<PushOpenResult> ProcessAsync(PushOpenMessage message, string appsFlyerId, CancellationToken cancellationToken)
        {
            var result = Enqueue(message, appsFlyerId);
            if (_callbackClient == null) return result;
            if (message == null || string.IsNullOrWhiteSpace(message.MessageId)) return result;
            await _deliveryGate.WaitAsync(cancellationToken);
            bool delivered;
            try { delivered = await DeliverAsync(message.MessageId, cancellationToken); }
            finally { _deliveryGate.Release(); }
            if (result.Kind == PushOpenResultKind.OpenWebView) return PushOpenResult.OpenWebView(result.Url, delivered);
            return result.Kind == PushOpenResultKind.Duplicate ? PushOpenResult.Duplicate(delivered) : result;
        }

        public async Task<int> FlushPendingAsync(CancellationToken cancellationToken, string appsFlyerId = "")
        {
            if (_callbackClient == null) return 0;
            await _deliveryGate.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrWhiteSpace(appsFlyerId))
                {
                    _stateStore.Update(state =>
                    {
                        EnsureCollections(state);
                        foreach (var pending in state.PendingInteractionCallbacks)
                            if (string.IsNullOrWhiteSpace(pending.AppsFlyerId)) pending.AppsFlyerId = appsFlyerId;
                    });
                }
                var state = _stateStore.Load() ?? new InstallState();
                EnsureCollections(state);
                var ids = state.PendingInteractionCallbacks.Select(p => p.MessageId).ToArray();
                var count = 0;
                foreach (var id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await DeliverAsync(id, cancellationToken)) count++;
                }
                return count;
            }
            finally { _deliveryGate.Release(); }
        }

        private async Task<bool> DeliverAsync(string messageId, CancellationToken cancellationToken)
        {
            var state = _stateStore.Load() ?? new InstallState();
            EnsureCollections(state);
            if (state.HandledPushMessageIds.Contains(messageId)) return true;
            var pending = state.PendingInteractionCallbacks.FirstOrDefault(p => p.MessageId == messageId);
            if (pending == null || string.IsNullOrWhiteSpace(pending.AppsFlyerId)) return false;
            try
            {
                if (!await _callbackClient.SendAsync(messageId, pending.AppsFlyerId, cancellationToken)) return false;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
            _stateStore.Update(current =>
            {
                EnsureCollections(current);
                current.PendingInteractionCallbacks.RemoveAll(p => p.MessageId == messageId);
                if (!current.HandledPushMessageIds.Contains(messageId)) current.HandledPushMessageIds.Add(messageId);
                while (current.HandledPushMessageIds.Count > MaxHandledMessageIds) current.HandledPushMessageIds.RemoveAt(0);
            });
            return true;
        }

        private static void EnsureCollections(InstallState state)
        {
            state.HandledPushMessageIds ??= new System.Collections.Generic.List<string>();
            state.PendingInteractionCallbacks ??= new System.Collections.Generic.List<PendingInteractionCallback>();
        }

        private PushOpenResult EnqueueWithoutCallback(PushOpenMessage message)
        {
            var duplicate = false;
            _stateStore.Update(state =>
            {
                EnsureCollections(state);
                duplicate = state.HandledPushMessageIds.Contains(message.MessageId);
                if (!duplicate) state.HandledPushMessageIds.Add(message.MessageId);
                while (state.HandledPushMessageIds.Count > MaxHandledMessageIds) state.HandledPushMessageIds.RemoveAt(0);
            });
            if (duplicate) return PushOpenResult.Duplicate(false);
            return IsHttpUrl(message.Url) ? PushOpenResult.OpenWebView(message.Url, false) : PushOpenResult.Ignored();
        }

        private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
