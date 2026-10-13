using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienProject.WebWrapper
{
    public sealed class WebWrapperController : MonoBehaviour
    {
        private WebWrapperSettings _settings;
        private PlayerPrefsInstallStateStore _stateStore;
        private CancellationTokenSource _cancellation;
        private LaunchDecision _decision;
        private string _status = "Preparing startup...";
        private bool _running;
        private bool _showOverlay = true;
        private bool _showNotificationPrompt;
        private bool _notificationPromptPending;
        private bool _requestingNotificationPermission;
        private INotificationPromptView _notificationPromptView;
        private ILoadingView _loadingView;
        private Vector2 _notificationScroll;
        private IWebViewAdapter _webView;
        private IPushOpenSource _pushOpenSource;
        private PushOpenProcessor _pushOpenProcessor;
        private NotificationFlowCoordinator _notificationFlow;
        private IPushRegistrationChangeSource _registrationChanges;
        private PushRegistrationSynchronizer _registrationSync;
        private IWebViewLifecycleSource _webViewLifecycle;
        private string _sessionPushUrl = string.Empty;
        private bool _webLoading;
        private bool _wasOnline;
        private bool _backgroundRunning;
        private bool _backgroundAgain;
        private float _nextBackgroundRetry;
        private CancellationTokenSource _startupCancellation;
        private bool _gameSessionLocked;

        public bool HasVisibleWebView => _decision?.Kind == LaunchDecisionKind.WebView && !_webLoading && !_showOverlay;

        public void ContinueInGameForSession()
        {
            if (_gameSessionLocked || HasVisibleWebView) return;
            _gameSessionLocked = true;
            _startupCancellation?.Cancel();
            if (_decision?.Kind != LaunchDecisionKind.Wrapper)
                FallBackToGame("Local startup is ready; keeping this session in the game.");
        }

        private async void Start()
        {
            DontDestroyOnLoad(gameObject);
            _settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            _stateStore = new PlayerPrefsInstallStateStore();
#if UNITY_EDITOR
            if (WebWrapperEditorLiveSession.IsEnabled) _stateStore = WebWrapperEditorLiveSession.CreateStateStore();
#endif
            _cancellation = new CancellationTokenSource();
            _wasOnline = Application.internetReachability != NetworkReachability.NotReachable;
            try
            {
                ConfigureNotificationServices();
                await RunAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FallBackToGame("Startup initialization failed: " + exception.Message);
            }
        }

        private void OnDestroy()
        {
            _showNotificationPrompt = false;
            WebWrapperLaunchState.Publish(null);
            HideNotificationPromptView();
            HideLoadingView();
            var cancellation = _cancellation;
            _cancellation = null;
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            _webView?.Hide();
            UnsubscribeWebView();
            if (_registrationChanges != null) _registrationChanges.RegistrationChanged -= OnRegistrationChanged;
            if (_pushOpenSource != null)
            {
                _pushOpenSource.Opened -= OnPushOpened;
            }
        }

        private void Update()
        {
            UpdatePendingNotificationPrompt();
            if (_showNotificationPrompt) UpdateNotificationPromptView();
            if (_settings == null || _settings.UseMockServices) return;
            var online = Application.internetReachability != NetworkReachability.NotReachable;
            if (online && (!_wasOnline || Time.realtimeSinceStartup >= _nextBackgroundRetry))
            {
                _nextBackgroundRetry = Time.realtimeSinceStartup + 30f;
                _ = RefreshBackgroundServicesAsync();
            }
            if (online && !_wasOnline && !_running && _decision?.Kind == LaunchDecisionKind.Offline)
                _ = RunAsync();
            _wasOnline = online;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && _cancellation != null) _ = RefreshBackgroundServicesAsync();
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            if (_running || _gameSessionLocked)
            {
                return;
            }

            _running = true;
            _notificationPromptPending = false;
            ContinueAfterNotificationPrompt();
            WebWrapperLaunchState.Publish(null);
            _decision = null;
            _showOverlay = true;
            _webView?.Hide();
            _webLoading = false;
            try
            {
                if (_settings == null)
                {
                    _status = "WebWrapperSettings was not found in a Resources folder.";
                    FallBackToGame(_status);
                    return;
                }

                if (!_settings.WebViewEnabled)
                {
                    _gameSessionLocked = true;
                    FallBackToGame("WebView is disabled in WebWrapper App.");
                    return;
                }

                _status = _settings.UseMockServices
                    ? "Resolving mock launch scenario..."
                    : "Resolving production launch scenario...";
                var clock = new SystemClock();
                var identity = CreateIdentity(_settings);
                WebWrapperDiagnostics.Record("Launch", "Starting; saved mode=" + _stateStore.Load().Mode +
                    "; network=" + Application.internetReachability + "; mock=" + _settings.UseMockServices);
#if UNITY_EDITOR
                WebWrapperEditorLiveSession.Trace("Starting launch coordinator. Saved mode: " + _stateStore.Load().Mode);
#endif
                var coordinator = CreateCoordinator(clock, identity);

                using (var startup = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token))
                {
                    _startupCancellation = startup;
                    var resolved = await coordinator.ResolveAsync(startup.Token);
                    if (_gameSessionLocked || startup.IsCancellationRequested) return;
                    _decision = resolved;
                }
#if UNITY_EDITOR
                if (_settings.UseMockServices &&
                    _settings.PreviewMockWebViewInEditor &&
                    _decision.Kind == LaunchDecisionKind.WebView &&
                    IsHttpUrl(_settings.MockWebUrl))
                {
                    _decision = LaunchDecision.ShowWebView(
                        _settings.MockWebUrl,
                        "Editor preview uses the currently configured mock URL.");
                }
#endif
                _status = _decision.Kind + ": " + _decision.Reason;
#if UNITY_EDITOR
                WebWrapperEditorLiveSession.Trace("Decision: " + _status);
#endif
                _decision = LaunchNavigationPolicy.Resolve(_decision, _sessionPushUrl,
                    Application.internetReachability != NetworkReachability.NotReachable);
                ApplyDecision(_decision);
                if (_settings.VerboseLogging)
                {
                    Debug.Log("[WebWrapper] " + _status);
                }
            }
            catch (OperationCanceledException)
            {
                if (_gameSessionLocked || _cancellation == null || _cancellation.IsCancellationRequested) return;
                FallBackToGame("Startup request was cancelled before receiving configuration.");
            }
            catch (Exception exception)
            {
                if (_gameSessionLocked) return;
                _status = "Unexpected startup error: " + exception.Message;
                Debug.LogException(exception);
                FallBackToGame(_status);
            }
            finally
            {
                _startupCancellation = null;
                _running = false;
                if (_cancellation != null && !_cancellation.IsCancellationRequested)
                    _ = RefreshBackgroundServicesAsync();
            }
        }

        private void LateUpdate() => UpdateLoadingView();

        private bool UpdateLoadingView()
        {
            var loading = _settings != null && !_settings.UseMockServices && _showOverlay &&
                !_showNotificationPrompt && (_running || _webLoading);
            var view = loading ? WebWrapperServices.LoadingView : null;
            if (view != null && !view.IsAvailable) view = null;
            if (!ReferenceEquals(view, _loadingView))
            {
                HideLoadingView();
                _loadingView = view;
                view?.Show();
            }
            return _loadingView != null;
        }

        private void HideLoadingView()
        {
            var view = _loadingView;
            _loadingView = null;
            view?.Hide();
        }

        private void OnGUI()
        {

            if (WebWrapperDiagnostics.ConsoleOpen) return;
            var hasCustomLoadingView = UpdateLoadingView();
            if (_settings == null)
            {
                return;
            }

            if (_showNotificationPrompt)
            {
                if (_notificationPromptView == null) DrawNotificationPrompt();
                return;
            }

            if (!_showOverlay) return;

            if (!_settings.UseMockServices)
            {
                if (!hasCustomLoadingView) DrawProductionStatusOverlay();
                return;
            }

            DrawMockStatusOverlay();
        }

        private void DrawMockStatusOverlay()
        {
            var width = Mathf.Min(620f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, 230f), GUI.skin.box);
            GUILayout.Label("Alien Project Web Wrapper — Mock Harness", GUI.skin.label);
            GUILayout.Space(8f);
            GUILayout.Label(_status);
            if (_decision != null && !string.IsNullOrWhiteSpace(_decision.Url))
            {
                GUILayout.Label("URL: " + _decision.Url);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !_running;
            if (GUILayout.Button("Retry"))
            {
                _ = RunAsync();
            }

            if (GUILayout.Button("Reset install state"))
            {
                _stateStore.Clear();
                _status = "Install state cleared. Press Retry.";
                _decision = null;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawProductionStatusOverlay()
        {
            var previousColor = GUI.color;
            GUI.color = _settings.LoadingBackgroundColor;
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height - WebWrapperDiagnostics.ToolbarPixels -
                    (WebWrapperDiagnostics.Enabled ? Screen.safeArea.yMin : 0f)),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = previousColor;

            var safeArea = ToGuiSafeArea();
            var contentWidth = Mathf.Min(520f, safeArea.width - 32f);
            var contentHeight = Mathf.Min(440f, safeArea.height - 32f);
            var content = new Rect(
                safeArea.x + (safeArea.width - contentWidth) * 0.5f,
                safeArea.y + (safeArea.height - contentHeight) * 0.5f,
                contentWidth,
                contentHeight);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.dpi / 8f), 24, 38),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.dpi / 12f), 16, 24),
                wordWrap = true
            };
            titleStyle.normal.textColor = _settings.LoadingTextColor;
            bodyStyle.normal.textColor = _settings.LoadingTextColor;

            GUILayout.BeginArea(content);
            GUILayout.FlexibleSpace();
            if (_settings.LoadingLogo != null)
            {
                GUILayout.Box(
                    _settings.LoadingLogo,
                    GUIStyle.none,
                    GUILayout.Height(Mathf.Min(128f, contentHeight * 0.32f)),
                    GUILayout.ExpandWidth(true));
                GUILayout.Space(20f);
            }

            GUILayout.Label(_settings.LoadingTitle, titleStyle);
            var dotCount = 1 + Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 3;
            GUILayout.Label(new string('•', dotCount), bodyStyle, GUILayout.Height(36f));

            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawNotificationPrompt()
        {
            var safeArea = ToGuiSafeArea();
            var width = Mathf.Min(520f, safeArea.width - 32f);
            var height = Mathf.Min(520f, safeArea.height - 32f);
            var left = safeArea.x + (safeArea.width - width) * 0.5f;
            var top = safeArea.y + (safeArea.height - height) * 0.5f;
            GUILayout.BeginArea(new Rect(left, top, width, height), GUI.skin.window);
            _notificationScroll = GUILayout.BeginScrollView(_notificationScroll);

            if (_settings.NotificationPromptIcon != null)
            {
                GUILayout.Box(
                    _settings.NotificationPromptIcon,
                    GUIStyle.none,
                    GUILayout.Height(72f),
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.Space(8f);
            GUILayout.Label(_settings.NotificationPromptTitle, GUI.skin.label);
            GUILayout.Space(8f);
            GUILayout.Label(_settings.NotificationPromptMessage, GUI.skin.label);

            if (_settings.NotificationPromptImage != null)
            {
                GUILayout.Space(12f);
                GUILayout.Box(
                    _settings.NotificationPromptImage,
                    GUIStyle.none,
                    GUILayout.Height(180f),
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = !_requestingNotificationPermission;
            if (GUILayout.Button(
                    _requestingNotificationPermission
                        ? "Requesting..."
                        : _settings.NotificationAllowButton,
                    GUILayout.Height(44f)))
            {
                RequestNotificationPermission();
            }

            if (GUILayout.Button(_settings.NotificationSkipButton, GUILayout.Height(40f)))
            {
                SkipNotificationPermission();
            }

            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private ILaunchCoordinator CreateCoordinator(IClock clock, AppIdentity identity)
        {
            if (_settings.UseMockServices)
            {
                _webView = _settings.PreviewMockWebViewInEditor
                    ? WebWrapperServices.WebViewAdapter
                    : null;
                return new LaunchCoordinator(
                    _stateStore,
                    new MockConnectivityService(_settings.MockScenario),
                    new MockAttributionProvider(_settings.MockScenario),
                    new MockPushRegistrationProvider(),
                    new MockConfigClient(
                        _settings.MockScenario,
                        _settings.MockWebUrl,
                        _settings.MockUrlLifetimeSeconds,
                        clock),
                    clock,
                    identity);
            }

            if (_settings.UsesFirebaseDatabase)
            {
                _webView = WebWrapperServices.WebViewAdapter;
                return new FirebaseDatabaseLaunchCoordinator(new UnityConnectivityService(),
                    new FirebaseDatabaseConfigClient(_settings.FirebaseDatabaseUrl,
                        _settings.FirebaseConfigPath, _settings.RequestTimeoutSeconds));
            }

            var attribution = WebWrapperServices.AttributionProvider;
            if (attribution == null)
            {
                throw new InvalidOperationException(
#if UNITY_EDITOR
                    "The mobile AppsFlyer SDK is unavailable in Unity Editor. " +
                    "For real HTTP tests, use Tools > Alien Project > Test Real Server in Editor. " +
                    "Mobile attribution and notification permission will be explicitly simulated.");
#else
                    "No production attribution provider is registered. " +
                    "Register an AppsFlyer adapter before the first scene loads.");
#endif
            }

            _webView = WebWrapperServices.WebViewAdapter;
            return new LaunchCoordinator(
                _stateStore,
                new UnityConnectivityService(),
                attribution,
                WebWrapperServices.PushRegistrationProvider ?? new EmptyPushRegistrationProvider(),
                new UnityWebRequestConfigClient(
                    _settings.ConfigEndpoint,
                    _settings.RequestTimeoutSeconds),
                clock,
                identity);
        }

        private void ConfigureNotificationServices()
        {
            if (_settings == null || _settings.UseMockServices)
            {
                return;
            }

            var permissionProvider = WebWrapperServices.NotificationPermissionProvider;
            if (permissionProvider != null)
            {
                _notificationFlow = new NotificationFlowCoordinator(
                    _stateStore,
                    permissionProvider,
                    new SystemClock(),
                    _settings.NotificationPromptCooldownDays);
            }

            _pushOpenSource = WebWrapperServices.PushOpenSource;
            if (_pushOpenSource != null)
            {
                _pushOpenProcessor = new PushOpenProcessor(
                    _stateStore,
                    _settings.UsesFirebaseDatabase ? null : new HttpInteractionCallbackClient(
                        _settings.ConfigEndpoint,
                        _settings.BackgroundRequestTimeoutSeconds));
                _pushOpenSource.Opened += OnPushOpened;
            }
            if (!_settings.UsesFirebaseDatabase && WebWrapperServices.AttributionProvider != null)
            {
                _registrationSync = new PushRegistrationSynchronizer(_stateStore,
                    new UnityWebRequestConfigClient(_settings.ConfigEndpoint, _settings.BackgroundRequestTimeoutSeconds),
                    WebWrapperServices.AttributionProvider, new UnityConnectivityService(), CreateIdentity(_settings));
            }
            _registrationChanges = WebWrapperServices.PushRegistrationProvider as IPushRegistrationChangeSource;
            if (_registrationChanges != null) _registrationChanges.RegistrationChanged += OnRegistrationChanged;
        }

        private void UpdatePendingNotificationPrompt()
        {
            if (!_notificationPromptPending || _running || _showOverlay || !WebWrapperLaunchState.IsGameReady) return;
            _notificationPromptPending = false;
            TryShowNotificationPrompt();
        }

        private bool TryShowNotificationPrompt()
        {
            if (_settings == null || _settings.UseMockServices || _showNotificationPrompt ||
                _decision == null ||
                _decision.Kind != LaunchDecisionKind.Wrapper ||
                !WebWrapperLaunchState.IsGameReady ||
                _running || _showOverlay ||
                _notificationFlow == null ||
                !_notificationFlow.ShouldShowCustomPrompt())
            {
                return false;
            }

            _showNotificationPrompt = true;
            _status = "Waiting for notification preference.";
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("PushPanel: game menu is open; waiting for Yes / Skip. WebView launches never show this panel.");
#endif
            UpdateNotificationPromptView();
            return true;
        }

        private void UpdateNotificationPromptView()
        {
            var view = WebWrapperServices.NotificationPromptView;
            if (view != null && !view.IsAvailable) view = null;
            if (ReferenceEquals(view, _notificationPromptView)) return;
            HideNotificationPromptView();
            _notificationPromptView = view;
            if (view == null) return;
            view.Show(RequestNotificationPermission, SkipNotificationPermission);
            view.SetBusy(_requestingNotificationPermission);
        }

        private void HideNotificationPromptView()
        {
            var view = _notificationPromptView;
            _notificationPromptView = null;
            view?.Hide();
        }

        private void SkipNotificationPermission()
        {
            if (!_showNotificationPrompt || _requestingNotificationPermission || _notificationFlow == null) return;
            _notificationFlow.Skip();
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("PushPanel skipped; deferred for at least 3 days in isolated test state.");
#endif
            ContinueAfterNotificationPrompt();
        }

        private async void RequestNotificationPermission()
        {
            if (!_showNotificationPrompt || _requestingNotificationPermission || _notificationFlow == null || _cancellation == null)
            {
                return;
            }

            _requestingNotificationPermission = true;
            _notificationPromptView?.SetBusy(true);
            try
            {
                var status = await _notificationFlow.RequestSystemPermissionAsync(_cancellation.Token);
                _status = "Notification permission: " + status;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _status = "Notification permission request failed: " + exception.Message;
                Debug.LogWarning("[WebWrapper] " + _status);
            }
            finally
            {
                _requestingNotificationPermission = false;
            }

            if (this == null || _cancellation == null || _cancellation.IsCancellationRequested) return;
            ContinueAfterNotificationPrompt();
            _ = RefreshBackgroundServicesAsync();
        }

        private void ContinueAfterNotificationPrompt()
        {
            _showNotificationPrompt = false;
            HideNotificationPromptView();

        }

        private void OnPushOpened(PushOpenMessage message)
        {
            if (_pushOpenProcessor == null || _settings == null || _settings.UseMockServices)
            {
                return;
            }

            try
            {
                var result = _pushOpenProcessor.Enqueue(message);
                _ = RefreshBackgroundServicesAsync();
                if (result.Kind != PushOpenResultKind.OpenWebView)
                {
                    return;
                }

                _sessionPushUrl = result.Url;
                if (_gameSessionLocked || _running || _decision == null) return;
                _decision = LaunchNavigationPolicy.Resolve(_decision, _sessionPushUrl,
                    Application.internetReachability != NetworkReachability.NotReachable);
                ApplyDecision(_decision);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WebWrapper] Push open failed: " + exception.Message);
            }
        }

        private async System.Threading.Tasks.Task FlushPendingPushCallbacksAsync()
        {
            if (_pushOpenProcessor == null || _settings.UsesFirebaseDatabase)
            {
                return;
            }

            var appsFlyerId = await GetAppsFlyerIdAsync();
            if (string.IsNullOrWhiteSpace(appsFlyerId))
            {
                return;
            }

            await _pushOpenProcessor.FlushPendingAsync(_cancellation.Token, appsFlyerId);
        }

        private async System.Threading.Tasks.Task<string> GetAppsFlyerIdAsync()
        {
            var attribution = WebWrapperServices.AttributionProvider;
            if (attribution == null)
            {
                return string.Empty;
            }

            var snapshot = await attribution.GetSnapshotAsync(_cancellation.Token);
            return snapshot?.AppsFlyerId ?? string.Empty;
        }

        private void ApplyDecision(LaunchDecision decision)
        {
            if (_gameSessionLocked && decision.Kind != LaunchDecisionKind.Wrapper) return;
            if (_settings != null && !_settings.WebViewEnabled && decision.Kind == LaunchDecisionKind.WebView)
                decision = LaunchDecision.ShowWrapper("WebView is disabled in WebWrapper App.");
            var production = _settings == null || !_settings.UseMockServices;
            if (production && (decision.Kind == LaunchDecisionKind.Offline || decision.Kind == LaunchDecisionKind.ConfigurationError))
                decision = LaunchDecision.ShowWrapper("Session fallback: " + decision.Reason);
            if (production && decision.Kind == LaunchDecisionKind.WebView && (_webView == null || !_webView.IsAvailable))
                decision = LaunchDecision.ShowWrapper("Session fallback: WebView adapter is unavailable.");
            _decision = decision;
            WebWrapperDiagnostics.Record("Route", decision.Kind + ": " + decision.Reason);
            ContinueAfterNotificationPrompt();
            _notificationPromptPending = _settings != null && production && decision.Kind == LaunchDecisionKind.Wrapper;
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("Applying route: " + decision.Kind + ".");
#endif
            WebWrapperLaunchState.Publish(decision);
            if (!production)
            {
                if (_settings.PreviewMockWebViewInEditor &&
                    decision.Kind == LaunchDecisionKind.WebView)
                {
                    if (_webView == null || !_webView.IsAvailable)
                    {
                        _status = "Mock WebView preview is enabled, but UniWebView is unavailable.";
                        _showOverlay = true;
                        return;
                    }

                    ShowWebView(decision.Url);
                    return;
                }

                _showOverlay = true;
                return;
            }

            switch (decision.Kind)
            {
                case LaunchDecisionKind.WebView:
                    try { ShowWebView(decision.Url); }
                    catch (Exception exception) { FallBackToGame("WebView could not open: " + exception.Message); }
                    break;

                case LaunchDecisionKind.Wrapper:
                    Screen.orientation = ScreenOrientation.Portrait;
                    _webLoading = false;
                    UnsubscribeWebView();
                    try { _webView?.Hide(); }
                    catch (Exception exception) { WebWrapperDiagnostics.Record("WebView hide", exception.Message); }
                    _showOverlay = false;
                    LoadWrapperSceneIfNeeded();
                    break;

                default:
                    _webView?.Hide();
                    _showOverlay = true;
                    break;
            }
        }

        private void FallBackToGame(string reason)
        {
            _sessionPushUrl = string.Empty;
            _status = "Game fallback: " + reason;
            WebWrapperDiagnostics.Record("Fallback", reason);

            ApplyDecision(LaunchDecision.ShowWrapper(_status));
        }

        private void ShowWebView(string url)
        {
            WebWrapperDiagnostics.Record("WebView", "Opening " + WebWrapperDiagnostics.SafeUrl(url));
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("Opening REAL WebView: " + WebWrapperEditorLiveSession.DescribeUrl(url));
#endif
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            UnsubscribeWebView();
            _webView = WebWrapperServices.WebViewAdapter ?? _webView;
            _webViewLifecycle = _webView as IWebViewLifecycleSource;
            if (_webViewLifecycle != null)
            {
                _webViewLifecycle.LoadStarted += OnWebViewLoadStarted;
                _webViewLifecycle.LoadCompleted += OnWebViewLoadCompleted;
                _webViewLifecycle.LoadFailed += OnWebViewLoadFailed;
            }
            _webView.Show(url);
            if (_webViewLifecycle == null) _showOverlay = false;
        }

        private void UnsubscribeWebView()
        {
            if (_webViewLifecycle == null) return;
            _webViewLifecycle.LoadStarted -= OnWebViewLoadStarted;
            _webViewLifecycle.LoadCompleted -= OnWebViewLoadCompleted;
            _webViewLifecycle.LoadFailed -= OnWebViewLoadFailed;
            _webViewLifecycle = null;
        }

        private void OnWebViewLoadStarted()
        {
            WebWrapperDiagnostics.Record("WebView", "Page loading started.");
            _webLoading = true;
            _showOverlay = true;
        }

        private void OnWebViewLoadCompleted()
        {
            if (_gameSessionLocked) return;
            WebWrapperDiagnostics.Record("WebView", "Page loaded successfully.");
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("WebView page loaded. Real-server launch path completed.");
#endif
            _webLoading = false;
            _showOverlay = false;
        }

        private void OnWebViewLoadFailed(string error)
        {
            WebWrapperDiagnostics.Record("WebView ERROR", error);
#if UNITY_EDITOR
            WebWrapperEditorLiveSession.Trace("WebView load failed: " + error);
#endif
            _webLoading = false;
            FallBackToGame(string.IsNullOrWhiteSpace(error) ? "Web page loading failed." : error);
        }

        private void OnRegistrationChanged(PushRegistration registration)
        {
            _registrationSync?.Queue(registration);
            if (!_running) _ = RefreshBackgroundServicesAsync();
        }

        private async System.Threading.Tasks.Task RefreshBackgroundServicesAsync()
        {
            if (_settings == null || _settings.UseMockServices || _cancellation == null || _running) return;
            if (_backgroundRunning) { _backgroundAgain = true; return; }
            _backgroundRunning = true;
            try
            {
                do
                {
                    _backgroundAgain = false;
                    if (Application.internetReachability == NetworkReachability.NotReachable) return;
                    var callbacks = FlushPendingPushCallbacksAsync();
                    var registration = SynchronizeRegistrationAsync();
                    await System.Threading.Tasks.Task.WhenAll(callbacks, registration);
                } while (_backgroundAgain && !_cancellation.IsCancellationRequested);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogWarning("[WebWrapper] Background retry pending: " + exception.Message); }
            finally { _backgroundRunning = false; }
        }

        private async System.Threading.Tasks.Task SynchronizeRegistrationAsync()
        {
            var provider = WebWrapperServices.PushRegistrationProvider;
            if (_registrationSync == null || provider == null || _stateStore.Load().Mode != LaunchMode.WebView) return;
            _registrationSync.Queue(await provider.GetRegistrationAsync(_cancellation.Token));
            await _registrationSync.FlushAsync(_cancellation.Token);
        }

        private void LoadWrapperSceneIfNeeded()
        {
            var sceneName = _settings?.WrapperSceneName;
            if (string.IsNullOrWhiteSpace(sceneName) ||
                SceneManager.GetActiveScene().name == sceneName)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                _status = "ConfigurationError: Wrapper scene is not in Build Settings: " + sceneName;
                _showOverlay = true;
                return;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private static AppIdentity CreateIdentity(WebWrapperSettings settings)
        {
#if UNITY_EDITOR
            if (WebWrapperEditorLiveSession.IsEnabled) return WebWrapperEditorLiveSession.CreateIdentity(settings);
#endif
            var os = "Editor";
            var storeId = Application.identifier;
#if UNITY_ANDROID
            os = "Android";
            storeId = string.IsNullOrWhiteSpace(settings.AndroidStoreId)
                ? Application.identifier
                : settings.AndroidStoreId;
#elif UNITY_IOS
            os = "iOS";
            storeId = StoreIdNormalizer.NormalizeIos(settings.IosStoreId);
#endif
            return new AppIdentity(
                Application.identifier,
                os,
                storeId,
                CultureInfo.CurrentUICulture.Name);
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static Rect ToGuiSafeArea()
        {
            var safeArea = Screen.safeArea;
            return new Rect(
                safeArea.x,
                Screen.height - safeArea.yMax,
                safeArea.width,
                safeArea.height);
        }
    }
}
