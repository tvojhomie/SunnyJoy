using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AlienProject.WebWrapper.Integrations.UniWebView
{

    public sealed class ReflectionUniWebViewAdapter : MonoBehaviour, IWebViewAdapter, IWebViewLifecycleSource
    {
        private Type _webViewType;
        private Component _webView;
        private int _screenWidth;
        private int _screenHeight;
        private Rect _safeArea;
        private readonly List<KeyValuePair<EventInfo, Delegate>> _eventHandlers = new List<KeyValuePair<EventInfo, Delegate>>();
        private readonly WebViewRedirectRecovery _redirectRecovery = new WebViewRedirectRecovery();
        private string _lastStartedUrl;
        private bool _showRequested;
        private bool _loading;
        private bool _nativeVisible;
        private bool _diagnosticsHidden;
        private bool _ignoreCompletionUntilStart;
        private Coroutine _redirectRetry;
        private object _mediaPermissionPrompt;
        private object _mediaPermissionGrant;
        private bool _openDiagnosticsOnUpdate;
        private const string DiagnosticsToolbarIdentifier = "webwrapper.logs";
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private Type _keyboardType;
        private PropertyInfo _keyboardCurrent;
        private PropertyInfo _escapeKey;
        private PropertyInfo _escapePressed;
#endif

        public event Action LoadStarted;
        public event Action LoadCompleted;
        public event Action<string> LoadFailed;

        public bool IsAvailable
        {
            get
            {
                ResolveType();
                if (_webViewType == null)
                {
                    return false;
                }

                try
                {
                    var supported = _webViewType.GetProperty(
                        "IsWebViewSupported",
                        BindingFlags.Public | BindingFlags.Static);
                    return supported == null || (bool)supported.GetValue(null);
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool CanGoBack
        {
            get
            {
                if (_webView == null)
                {
                    return false;
                }

                try
                {
                    return (bool)_webViewType.GetProperty("CanGoBack")?.GetValue(_webView);
                }
                catch
                {
                    return false;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBeforeSceneLoad()
        {
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
#if UNITY_EDITOR
            if (settings == null ||
                !(WebWrapperEditorLiveSession.IsEnabled || !string.IsNullOrEmpty(settings.SourceSceneGuid) ||
                    (settings.UseMockServices && settings.PreviewMockWebViewInEditor)) ||
                FindUniWebViewType() == null)
            {
                return;
            }
#elif UNITY_ANDROID || UNITY_IOS
            if (settings == null || settings.UseMockServices || FindUniWebViewType() == null)
            {
                return;
            }
#else
            return;
#endif

            if (FindFirstObjectByType<ReflectionUniWebViewAdapter>() != null)
            {
                return;
            }

            var host = new GameObject("[WebWrapper.UniWebView]");
            DontDestroyOnLoad(host);
            host.AddComponent<ReflectionUniWebViewAdapter>();
        }

        private void Awake()
        {
            ResolveType();
            if (_webViewType == null)
            {
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            WebWrapperServices.WebViewAdapter = this;
        }

        private void Update()
        {
            if (_openDiagnosticsOnUpdate)
            {
                _openDiagnosticsOnUpdate = false;
                if (WebWrapperDiagnostics.Enabled) WebWrapperDiagnostics.ConsoleOpen = true;
            }
            UpdateDiagnosticsVisibility();
            if (_showRequested && _nativeVisible && !WebWrapperDiagnostics.ConsoleOpen && BackPressedThisFrame())
            {
                GoBack();
            }

            if (_webView == null ||
                (_screenWidth == Screen.width &&
                 _screenHeight == Screen.height &&
                 _safeArea == Screen.safeArea))
            {
                return;
            }

            ApplyViewportFrame();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _webView != null)
            {
                ApplyViewportFrame();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (!isPaused && _webView != null)
            {
                ApplyViewportFrame();
            }
        }

#if UNITY_EDITOR
        private void OnDisable()
        {

            DisposeWebView(true);
        }

        private void OnApplicationQuit()
        {
            DisposeWebView(true);
        }
#endif

        private void OnDestroy()
        {
#if UNITY_EDITOR
            DisposeWebView(true);
#else
            DisposeWebView(false);
#endif
            if (ReferenceEquals(WebWrapperServices.WebViewAdapter, this))
            {
                WebWrapperServices.WebViewAdapter = null;
            }
        }

        public void Show(string url)
        {
            if (!IsAvailable || !IsHttpUrl(url))
            {
                throw new InvalidOperationException("UniWebView is unavailable or the URL is invalid.");
            }

            EnsureWebView();
            ApplyViewportFrame();
            CancelRedirectRetry();
            _redirectRecovery.Reset();
            _lastStartedUrl = url;
            _showRequested = true;
            _nativeVisible = false;
            _loading = false;
            _ignoreCompletionUntilStart = false;

            TryInvokeOptional("Hide");
            TryInvokeOptional("SetShowSpinnerWhileLoading", true);
            TryInvokeOptional("SetBackButtonEnabled", false);
            BeginLoading();
            try
            {
                InvokeOptional("Load", url);
            }
            catch
            {
                FailLoading("The web page could not be opened.");
                throw;
            }
        }

        public void Hide()
        {
            _showRequested = false;
            _nativeVisible = false;
            _loading = false;
            CancelRedirectRetry();
            if (_webView != null)
            {
                TryInvokeOptional("SetBackButtonEnabled", false);
                TryInvokeOptional("Stop");
                TryInvokeOptional("Hide");
            }
        }

        public void GoBack()
        {
            if (_webView != null && CanGoBack)
            {
                TryInvokeOptional("GoBack");
            }
        }

        private void ResolveType()
        {
            _webViewType ??= FindUniWebViewType();
        }

        private void EnsureWebView()
        {
            if (_webView != null)
            {
                return;
            }

            TryInvokeStaticOptional("SetAllowAutoPlay", true);
            TryInvokeStaticOptional("SetAllowInlinePlay", true);
            TryInvokeStaticOptional("SetEnableKeyboardAvoidance", true);
            TryInvokeStaticOptional("SetJavaScriptEnabled", true);
            _webView = gameObject.GetComponent(_webViewType) ?? gameObject.AddComponent(_webViewType);

            TryInvokeOptional("SetBackButtonEnabled", false);
            SubscribeEvent("OnPageStarted", nameof(OnPageStarted));
            SubscribeEvent("OnPageFinished", nameof(OnPageFinished));
            SubscribeEvent("OnLoadingErrorReceived", nameof(OnLoadingErrorReceived));
            SubscribeEvent("OnShouldClose", nameof(OnShouldClose));
#if UNITY_ANDROID && !UNITY_EDITOR
            TrySetProperty("RestoreViewHierarchyOnResume", true);
#endif
            TryInvokeOptional("SetAcceptThirdPartyCookies", true);
            TryInvokeOptional("SetAutoDownloadEnabled", true);
            TryInvokeOptional("SetSupportMultipleWindows", false, false);
            TryInvokeOptional("SetAllowBackForwardNavigationGestures", true);
            ConfigureUserAgent();
            ConfigureMediaPermissions();
            ConfigureEmbeddedToolbar();
        }

        private void ConfigureEmbeddedToolbar()
        {
            try
            {
                var toolbar = _webViewType.GetProperty("EmbeddedToolbar")?.GetValue(_webView);
                if (toolbar == null)
                {
                    throw new MissingMemberException("UniWebView.EmbeddedToolbar");
                }

                if (!WebWrapperDiagnostics.Enabled)
                {
                    toolbar.GetType().GetMethod("Hide")?.Invoke(toolbar, null);
                    return;
                }

                var register = _webViewType.GetMethod("RegisterOnEmbeddedToolbarItemAction");
                if (register == null)
                {
                    throw new MissingMethodException("UniWebView.RegisterOnEmbeddedToolbarItemAction");
                }

                var handler = Delegate.CreateDelegate(register.GetParameters()[0].ParameterType, this,
                    GetType().GetMethod(nameof(OnToolbarAction), BindingFlags.NonPublic | BindingFlags.Instance));
                register.Invoke(_webView, new object[] { handler });

                var configType = _webViewType.Assembly.GetType("UniWebViewEmbeddedToolbarConfig", true);
                var rightItems = new List<object>();
                if (WebWrapperDiagnostics.Enabled) rightItems.Add(new Dictionary<string, object>
                    { ["type"] = "custom", ["identifier"] = DiagnosticsToolbarIdentifier, ["title"] = "Logs" });
                var config = configType.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static)?.Invoke(null,
                    new object[] { new Dictionary<string, object>
                    {
                        ["version"] = 1,
                        ["visible"] = true,
                        ["position"] = "bottom",
                        ["left"] = new List<object>
                        {
                            new Dictionary<string, object> { ["type"] = "builtIn", ["kind"] = "back", ["identifier"] = "uwv.toolbar.back" },
                            new Dictionary<string, object> { ["type"] = "builtIn", ["kind"] = "forward", ["identifier"] = "uwv.toolbar.forward" }
                        },
                        ["right"] = rightItems
                    } });
                if (config == null)
                {
                    throw new InvalidOperationException("UniWebView toolbar config could not be created.");
                }

                toolbar.GetType().GetMethod("ApplyConfig")?.Invoke(toolbar, new[] { config });
            }
            catch (Exception)
            {
                Debug.LogWarning("[WebWrapper.UniWebView] The native diagnostics toolbar requires UniWebView 6.10 or later.");
            }
        }

        private bool OnToolbarAction(object request)
        {
            var identifier = request?.GetType().GetProperty("Identifier")?.GetValue(request) as string;
            if (identifier == DiagnosticsToolbarIdentifier && WebWrapperDiagnostics.Enabled)
            {
                _openDiagnosticsOnUpdate = true;
                return true;
            }
            return false;
        }

        private void SubscribeEvent(string eventName, string handlerName)
        {
            var eventInfo = _webViewType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            if (eventInfo == null)
            {
                throw new MissingMemberException(_webViewType.FullName, eventName);
            }

            var method = GetType().GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, method);
            eventInfo.AddEventHandler(_webView, handler);
            _eventHandlers.Add(new KeyValuePair<EventInfo, Delegate>(eventInfo, handler));
        }

        private void OnPageStarted(object webView, string url)
        {
            if (!ReferenceEquals(webView, _webView) || !_showRequested || !IsHttpUrl(url))
            {
                return;
            }

            _lastStartedUrl = url;
            _ignoreCompletionUntilStart = false;

            BeginLoading();
        }

        private void OnPageFinished(object webView, int statusCode, string url)
        {
            if (!ReferenceEquals(webView, _webView) || !_showRequested || _ignoreCompletionUntilStart || !IsHttpUrl(url))
            {
                return;
            }

            if (statusCode >= 400)
            {
                FailLoading("The web server returned HTTP " + statusCode + ".");
                return;
            }

            _redirectRecovery.Reset();
            _loading = false;
            if (!WebWrapperDiagnostics.ConsoleOpen) TryInvokeOptional("Show");
            _diagnosticsHidden = WebWrapperDiagnostics.ConsoleOpen;
            _nativeVisible = true;
            LoadCompleted?.Invoke();
        }

        private void OnLoadingErrorReceived(object webView, int errorCode, string message, object payload)
        {
            if (!ReferenceEquals(webView, _webView) || !_showRequested)
            {
                return;
            }

            var extra = payload?.GetType().GetProperty("Extra")?.GetValue(payload) as IDictionary<string, object>;
            var failingUrl = extra != null && extra.TryGetValue("failingURL", out var value) ? value as string : null;
            if (_nativeVisible && WebViewNavigationPolicy.IsExternalNavigationError(errorCode, message, failingUrl))
            {
                _loading = false;
                _ignoreCompletionUntilStart = true;
                CancelRedirectRetry();
                _redirectRecovery.Reset();
                if (!WebWrapperDiagnostics.ConsoleOpen) TryInvokeOptional("Show");
                _diagnosticsHidden = WebWrapperDiagnostics.ConsoleOpen;
                WebWrapperDiagnostics.Record("WebView external link", "External app unavailable; keeping the current web page.");
                LoadCompleted?.Invoke();
                return;
            }

            if (errorCode == -999 ||
                (!string.IsNullOrEmpty(message) && message.IndexOf("ERR_ABORTED", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return;
            }

            _ignoreCompletionUntilStart = true;
            if (_redirectRecovery.TryGetRetryUrl(errorCode, message, failingUrl, _lastStartedUrl, out var retryUrl))
            {
                CancelRedirectRetry();
                _redirectRetry = StartCoroutine(RetryRedirectOnNextFrame(retryUrl));
                return;
            }

            FailLoading("The web page failed to load (" + errorCode + ").");
        }

        private bool OnShouldClose(object webView)
        {

            return false;
        }

        private bool BackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (_keyboardType == null)
            {
                _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem", false);
                _keyboardCurrent = _keyboardType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                _escapeKey = _keyboardType?.GetProperty("escapeKey", BindingFlags.Public | BindingFlags.Instance);
            }

            var keyboard = _keyboardCurrent?.GetValue(null);
            var escape = keyboard == null ? null : _escapeKey?.GetValue(keyboard);
            if (escape == null)
            {
                return false;
            }

            _escapePressed ??= escape.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
            return _escapePressed?.GetValue(escape) is bool pressed && pressed;
#elif ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            return Input.GetKeyUp(KeyCode.Escape);
#else
            return false;
#endif
        }

        private IEnumerator RetryRedirectOnNextFrame(string url)
        {
            yield return null;
            _redirectRetry = null;
            if (_webView == null || !_showRequested)
            {
                yield break;
            }

            try
            {
                InvokeOptional("Load", url);
            }
            catch (Exception)
            {
                FailLoading("The redirected web page could not be opened.");
            }
        }

        private void CancelRedirectRetry()
        {
            if (_redirectRetry != null)
            {
                StopCoroutine(_redirectRetry);
                _redirectRetry = null;
            }
        }

        private void BeginLoading()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            LoadStarted?.Invoke();
        }

        private void FailLoading(string message)
        {
            _loading = false;
            _nativeVisible = false;
            _ignoreCompletionUntilStart = true;
            TryInvokeOptional("Hide");
            LoadFailed?.Invoke(message);
        }

        private void ConfigureUserAgent()
        {
            try
            {
                var method = FindCallableMethod("GetUserAgent", Array.Empty<object>());
                var nativeAgent = method?.Invoke(_webView, Array.Empty<object>()) as string;
                var normalizedAgent = WebViewNavigationPolicy.NormalizeUserAgent(nativeAgent);
                if (!string.IsNullOrWhiteSpace(normalizedAgent) && normalizedAgent != nativeAgent)
                {
                    TryInvokeOptional("SetUserAgent", normalizedAgent);
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("[WebWrapper.UniWebView] Could not read the native user agent; retaining its default value.");
            }
        }

        private void ConfigureMediaPermissions()
        {
            try
            {

                var sdk = _webViewType.Assembly;
                if (sdk.GetName().Name != "UniWebView-CSharp" ||
                    _webViewType.GetMethod("RegisterOnRequestMediaCapturePermission") == null)
                {
                    throw new NotSupportedException("The installed SDK does not have the UniWebView 6 permission API.");
                }
                var managerType = sdk.GetType("UniWebViewChannelMethodManager", true);
                var channelType = sdk.GetType("UniWebViewChannelMethod", true);
                var decisionType = sdk.GetType("UniWebViewMediaCapturePermissionDecision", true);
                var manager = managerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                var listener = _webViewType.GetField("listener", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_webView);
                var listenerName = listener?.GetType().GetProperty("Name")?.GetValue(listener) as string;
                var register = managerType.GetMethod("RegisterChannelMethod", BindingFlags.Instance | BindingFlags.NonPublic);
                if (manager == null || string.IsNullOrEmpty(listenerName) || register == null)
                {
                    throw new MissingMemberException("UniWebView 6 media permission channel is unavailable.");
                }

                _mediaPermissionPrompt = Enum.Parse(decisionType, "Prompt");
                _mediaPermissionGrant = Enum.Parse(decisionType, "Grant");
                register.Invoke(manager, new object[]
                {
                    listenerName,
                    Enum.Parse(channelType, "RequestMediaCapturePermission"),
                    new Func<object, object>(DecideMediaPermission)
                });
            }
            catch (Exception)
            {

                Debug.LogWarning("[WebWrapper.UniWebView] Protected-media auto-grant is unavailable for this SDK; permissions will use the native prompt.");
            }
        }

        private object DecideMediaPermission(object request)
        {
            var resources = request?.GetType().GetProperty("Resources")?.GetValue(request) as string[];
            return WebViewNavigationPolicy.ShouldAutoGrantMedia(resources) ? _mediaPermissionGrant : _mediaPermissionPrompt;
        }

        private void DisposeWebView(bool immediate)
        {
            CancelRedirectRetry();
            _showRequested = false;
            _nativeVisible = false;
            _loading = false;
            var webView = _webView;
            if (webView == null)
            {
                return;
            }

            TryInvokeOptional("Hide");
            TryInvokeOptional("UnregisterOnRequestMediaCapturePermission");
            TryInvokeOptional("UnregisterOnEmbeddedToolbarItemAction");
            foreach (var subscription in _eventHandlers)
            {
                subscription.Key.RemoveEventHandler(webView, subscription.Value);
            }
            _eventHandlers.Clear();
            _webView = null;
            if (immediate)
            {
                DestroyImmediate(webView);
            }
            else
            {
                Destroy(webView);
            }
        }

        private void UpdateDiagnosticsVisibility()
        {
            if (_webView == null || !_showRequested || !_nativeVisible) return;
            var hidden = WebWrapperDiagnostics.ConsoleOpen;
            if (_diagnosticsHidden == hidden) return;
            _diagnosticsHidden = hidden;
            TryInvokeOptional(hidden ? "Hide" : "Show");
        }

        private void ApplyViewportFrame()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _safeArea = Screen.safeArea;
#if UNITY_ANDROID && !UNITY_EDITOR

            const bool fillViewport = true;
#else
            const bool fillViewport = false;
#endif
            var frame = CalculateViewportFrame(_safeArea, _screenWidth, _screenHeight,
                WebWrapperDiagnostics.ToolbarPixels, fillViewport);
#if UNITY_EDITOR

            frame = WebWrapperEditorLiveSession.FitWebViewPreview(frame, UnityEditor.EditorGUIUtility.GetMainWindowPosition().size);
#endif
            _webViewType.GetProperty("Frame")?.SetValue(
                _webView,
                frame);
        }

        public static Rect CalculateNativeFrame(Rect safeArea, int screenHeight, float diagnosticToolbarPixels)
        {

            return new Rect(safeArea.x, screenHeight - safeArea.yMax, safeArea.width,
                Mathf.Max(1, safeArea.height - Mathf.Max(0, diagnosticToolbarPixels)));
        }

        public static Rect CalculateViewportFrame(Rect safeArea, int screenWidth, int screenHeight,
            float diagnosticToolbarPixels, bool fillViewport)
        {
            var bounds = fillViewport ? new Rect(0, 0, screenWidth, screenHeight) : safeArea;
            return CalculateNativeFrame(bounds, screenHeight, diagnosticToolbarPixels);
        }

        private void InvokeOptional(string methodName, params object[] suppliedArguments)
        {
            var method = FindCallableMethod(methodName, suppliedArguments);
            if (method == null)
            {
                throw new MissingMethodException(_webViewType.FullName, methodName);
            }

            method.Invoke(_webView, BuildArguments(method, suppliedArguments));
        }

        private void TryInvokeOptional(string methodName, params object[] suppliedArguments)
        {
            try
            {
                var method = FindCallableMethod(methodName, suppliedArguments);
                if (method != null)
                {
                    method.Invoke(_webView, BuildArguments(method, suppliedArguments));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[WebWrapper.UniWebView] " + methodName + " failed: " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }

        private void TryInvokeStaticOptional(string methodName, params object[] suppliedArguments)
        {
            try
            {
                var method = FindCallableMethod(
                    methodName,
                    suppliedArguments,
                    BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, BuildArguments(method, suppliedArguments));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[WebWrapper.UniWebView] " + methodName + " failed: " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }

        private void TrySetProperty(string propertyName, object value)
        {
            try
            {
                var property = _webViewType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_webView, value);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[WebWrapper.UniWebView] " + propertyName + " failed: " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }

        private MethodInfo FindCallableMethod(string methodName, object[] suppliedArguments)
        {
            return FindCallableMethod(
                methodName,
                suppliedArguments,
                BindingFlags.Public | BindingFlags.Instance);
        }

        private MethodInfo FindCallableMethod(
            string methodName,
            object[] suppliedArguments,
            BindingFlags bindingFlags)
        {
            return _webViewType
                .GetMethods(bindingFlags)
                .Where(method => method.Name == methodName)
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault(method => CanCall(method, suppliedArguments));
        }

        private static bool CanCall(MethodInfo method, object[] suppliedArguments)
        {
            var parameters = method.GetParameters();
            if (parameters.Length < suppliedArguments.Length)
            {
                return false;
            }

            for (var index = 0; index < suppliedArguments.Length; index++)
            {
                var argument = suppliedArguments[index];
                if (argument != null && !parameters[index].ParameterType.IsInstanceOfType(argument))
                {
                    return false;
                }
            }

            for (var index = suppliedArguments.Length; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOptional)
                {
                    return false;
                }
            }

            return true;
        }

        private static object[] BuildArguments(MethodInfo method, object[] suppliedArguments)
        {
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            Array.Copy(suppliedArguments, arguments, suppliedArguments.Length);
            for (var index = suppliedArguments.Length; index < parameters.Length; index++)
            {
                arguments[index] = parameters[index].DefaultValue;
            }

            return arguments;
        }

        private static Type FindUniWebViewType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UniWebView", false);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    return type;
                }
            }

            return null;
        }

        private static bool IsHttpUrl(string value)
        {
            return WebViewNavigationPolicy.IsHttpUrl(value);
        }
    }
}
