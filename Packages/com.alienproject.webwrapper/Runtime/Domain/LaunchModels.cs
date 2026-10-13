using System;
using System.Collections.Generic;

namespace AlienProject.WebWrapper
{
    public enum LaunchMode
    {
        Unknown = 0,
        WebView = 1,
        Wrapper = 2
    }

    public enum LaunchDecisionKind
    {
        WebView = 0,
        Wrapper = 1,
        Offline = 2,
        ConfigurationError = 3
    }

    public enum ConfigResultKind
    {
        Success = 0,
        Negative = 1,
        NetworkUnavailable = 2,
        Failure = 3
    }

    public enum WebWrapperMockScenario
    {
        NonOrganicSuccess = 0,
        OrganicRejected = 1,
        NoInternet = 2,
        ServerError = 3
    }

    public enum NotificationPermissionStatus
    {
        Unknown = 0,
        Granted = 1,
        DeniedCanAskAgain = 2,
        DeniedPermanently = 3
    }

    public enum PushOpenResultKind
    {
        Ignored = 0,
        OpenWebView = 1,
        Duplicate = 2
    }

    [Serializable]
    public sealed class PendingInteractionCallback
    {
        public string MessageId = string.Empty;
        public string AppsFlyerId = string.Empty;
    }

    [Serializable]
    public sealed class InstallState
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public LaunchMode Mode = LaunchMode.Unknown;
        public string CachedUrl = string.Empty;
        public long ExpiresUnixSeconds;
        public long NotificationPromptDeferredUntilUnixSeconds;
        public bool NotificationSystemPermissionDenied;
        public string LastRegisteredPushToken = string.Empty;
        public string LastRegisteredFirebaseProjectId = string.Empty;
        public List<string> HandledPushMessageIds = new List<string>();
        public List<PendingInteractionCallback> PendingInteractionCallbacks = new List<PendingInteractionCallback>();
    }

    public sealed class LaunchDecision
    {
        private LaunchDecision(LaunchDecisionKind kind, string url, string reason)
        {
            Kind = kind;
            Url = url ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public LaunchDecisionKind Kind { get; }
        public string Url { get; }
        public string Reason { get; }

        public static LaunchDecision ShowWebView(string url, string reason)
        {
            return new LaunchDecision(LaunchDecisionKind.WebView, url, reason);
        }

        public static LaunchDecision ShowWrapper(string reason)
        {
            return new LaunchDecision(LaunchDecisionKind.Wrapper, string.Empty, reason);
        }

        public static LaunchDecision ShowOffline(string reason)
        {
            return new LaunchDecision(LaunchDecisionKind.Offline, string.Empty, reason);
        }

        public static LaunchDecision ConfigurationError(string reason)
        {
            return new LaunchDecision(LaunchDecisionKind.ConfigurationError, string.Empty, reason);
        }
    }

    public sealed class ConfigResult
    {
        private ConfigResult(ConfigResultKind kind, string url, long expiresUnixSeconds, string message)
        {
            Kind = kind;
            Url = url ?? string.Empty;
            ExpiresUnixSeconds = expiresUnixSeconds;
            Message = message ?? string.Empty;
        }

        public ConfigResultKind Kind { get; }
        public string Url { get; }
        public long ExpiresUnixSeconds { get; }
        public string Message { get; }

        public static ConfigResult Success(string url, long expiresUnixSeconds)
        {
            return new ConfigResult(ConfigResultKind.Success, url, expiresUnixSeconds, string.Empty);
        }

        public static ConfigResult Negative(string message)
        {
            return new ConfigResult(ConfigResultKind.Negative, string.Empty, 0, message);
        }

        public static ConfigResult Failure(string message)
        {
            return new ConfigResult(ConfigResultKind.Failure, string.Empty, 0, message);
        }

        public static ConfigResult NetworkUnavailable(string message)
        {
            return new ConfigResult(ConfigResultKind.NetworkUnavailable, string.Empty, 0, message);
        }
    }

    public sealed class AttributionPayload
    {
        public AttributionPayload(IReadOnlyDictionary<string, object> values)
        {
            Values = values ?? new Dictionary<string, object>();
        }

        public IReadOnlyDictionary<string, object> Values { get; }
    }

    public sealed class AttributionSnapshot
    {
        public AttributionSnapshot(string appsFlyerId, IReadOnlyList<AttributionPayload> payloads)
        {
            AppsFlyerId = appsFlyerId ?? string.Empty;
            Payloads = payloads ?? Array.Empty<AttributionPayload>();
        }

        public string AppsFlyerId { get; }
        public IReadOnlyList<AttributionPayload> Payloads { get; }

        public static AttributionSnapshot Empty { get; } =
            new AttributionSnapshot(string.Empty, Array.Empty<AttributionPayload>());
    }

    public sealed class PushRegistration
    {
        public PushRegistration(string token, string firebaseProjectId)
        {
            Token = token ?? string.Empty;
            FirebaseProjectId = firebaseProjectId ?? string.Empty;
        }

        public string Token { get; }
        public string FirebaseProjectId { get; }

        public bool IsComplete => !string.IsNullOrWhiteSpace(Token) &&
                                  !string.IsNullOrWhiteSpace(FirebaseProjectId);

        public static PushRegistration Empty { get; } = new PushRegistration(string.Empty, string.Empty);
    }

    public sealed class AppIdentity
    {
        public AppIdentity(string bundleId, string operatingSystem, string storeId, string locale)
        {
            BundleId = bundleId ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            StoreId = storeId ?? string.Empty;
            Locale = locale ?? string.Empty;
        }

        public string BundleId { get; }
        public string OperatingSystem { get; }
        public string StoreId { get; }
        public string Locale { get; }
    }

    public sealed class ConfigRequest
    {
        public ConfigRequest(IReadOnlyDictionary<string, object> values)
        {
            Values = values ?? new Dictionary<string, object>();
        }

        public IReadOnlyDictionary<string, object> Values { get; }
    }

    public sealed class PushOpenMessage
    {
        public PushOpenMessage(string messageId, string url)
        {
            MessageId = messageId ?? string.Empty;
            Url = url ?? string.Empty;
        }

        public string MessageId { get; }
        public string Url { get; }
    }

    public sealed class PushOpenResult
    {
        private PushOpenResult(PushOpenResultKind kind, string url, bool callbackDelivered)
        {
            Kind = kind;
            Url = url ?? string.Empty;
            CallbackDelivered = callbackDelivered;
        }

        public PushOpenResultKind Kind { get; }
        public string Url { get; }
        public bool CallbackDelivered { get; }

        public static PushOpenResult Ignored()
        {
            return new PushOpenResult(PushOpenResultKind.Ignored, string.Empty, false);
        }

        public static PushOpenResult Duplicate(bool callbackDelivered = true)
        {
            return new PushOpenResult(PushOpenResultKind.Duplicate, string.Empty, callbackDelivered);
        }

        public static PushOpenResult OpenWebView(string url, bool callbackDelivered)
        {
            return new PushOpenResult(PushOpenResultKind.OpenWebView, url, callbackDelivered);
        }
    }
}
