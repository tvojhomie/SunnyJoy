#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AlienProject.WebWrapper
{

    public static class WebWrapperEditorLiveSession
    {
        public enum AttributionMode { NonOrganicQa, OrganicQa, NoNativeData }
        private const string Prefix = "AlienProject.WebWrapper.EditorLive.";
        private static EditorPermissionProvider permission;
        private static IPushRegistrationProvider push;
        public static bool IsEnabled => SessionState.GetBool(Prefix + "Enabled", false);
        public static string InstallationId => PlayerPrefs.GetString(Prefix + "LastInstall." + BundleId, string.Empty);
        public static string BundleId => PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
        public static string StateKey => Prefix + "State." + BundleId + "." + InstallationId;
        public static string Transcript => SessionState.GetString(Prefix + "Transcript", string.Empty);
        public static string RequestJson => SessionState.GetString(Prefix + "Request", string.Empty);
        public static bool PermissionPending => permission != null && permission.IsPending;
        public static AttributionMode Mode => (AttributionMode)SessionState.GetInt(Prefix + "Attribution", 0);

        public static void Prepare(bool fresh, AttributionMode mode)
        {
            if (fresh || string.IsNullOrEmpty(InstallationId))
            {
                PlayerPrefs.SetString(Prefix + "LastInstall." + BundleId, "unity-editor-" + Guid.NewGuid().ToString("N"));
                PlayerPrefs.Save();
            }
            SessionState.SetBool(Prefix + "Enabled", true);
            SessionState.SetInt(Prefix + "Attribution", (int)mode);
            SessionState.SetString(Prefix + "Transcript", string.Empty);
            SessionState.SetString(Prefix + "Request", string.Empty);
            WebWrapperBootstrap.SetEditorPreview(false);
            Trace(fresh ? "NEW editor test installation (isolated storage)." : "REPEAT editor test installation (cached state retained).");
            Trace("Real HTTP + routing + macOS WebView. Mobile SDK attribution/permissions are simulated; no APNs/FCM token.");
        }

        public static void End()
        {
            SessionState.SetBool(Prefix + "Enabled", false);
            if (WebWrapperServices.AttributionProvider is EditorAttributionProvider) WebWrapperServices.AttributionProvider = null;
            if (ReferenceEquals(WebWrapperServices.PushRegistrationProvider, push)) WebWrapperServices.PushRegistrationProvider = null;
            if (ReferenceEquals(WebWrapperServices.NotificationPermissionProvider, permission)) WebWrapperServices.NotificationPermissionProvider = null;
            permission = null;
            push = null;
        }

        public static PlayerPrefsInstallStateStore CreateStateStore() => new PlayerPrefsInstallStateStore(StateKey);

        public static AppIdentity CreateIdentity(WebWrapperSettings settings) => new AppIdentity(
            BundleId, "iOS", StoreIdNormalizer.NormalizeIos(settings.IosStoreId), CultureInfo.CurrentUICulture.Name);

        public static AttributionSnapshot CreateAttribution(AttributionMode mode, string installationId)
        {
            var fields = new Dictionary<string, object> { ["is_unity_editor"] = true };
            if (mode == AttributionMode.NoNativeData)
                return new AttributionSnapshot(string.Empty, new[] { new AttributionPayload(fields) });
            fields["af_status"] = mode == AttributionMode.NonOrganicQa ? "Non-organic" : "Organic";
            fields["media_source"] = "unity_editor";
            fields["campaign"] = "unity_editor_test";
            return new AttributionSnapshot(installationId, new[] { new AttributionPayload(fields) });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (!IsEnabled) return;
            WebWrapperServices.AttributionProvider = new EditorAttributionProvider();
            push = new EmptyPushRegistrationProvider();
            WebWrapperServices.PushRegistrationProvider = push;
            WebWrapperServices.PushOpenSource = null;
            permission = new EditorPermissionProvider(StateKey + ".Permission");
            WebWrapperServices.NotificationPermissionProvider = permission;
            Trace("Mobile boundary ready. Attribution: " + Mode + "; iOS bundle: " + BundleId);
        }

        public static void CompletePermission(bool allow) => permission?.Complete(allow);

        public static void Trace(string message)
        {
            if (!IsEnabled) return;
            var entry = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
            var text = Transcript + entry + "\n";
            if (text.Length > 16000) text = text.Substring(text.Length - 16000);
            SessionState.SetString(Prefix + "Transcript", text);
            Debug.Log("[WebWrapper.EditorLive] " + message);
        }

        public static void RecordRequest(ConfigRequest request, string endpoint)
        {
            if (!IsEnabled) return;
            SessionState.SetString(Prefix + "Request", JsonObjectWriter.Serialize(request.Values));
            Trace("POST " + DescribeUrl(endpoint) + " — waiting for the REAL server response...");
        }

        public static string DescribeUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path) : "(invalid URL)";

        public static Rect FitWebViewPreview(Rect source, Vector2 editorWindowSize)
        {
            if (source.width <= 0 || source.height <= 0) return new Rect(0, 0, 360, 640);
            float width = Mathf.Min(520, Mathf.Max(240, editorWindowSize.x - 80));
            float height = Mathf.Min(820, Mathf.Max(320, editorWindowSize.y - 100));
            float scale = Mathf.Min(1, width / source.width, height / source.height);
            return new Rect(0, 0, Mathf.Round(source.width * scale), Mathf.Round(source.height * scale));
        }

        private sealed class EditorAttributionProvider : IAttributionProvider
        {
            public Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Trace("Editor QA attribution prepared (not an AppsFlyer SDK callback).");
                return Task.FromResult(CreateAttribution(Mode, InstallationId));
            }
        }

        public sealed class EditorPermissionProvider : INotificationPermissionProvider
        {
            private readonly string key;
            private TaskCompletionSource<NotificationPermissionStatus> pending;
            public EditorPermissionProvider(string key) => this.key = key;
            public NotificationPermissionStatus Status => (NotificationPermissionStatus)PlayerPrefs.GetInt(key, 0);
            public bool IsPending => pending != null && !pending.Task.IsCompleted;

            public async Task<NotificationPermissionStatus> RequestAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (Status != NotificationPermissionStatus.Unknown) return Status;
                if (pending == null)
                {
                    pending = new TaskCompletionSource<NotificationPermissionStatus>();
                    Trace("SIMULATED iOS permission: choose Allow / Don't Allow in the Real Server Test window.");
                }
                using (token.Register(() => pending?.TrySetCanceled()))
                    return await pending.Task;
            }

            public void Complete(bool allow)
            {
                if (!IsPending) return;
                var status = allow ? NotificationPermissionStatus.Granted : NotificationPermissionStatus.DeniedPermanently;
                PlayerPrefs.SetInt(key, (int)status);
                PlayerPrefs.Save();
                Trace("SIMULATED notification permission: " + status + ". No OS permission was changed.");
                pending.TrySetResult(status);
            }
        }
    }
}
#endif
