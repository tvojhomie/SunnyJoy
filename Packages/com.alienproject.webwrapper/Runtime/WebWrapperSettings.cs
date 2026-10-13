using UnityEngine;

namespace AlienProject.WebWrapper
{
    public enum RemoteConfigSource
    {
        HttpConfig = 0,
        FirebaseRealtimeDatabase = 1
    }

    [CreateAssetMenu(fileName = ResourceName, menuName = "Alien Project/Web Wrapper Settings")]
    public sealed class WebWrapperSettings : ScriptableObject
    {
        public const string ResourceName = "WebWrapperSettings";

        [Header("Runtime")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool webViewEnabled = true;
        [SerializeField] private bool useMockServices;
        [SerializeField] private bool verboseLogging;
        [SerializeField, HideInInspector] private string sourceSceneGuid;

        [Header("Remote configuration")]
        [SerializeField] private RemoteConfigSource configSource = RemoteConfigSource.FirebaseRealtimeDatabase;
        [Tooltip("Used only in HttpConfig mode. Firebase mode never posts to this endpoint.")]
        [SerializeField] private string configEndpoint = string.Empty;
        [Tooltip("HTTPS Realtime Database root URL, without credentials or .json.")]
        [SerializeField] private string firebaseDatabaseUrl = string.Empty;
        [Tooltip("Public read-only config node containing enabled and url. Use a different node for each game.")]
        [SerializeField] private string firebaseConfigPath = string.Empty;
        [Tooltip("Config request timeout. Zero waits for the server response or a transport error without a local startup deadline.")]
        [SerializeField, Min(0)] private int requestTimeoutSeconds;
        [SerializeField] private string androidStoreId = string.Empty;
        [SerializeField] private string iosStoreId = string.Empty;

        [Header("AppsFlyer")]
        [SerializeField] private string appsFlyerDevKey = string.Empty;
        [SerializeField] private string appsFlyerIosAppId = string.Empty;
        [SerializeField] private bool appsFlyerDebugLogging;
        [SerializeField, Min(1)] private int appsFlyerTimeoutSeconds = 7;
        [SerializeField, Range(0, 3000)] private int deepLinkGraceMilliseconds = 750;

        [Header("OneLink (iOS)")]
        [Tooltip("OneLink host only, for example yourapp.onelink.me. Configure its matching Apple prefix/bundle ID in AppsFlyer.")]
        [SerializeField] private string oneLinkDomain = string.Empty;
        [Tooltip("Custom URI scheme without ://. Keep it identical to the OneLink template.")]
        [SerializeField] private string iosUrlScheme = string.Empty;

        [Header("Firebase Messaging")]
        [SerializeField, Min(1)] private int firebaseTimeoutSeconds = 8;
        [SerializeField] private string notificationChannelId = "webwrapper_messages";
        [SerializeField] private string notificationChannelName = "Messages";
        [SerializeField] private string notificationChannelDescription = "Remote messages and updates";
        [SerializeField] private Color notificationAccentColor = new Color32(55, 120, 246, 255);

        [Header("Notification permission prompt")]
        [SerializeField] private string notificationPromptTitle = "Stay up to date";
        [SerializeField, TextArea(2, 5)] private string notificationPromptMessage =
            "Allow notifications so you don't miss important updates.";
        [SerializeField] private string notificationAllowButton = "Allow";
        [SerializeField] private string notificationSkipButton = "Not now";
        [SerializeField] private string notificationPrivacyButton = "Privacy Policy";
        [SerializeField] private Texture2D notificationPromptIcon;
        [SerializeField] private Texture2D notificationPromptImage;

        [Header("iOS WebView permissions")]
        [SerializeField, TextArea(2, 4)] private string webViewCameraUsageDescription =
            "Camera access is used to upload a photo when you choose to do so.";
        [SerializeField, TextArea(2, 4)] private string webViewPhotoLibraryUsageDescription =
            "Photo library access is used to upload an image when you choose to do so.";

        [Header("Host project")]
        [SerializeField] private string wrapperSceneName = string.Empty;
        [SerializeField] private string privacyPolicyUrl = string.Empty;
        [SerializeField, Min(3)] private int notificationPromptCooldownDays = 3;

        [Header("Loading and offline screen")]
        [SerializeField] private string loadingTitle = "Loading";
        [SerializeField] private string offlineTitle = "No internet connection";
        [SerializeField, TextArea(2, 4)] private string offlineMessage =
            "Check your connection and try again.";
        [SerializeField] private string errorTitle = "Something went wrong";
        [SerializeField] private string retryButton = "Try again";
        [SerializeField] private Texture2D loadingLogo;
        [SerializeField] private Color loadingBackgroundColor = new Color32(18, 22, 31, 255);
        [SerializeField] private Color loadingTextColor = Color.white;

        [Header("Mock test harness")]
        [SerializeField] private WebWrapperMockScenario mockScenario = WebWrapperMockScenario.NonOrganicSuccess;
        [SerializeField] private string mockWebUrl = string.Empty;
        [SerializeField, Min(1)] private int mockUrlLifetimeSeconds = 3600;
        [SerializeField] private bool previewMockWebViewInEditor;

        public bool AutoStart => autoStart;
        public bool WebViewEnabled => webViewEnabled;
        public string SourceSceneGuid => sourceSceneGuid;

        public void ConfigureFromApp(WebWrapperApp app, string sceneGuid)
        {
            autoStart = true;
            webViewEnabled = app.WebViewEnabled;
            useMockServices = verboseLogging = appsFlyerDebugLogging = previewMockWebViewInEditor = false;
            sourceSceneGuid = sceneGuid;
            configSource = RemoteConfigSource.FirebaseRealtimeDatabase;
            requestTimeoutSeconds = 0;
            configEndpoint = mockWebUrl = wrapperSceneName = string.Empty;
            firebaseDatabaseUrl = app.FirebaseDatabaseUrl.Trim().TrimEnd('/');
            firebaseConfigPath = app.FirebaseConfigPath.Trim().Trim('/');
            androidStoreId = app.BundleIdentifier.Trim();
            var appleId = app.AppleAppId.Trim();
            if (appleId.StartsWith("id", System.StringComparison.OrdinalIgnoreCase)) appleId = appleId.Substring(2);
            iosStoreId = appsFlyerIosAppId = appleId;
            appsFlyerDevKey = app.AppsFlyerDevKey.Trim();
            oneLinkDomain = app.OneLinkDomain.Trim();
            iosUrlScheme = app.IosUrlScheme.Trim();
            privacyPolicyUrl = app.PrivacyPolicyUrl.Trim();
            notificationChannelName = string.IsNullOrWhiteSpace(app.ApplicationName) ? "Messages" : app.ApplicationName.Trim();
        }
        public bool UseMockServices => useMockServices;
        public bool VerboseLogging => verboseLogging;
        public RemoteConfigSource ConfigSource => configSource;
        public bool UsesFirebaseDatabase => configSource == RemoteConfigSource.FirebaseRealtimeDatabase;
        public string FirebaseDatabaseUrl => firebaseDatabaseUrl;
        public string FirebaseConfigPath => firebaseConfigPath;
        public string ActiveConfigAddress => UsesFirebaseDatabase
            ? firebaseDatabaseUrl.TrimEnd('/') + "/" + firebaseConfigPath.Trim('/') + ".json"
            : configEndpoint;
        public string ConfigEndpoint => configEndpoint;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;
        public int BackgroundRequestTimeoutSeconds => Mathf.Max(8, requestTimeoutSeconds);
        public string AndroidStoreId => androidStoreId;
        public string IosStoreId => iosStoreId;
        public string AppsFlyerDevKey => appsFlyerDevKey;
        public string AppsFlyerIosAppId => appsFlyerIosAppId;
        public bool AppsFlyerDebugLogging => appsFlyerDebugLogging;
        public int AppsFlyerTimeoutSeconds => appsFlyerTimeoutSeconds;
        public int DeepLinkGraceMilliseconds => deepLinkGraceMilliseconds;
        public string OneLinkDomain => oneLinkDomain;
        public string IosUrlScheme => iosUrlScheme;
        public int FirebaseTimeoutSeconds => firebaseTimeoutSeconds;
        public string NotificationChannelId => notificationChannelId;
        public string NotificationChannelName => notificationChannelName;
        public string NotificationChannelDescription => notificationChannelDescription;
        public Color NotificationAccentColor => notificationAccentColor;
        public string NotificationPromptTitle => notificationPromptTitle;
        public string NotificationPromptMessage => notificationPromptMessage;
        public string NotificationAllowButton => notificationAllowButton;
        public string NotificationSkipButton => notificationSkipButton;
        public string NotificationPrivacyButton => notificationPrivacyButton;
        public Texture2D NotificationPromptIcon => notificationPromptIcon;
        public Texture2D NotificationPromptImage => notificationPromptImage;
        public string WebViewCameraUsageDescription => webViewCameraUsageDescription;
        public string WebViewPhotoLibraryUsageDescription => webViewPhotoLibraryUsageDescription;
        public string WrapperSceneName => wrapperSceneName;
        public string PrivacyPolicyUrl => privacyPolicyUrl;
        public int NotificationPromptCooldownDays => Mathf.Max(3, notificationPromptCooldownDays);
        public string LoadingTitle => loadingTitle;
        public string OfflineTitle => offlineTitle;
        public string OfflineMessage => offlineMessage;
        public string ErrorTitle => errorTitle;
        public string RetryButton => retryButton;
        public Texture2D LoadingLogo => loadingLogo;
        public Color LoadingBackgroundColor => loadingBackgroundColor;
        public Color LoadingTextColor => loadingTextColor;
        public WebWrapperMockScenario MockScenario => mockScenario;
        public string MockWebUrl => mockWebUrl;
        public int MockUrlLifetimeSeconds => mockUrlLifetimeSeconds;
        public bool PreviewMockWebViewInEditor => previewMockWebViewInEditor;
    }
}
