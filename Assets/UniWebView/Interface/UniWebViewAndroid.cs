#if UNITY_ANDROID && !UNITY_EDITOR

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

class UniWebViewMethodChannel: AndroidJavaProxy {
    private const string GlobalChannelIdentifier = "__UniWebViewGlobalChannelIdentifier";
    public UniWebViewMethodChannel() : base("com.onevcat.uniwebview.UniWebViewNativeChannel") { }

    string invokeChannelMethod(string name, string method, string parameters) {
        if (name == GlobalChannelIdentifier) {
            UniWebViewLogger.Instance.Verbose(
                () => "Global channel method invoked. Method: " + method +
                      " Params: " + UniWebViewLogger.DescribePayload(parameters)
            );
            return UniWebViewStaticListener.InvokeStaticMethod(method, parameters);
        } else {
            UniWebViewLogger.Instance.Verbose(
                () => "invokeChannelMethod invoked by native side. Name: " + name + " Method: " + method +
                      " Params: " + UniWebViewLogger.DescribePayload(parameters)
            );
            return UniWebViewChannelMethodManager.Instance.InvokeMethod(name, method, parameters);
        }
    }
}

public class UniWebViewInterface {
    private const int SnapshotTextureStreamStopRenderEventCount = 3;
    private static readonly AndroidJavaClass plugin;
    private static bool correctPlatform = Application.platform == RuntimePlatform.Android;
    
    static UniWebViewInterface() {
        var go = new GameObject("UniWebViewAndroidStaticListener");
        go.AddComponent<UniWebViewAndroidStaticListener>();
        plugin = new AndroidJavaClass("com.onevcat.uniwebview.UniWebViewInterface");
        
        // Prepare dispatcher instance. Some callbacks may come from non-UI threads. Use this dispatcher to
        // send any action to the Unity main thread.
        _ = UniWebViewMainThreadDispatcher.Instance;

        CheckPlatform();

        plugin.CallStatic("prepare");

        UniWebViewLogger.Instance.Info("Connecting to native side method channel.");
        plugin.CallStatic("registerChannel", new UniWebViewMethodChannel());
    }

    public static void SetLogLevel(int level) {
        CheckPlatform();
        plugin.CallStatic("setLogLevel", level); 
    }

    public static bool IsWebViewSupported() {
        CheckPlatform();
        return plugin.CallStatic<bool>("isWebViewSupported");
    }

    public static void Init(string name, int x, int y, int width, int height) {
        CheckPlatform();
        plugin.CallStatic("init", name, x, y, width, height);
    }

    public static void Destroy(string name) {
        CheckPlatform();
        plugin.CallStatic("destroy", name);
    }

    public static void Load(string name, string url, bool skipEncoding, string readAccessURL) {
        CheckPlatform();
        plugin.CallStatic("load", name, url);
    }

    public static void LoadHTMLString(string name, string html, string baseUrl, bool skipEncoding) {
        CheckPlatform();
        plugin.CallStatic("loadHTMLString", name, html, baseUrl);
    }

    public static void Reload(string name) {
        CheckPlatform();
        plugin.CallStatic("reload", name);
    }

    public static void Stop(string name) {
        CheckPlatform();
        plugin.CallStatic("stop", name);
    }

    public static string GetUrl(string name) {
        CheckPlatform();
        return plugin.CallStatic<string>("getUrl", name);
    }

    public static void SetFrame(string name, int x, int y, int width, int height) {
        CheckPlatform();
        plugin.CallStatic("setFrame", name, x, y, width, height);
    }

    public static void SetPosition(string name, int x, int y) {
        CheckPlatform();
        plugin.CallStatic("setPosition", name, x, y);
    }

    public static void SetSize(string name, int width, int height) {
        CheckPlatform();
        plugin.CallStatic("setSize", name, width, height);
    }

    public static void SetTransform(string name, float rotation, float scaleX, float scaleY) {
        CheckPlatform();
        plugin.CallStatic("setTransform", name, rotation, scaleX, scaleY);
    }

    public static void SetRoundCornerRadius(string name, float topLeft, float topRight, float bottomLeft, float bottomRight) {
        CheckPlatform();
        plugin.CallStatic("setCornerRadius", name, topLeft, topRight, bottomLeft, bottomRight);
    }

    public static void SetShadow(
        string name,
        float red,
        float green,
        float blue,
        float alpha,
        float opacity,
        float radius,
        float offsetX,
        float offsetY,
        float spread
    ) {
        CheckPlatform();
        plugin.CallStatic("setShadow", name, red, green, blue, alpha, opacity, radius, offsetX, offsetY, spread);
    }

    public static bool Show(string name, bool fade, int edge, float duration, bool useAsync, string identifier) {
        CheckPlatform();
        if (useAsync) {
            plugin.CallStatic("showAsync", name, fade, edge, duration, identifier);
            return true;
        } else {
            return plugin.CallStatic<bool>("show", name, fade, edge, duration, identifier);
        }
    }

    public static bool Hide(string name, bool fade, int edge, float duration, bool useAsync, string identifier) {
        CheckPlatform();
        if (useAsync) {
            plugin.CallStatic("hideAsync", name, fade, edge, duration, identifier);
            return true;
        } else {
            return plugin.CallStatic<bool>("hide", name, fade, edge, duration, identifier);
        }
    }

    public static bool AnimateTo(string name, int x, int y, int width, int height, float duration, float delay, string identifier) {
        CheckPlatform();
        return plugin.CallStatic<bool>("animateTo", name, x, y, width, height, duration, delay, identifier);
    }

    public static void AddJavaScript(string name, string jsString, string identifier) {
        CheckPlatform();
        plugin.CallStatic("addJavaScript", name, jsString, identifier);
    }

    public static void EvaluateJavaScript(string name, string jsString, string identifier) {
        CheckPlatform();
        plugin.CallStatic("evaluateJavaScript", name, jsString, identifier);
    }

    public static void ClosePopupWindow(string name, string popupId) {
        CheckPlatform();
        plugin.CallStatic("closePopupWindow", name, popupId);
    }

    public static void GoBackPopupWindow(string name, string popupId) {
        CheckPlatform();
        plugin.CallStatic("goBackPopupWindow", name, popupId);
    }

    public static void GoForwardPopupWindow(string name, string popupId) {
        CheckPlatform();
        plugin.CallStatic("goForwardPopupWindow", name, popupId);
    }

    public static void EvaluateJavaScriptInPopupWindow(
        string name,
        string popupId,
        string jsString,
        string identifier
    ) {
        CheckPlatform();
        plugin.CallStatic("evaluateJavaScriptInPopupWindow", name, popupId, jsString, identifier);
    }

    public static void CloseAllPopupWindows(string name) {
        CheckPlatform();
        plugin.CallStatic("closeAllPopupWindows", name);
    }

    public static void SetPopupPageEventEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setPopupPageEventEnabled", name, enabled);
    }

    public static void AddUrlScheme(string name, string scheme) {
        CheckPlatform();
        plugin.CallStatic("addUrlScheme", name, scheme);
    }

    public static void RemoveUrlScheme(string name, string scheme) {
        CheckPlatform();
        plugin.CallStatic("removeUrlScheme", name, scheme);
    }

    [Obsolete("AddSslExceptionDomain is deprecated. Use AddSslPinnedFingerprint instead.")]
    public static void AddSslExceptionDomain(string name, string domain) {
        CheckPlatform();
        plugin.CallStatic("addSslExceptionDomain", name, domain);
    }

    [Obsolete("RemoveSslExceptionDomain is deprecated. Use RemoveSslPinnedFingerprint instead.")]
    public static void RemoveSslExceptionDomain(string name, string domain) {
        CheckPlatform();
        plugin.CallStatic("removeSslExceptionDomain", name, domain);
    }

    public static void AddSslPinnedFingerprint(string name, string domain, string fingerprint) {
        CheckPlatform();
        plugin.CallStatic("addSslPinnedFingerprint", name, domain, fingerprint);
    }

    public static void RemoveSslPinnedFingerprint(string name, string domain, string fingerprint) {
        CheckPlatform();
        plugin.CallStatic("removeSslPinnedFingerprint", name, domain, fingerprint);
    }

    public static void AddPermissionTrustDomain(string name, string domain) {
        CheckPlatform();
        plugin.CallStatic("addPermissionTrustDomain", name, domain);
    }

    public static void RemovePermissionTrustDomain(string name, string domain) {
        CheckPlatform();
        plugin.CallStatic("removePermissionTrustDomain", name, domain);
    }

    public static void SetHeaderField(string name, string key, string value) {
        CheckPlatform();
        plugin.CallStatic("setHeaderField", name, key, value);
    }

    public static void SetUserAgent(string name, string userAgent) {
        CheckPlatform();
        plugin.CallStatic("setUserAgent", name, userAgent);
    }

    public static string GetUserAgent(string name) {
        CheckPlatform();
        return plugin.CallStatic<string>("getUserAgent", name);
    }

    public static void SetAllowAutoPlay(bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAllowAutoPlay", flag);
    }

    public static void SetAllowJavaScriptOpenWindow(bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAllowJavaScriptOpenWindow", flag);
    }

    public static void SetAllowFileAccess(string name, bool flag) { 
        CheckPlatform();
        plugin.CallStatic("setAllowFileAccess", name, flag);
    }

    public static void SetAcceptThirdPartyCookies(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAcceptThirdPartyCookies", name, flag);
    }

    public static void SetAllowFileAccessFromFileURLs(string name, bool flag) { 
        CheckPlatform();
        plugin.CallStatic("setAllowFileAccessFromFileURLs", name, flag);
    }

    public static void SetAllowUniversalAccessFromFileURLs(bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAllowUniversalAccessFromFileURLs", flag);
    }
    public static void BringContentToFront(string name) {
        CheckPlatform();
        plugin.CallStatic("bringContentToFront", name);
    }

    public static void SetForwardWebConsoleToNativeOutput(bool flag) {
        CheckPlatform();
        plugin.CallStatic("setForwardWebConsoleToNativeOutput", flag);
    }

    public static void SetEnableKeyboardAvoidance(bool flag) {
        CheckPlatform();
        plugin.CallStatic("setEnableKeyboardAvoidance", flag);
    }

    public static void SetJavaScriptEnabled(bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setJavaScriptEnabled", enabled);
    }

    public static void CleanCache(string name, bool includeStorage, string identifier) {
        CheckPlatform();
        plugin.CallStatic("cleanCache", name, includeStorage, identifier);
    }

    public static void SetCacheMode(string name, int mode) {
        CheckPlatform();
        plugin.CallStatic("setCacheMode", name, mode);
    }

    public static void ClearCookies() {
        CheckPlatform();
        plugin.CallStatic("clearCookies");
    }

    public static void ClearCookies(string identifier) {
        CheckPlatform();
        plugin.CallStatic("clearCookiesAsync", identifier);
    }

    public static void SetCookie(string url, string cookie, bool skipEncoding) {
        CheckPlatform();
        plugin.CallStatic("setCookie", url, cookie);
    }

    public static void SetCookie(string url, string cookie, bool skipEncoding, string identifier) {
        CheckPlatform();
        plugin.CallStatic("setCookieAsync", url, cookie, identifier);
    }

    public static string GetCookie(string url, string key, bool skipEncoding) {
        CheckPlatform();
        return plugin.CallStatic<string>("getCookie", url, key);
    }

    public static void GetCookie(string url, string key, bool skipEncoding, string identifier) {
        CheckPlatform();
        plugin.CallStatic("getCookieAsync", url, key, identifier);
    }

    public static void RemoveCookies(string url, bool skipEncoding) {
        CheckPlatform();
        plugin.CallStatic("removeCookies", url);
    }

    public static void RemoveCookies(string url, bool skipEncoding, string identifier) {
        CheckPlatform();
        plugin.CallStatic("removeCookiesAsync", url, identifier);
    }

    public static void RemoveCookie(string url, string key, bool skipEncoding) {
        CheckPlatform();
        plugin.CallStatic("removeCookie", url, key);
    }

    public static void RemoveCookie(string url, string key, bool skipEncoding, string identifier) {
        CheckPlatform();
        plugin.CallStatic("removeCookieAsync", url, key, identifier);
    }

    public static void ClearHttpAuthUsernamePassword(string host, string realm) {
        CheckPlatform();
        plugin.CallStatic("clearHttpAuthUsernamePassword", host, realm);
    }

    public static void SetBackgroundColor(string name, float r, float g, float b, float a) {
        CheckPlatform();
        plugin.CallStatic("setBackgroundColor", name, r, g, b, a);
    }

    public static void SetWebViewAlpha(string name, float alpha) {
        CheckPlatform();
        plugin.CallStatic("setWebViewAlpha", name, alpha);
    }

    public static float GetWebViewAlpha(string name) {
        CheckPlatform();
        return plugin.CallStatic<float>("getWebViewAlpha", name);
    }

    public static void SetShowSpinnerWhileLoading(string name, bool show) {
        CheckPlatform();
        plugin.CallStatic("setShowSpinnerWhileLoading", name, show);
    }

    public static void SetSpinnerText(string name, string text) {
        CheckPlatform();
        plugin.CallStatic("setSpinnerText", name, text);
    }

    public static void SetAllowUserDismissSpinnerByGesture(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAllowUserDismissSpinnerByGesture", name, flag);
    }

    public static void ShowSpinner(string name) {
        CheckPlatform();
        plugin.CallStatic("showSpinner", name);
    }

    public static void HideSpinner(string name) {
        CheckPlatform();
        plugin.CallStatic("hideSpinner", name);
    }

    public static bool CanGoBack(string name) {
        CheckPlatform();
        return plugin.CallStatic<bool>("canGoBack", name);
    }

    public static bool CanGoForward(string name) {
        CheckPlatform();
        return plugin.CallStatic<bool>("canGoForward", name);
    }

    public static void GoBack(string name) {
        CheckPlatform();
        plugin.CallStatic("goBack", name);
    }
    public static void GoForward(string name) {
        CheckPlatform();
        plugin.CallStatic("goForward", name);
    }

    public static void SetOpenLinksInExternalBrowser(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setOpenLinksInExternalBrowser", name, flag);
    }

    public static void SetHorizontalScrollBarEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setHorizontalScrollBarEnabled", name, enabled);
    }

    public static void SetVerticalScrollBarEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setVerticalScrollBarEnabled", name, enabled);
    }

    public static void SetBouncesEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setBouncesEnabled", name, enabled);
    }

    public static void SetZoomEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setZoomEnabled", name, enabled);
    }

    public static void SetUseWideViewPort(string name, bool use) {
        CheckPlatform();
        plugin.CallStatic("setUseWideViewPort", name, use);
    }

    public static void SetLoadWithOverviewMode(string name, bool overview) {
        CheckPlatform();
        plugin.CallStatic("setLoadWithOverviewMode", name, overview);
    }

    public static void SetImmersiveModeEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setImmersiveModeEnabled", name, enabled);
    }

    public static void SetUserInteractionEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setUserInteractionEnabled", name, enabled);
    }

    public static void SetTransparencyClickingThroughEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setTransparencyClickingThroughEnabled", name, enabled);
    }

    public static void RefreshTransparencyClickingThroughLayout(string name) {
        CheckPlatform();
        plugin.CallStatic("refreshTransparencyClickingThroughLayout", name);
    }

    public static void SetWebContentsDebuggingEnabled(bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setWebContentsDebuggingEnabled", enabled);
    }

    public static void SetAllowHTTPAuthPopUpWindow(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setAllowHTTPAuthPopUpWindow", name, flag);
    }

    public static void Print(string name) {
        CheckPlatform();
        plugin.CallStatic("print", name);
    }

    public static void CaptureSnapshot(string name, string filename) { 
        CheckPlatform();
        plugin.CallStatic("captureSnapshot", name, filename);
    }

    public static void ScrollTo(string name, int x, int y, bool animated) {
        CheckPlatform();
        plugin.CallStatic("scrollTo", name, x, y, animated);
    }

    public static void SetCalloutEnabled(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setCalloutEnabled", name, flag);
    }

    public static void SetSupportMultipleWindows(string name, bool enabled, bool allowJavaScriptOpening) {
        CheckPlatform();
        plugin.CallStatic("setSupportMultipleWindows", name, enabled, allowJavaScriptOpening);
    }

    public static void SetDragInteractionEnabled(string name, bool flag) {
        CheckPlatform();
        plugin.CallStatic("setDragInteractionEnabled", name, flag);
    }

    public static void SetDefaultFontSize(string name, int size) {
        CheckPlatform();
        plugin.CallStatic("setDefaultFontSize", name, size);
    }

    public static void SetTextZoom(string name, int textZoom) { 
        CheckPlatform();
        plugin.CallStatic("setTextZoom", name, textZoom);
    }

    public static float NativeScreenWidth() {
        CheckPlatform();
        return plugin.CallStatic<float>("screenWidth");
    }

    public static float NativeScreenHeight() {
        CheckPlatform();
        return plugin.CallStatic<float>("screenHeight");
    }

    public static int GetStatusBarHeight() {
        CheckPlatform();
        return plugin.CallStatic<int>("getStatusBarHeight");
    }

    // Returns how far the Unity player surface is inset from the edges of the native content view, as
    // [left, top, right, bottom] in pixels. Returns all zeros if the value cannot be retrieved, so callers
    // fall back to the status-bar-based frame mapping.
    public static float[] GetUnityViewInsets() {
        CheckPlatform();
        try {
            var result = plugin.CallStatic<string>("getUnityViewInsets");
            var parts = result.Split(',');
            if (parts.Length == 4) {
                return new float[] {
                    int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3])
                };
            }
            UniWebViewLogger.Instance.Warning("Unexpected Unity view insets value: " + result);
        } catch (Exception e) {
            UniWebViewLogger.Instance.Warning("Failed to get Unity view insets: " + e.Message);
        }
        return new float[] { 0, 0, 0, 0 };
    }

    public static void SetDownloadEventForContextMenuEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setDownloadEventForContextMenuEnabled", name, enabled);
    }

    public static void SetAutoDownloadEnabled(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("setAutoDownloadEnabled", name, enabled);
    }

    public static void SetAndroidDownloadDestination(string name, int destination) {
        CheckPlatform();
        plugin.CallStatic("setAndroidDownloadDestination", name, destination);
    }

    public static void SetAllowUserEditFileNameBeforeDownloading(string name, bool allowed) {
        CheckPlatform();
        plugin.CallStatic("setAllowUserEditFileNameBeforeDownloading", name, allowed);
    }

    // Safe Browsing

    public static bool IsSafeBrowsingSupported() {
        CheckPlatform();
        return plugin.CallStatic<bool>("isSafeBrowsingSupported");
    }

    public static string GetSafeBrowsingCustomTabsProviderPackageName() {
        CheckPlatform();
        return plugin.CallStatic<string>("getSafeBrowsingCustomTabsProviderPackageName");
    }


    public static void SafeBrowsingInit(string name, string url) { 
        CheckPlatform();
        plugin.CallStatic("safeBrowsingInit", name, url);
    }

    public static void SafeBrowsingChangeUrl(string name, string url) {
        CheckPlatform();
        plugin.CallStatic("safeBrowsingChangeUrl", name, url);
    }

    public static void SafeBrowsingSetToolbarColor(string name, float r, float g, float b) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetToolbarColor", name, r, g, b);
    }

    public static void SafeBrowsingSetColorScheme(string name, int colorScheme) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetColorScheme", name, colorScheme);
    }

    public static void SafeBrowsingShow(string name) {
        CheckPlatform();
        plugin.CallStatic("safeBrowsingShow", name);
    }

    public static void SafeBrowsingInvalidate(string name) {
        CheckPlatform();
        plugin.CallStatic("safeBrowsingInvalidate", name);
    }

    public static void SetPreferredCustomTabsBrowsers(string[] packages) {
        CheckPlatform();
        plugin.CallStatic("setPreferredCustomTabsBrowsers", (object)packages);
    }

    public static void SafeBrowsingSetSecondaryToolbarColor(string name, float r, float g, float b) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetSecondaryToolbarColor", name, r, g, b);
    }

    public static void SafeBrowsingSetNavigationBarColor(string name, float r, float g, float b) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetNavigationBarColor", name, r, g, b);
    }

    public static void SafeBrowsingSetNavigationBarDividerColor(string name, float r, float g, float b) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetNavigationBarDividerColor", name, r, g, b);
    }

    public static void SafeBrowsingSetToolbarCornerRadiusDp(string name, int cornerRadiusDp) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetToolbarCornerRadiusDp", name, cornerRadiusDp);
    }

    public static void SafeBrowsingSetInitialHeightPx(string name, int initialHeightPx, int resizeBehavior = (int)UniWebViewSafeBrowsing.ActivityHeightResizeBehavior.Fixed) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetInitialHeightPx", name, initialHeightPx, resizeBehavior);
    }

    public static void SafeBrowsingSetInitialWidthPx(string name, int initialWidthPx) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetInitialWidthPx", name, initialWidthPx);
    }

    public static void SafeBrowsingSetActivitySideSheetBreakpointDp(string name, int breakpointDp) {
        CheckPlatform();
        plugin.CallStatic("safeBrowsingSetActivitySideSheetBreakpointDp", name, breakpointDp);
    }

    public static void SafeBrowsingSetActivitySideSheetPosition(string name, int position) {
        CheckPlatform();
        plugin.CallStatic("safeBrowsingSetActivitySideSheetPosition", name, position);
    }

    public static void SafeBrowsingSetShareMenuItemEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetShareMenuItemEnabled", name, enabled);
    }

    public static void SafeBrowsingSetUrlBarHidingEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetUrlBarHidingEnabled", name, enabled);
    }

    public static void SafeBrowsingSetSendToExternalDefaultHandlerEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetSendToExternalDefaultHandlerEnabled", name, enabled);
    }

    public static void SafeBrowsingSetMaximizationEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetMaximizationEnabled", name, enabled);
    }

    public static void SafeBrowsingSetDownloadButtonEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetDownloadButtonEnabled", name, enabled);
    }

    public static void SafeBrowsingSetBookmarksButtonEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetBookmarksButtonEnabled", name, enabled);
    }

    public static void SafeBrowsingSetBackgroundInteractionEnabled(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetBackgroundInteractionEnabled", name, enabled);
    }

    public static void SafeBrowsingSetWarmup(string name, bool enabled) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetWarmup", name, enabled);
    }

    public static void SafeBrowsingSetPrefetch(string name, bool enabled, string optionalAlternativeUrl = null) {
        CheckPlatform(); 
        plugin.CallStatic("safeBrowsingSetPrefetch", name, enabled, optionalAlternativeUrl);
    }
    
    // Authentication

    public static bool IsAuthenticationIsSupported() {
        CheckPlatform();
        return plugin.CallStatic<bool>("isAuthenticationIsSupported");
    }

    public static void AuthenticationInit(string name, string url, string scheme) {
        CheckPlatform();
        plugin.CallStatic("authenticationInit", name, url, scheme);
    }

    public static void AuthenticationStart(string name) {
        CheckPlatform();
        plugin.CallStatic("authenticationStart", name);
    }

    public static void AuthenticationSetPrivateMode(string name, bool enabled) {
        CheckPlatform();
        plugin.CallStatic("authenticationSetPrivateMode", name, enabled);
    }

    public static bool SetEmbeddedToolbarConfig(string name, string json) {
        CheckPlatform();
        return plugin.CallStatic<bool>("setEmbeddedToolbarConfig", name, json);
    }

    public static void SetShowEmbeddedToolbar(string name, bool show) {
        CheckPlatform();
        plugin.CallStatic("setShowEmbeddedToolbar", name, show);
    }

    public static void SetEmbeddedToolbarOnTop(string name, bool top) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarOnTop", name, top);
    }

    public static void SetEmbeddedToolbarDoneButtonText(string name, string text) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarDoneButtonText", name, text);
    }

    public static void SetEmbeddedToolbarGoBackButtonText(string name, string text) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarGoBackButtonText", name, text);
    }

    public static void SetEmbeddedToolbarGoForwardButtonText(string name, string text) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarGoForwardButtonText", name, text);
    }
    
    public static void SetEmbeddedToolbarTitleText(string name, string text) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarTitleText", name, text);
    }

    public static void SetEmbeddedToolbarBackgroundColor(string name, Color color) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarBackgroundColor", name, color.r, color.g, color.b, color.a);
    }
    
    public static void SetEmbeddedToolbarButtonTextColor(string name, Color color) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarButtonTextColor", name, color.r, color.g, color.b, color.a);
    }

    public static void SetEmbeddedToolbarTitleTextColor(string name, Color color) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarTitleTextColor", name, color.r, color.g, color.b, color.a);
    }

    public static void SetEmeddedToolbarNavigationButtonsShow(string name, bool show) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarNavigationButtonsShow", name, show);
    }

    public static void SetEmbeddedToolbarMaxHeight(string name, float height) {
        CheckPlatform();
        plugin.CallStatic("setEmbeddedToolbarMaxHeight", name, height);
    }

    public static void StartSnapshotForRendering(string name, string identifier) {
        CheckPlatform();
        plugin.CallStatic("startSnapshotForRendering", name, identifier);
    }

    public static void StopSnapshotForRendering(string name) {
        CheckPlatform();
        plugin.CallStatic("stopSnapshotForRendering", name);
    }

    public static byte[] GetRenderedData(string name, int x, int y, int width, int height) {
        CheckPlatform();
        var sbyteArray = plugin.CallStatic<sbyte[]>("getRenderedData", name, x, y, width, height);
        if (sbyteArray == null) {
            return null;
        }
        byte[] byteArray = new byte[sbyteArray.Length];
        // sbyte and byte share the same bit layout; a bulk copy avoids a per-element loop over a
        // multi-megabyte image buffer.
        Buffer.BlockCopy(sbyteArray, 0, byteArray, 0, sbyteArray.Length);
        return byteArray;
    }

    public static bool StartSnapshotTextureStream(string name, long streamId, int x, int y, int width, int height, float resolutionScale) {
        CheckPlatform();
        LogSnapshotTextureStreamCpuFallbackIfNeeded();
        if (!Mathf.Approximately(resolutionScale, 1.0f)) {
            UniWebViewLogger.Instance.Info("Snapshot texture stream resolutionScale is not supported on Android yet. Capturing at full resolution.");
        }
        return plugin.CallStatic<bool>("startSnapshotTextureStream", name, streamId, x, y, width, height);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool uv_startSnapshotTextureStreamSynthetic(string name, long streamId, int width, int height, int framePattern);
    public static bool StartSnapshotTextureStreamWithSyntheticFrames(
        string name, long streamId, int width, int height, int framePattern
    ) {
        CheckPlatform();
        LogSnapshotTextureStreamCpuFallbackIfNeeded();
        return uv_startSnapshotTextureStreamSynthetic(name, streamId, width, height, framePattern);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern void uv_stopSnapshotTextureStream(string name, long streamId);
    public static void StopSnapshotTextureStream(string name, long streamId) {
        CheckPlatform();
        uv_stopSnapshotTextureStream(name, streamId);
        plugin.CallStatic("stopSnapshotTextureStream", name, streamId);
        if (!SnapshotTextureStreamUsesCpuFallback()) {
            IssueSnapshotTextureStreamRenderEvents(SnapshotTextureStreamStopRenderEventCount);
        }
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern void uv_tickSnapshotTextureStream(string name, long streamId);

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern IntPtr uv_getSnapshotTextureStreamRenderEventFunc();
    public static void TickSnapshotTextureStream(string name, long streamId) {
        CheckPlatform();
        plugin.CallStatic("tickSnapshotTextureStream", name, streamId);
        uv_tickSnapshotTextureStream(name, streamId);
    }

    public static void PumpSnapshotTextureStream(string name, long streamId) {
        CheckPlatform();
        if (SnapshotTextureStreamUsesCpuFallback()) {
            return;
        }
        IssueSnapshotTextureStreamRenderEvent();
    }

    private static void IssueSnapshotTextureStreamRenderEvent() {
        GL.IssuePluginEvent(uv_getSnapshotTextureStreamRenderEventFunc(), 0);
    }

    private static void IssueSnapshotTextureStreamRenderEvents(int count) {
        for (var i = 0; i < count; i++) {
            IssueSnapshotTextureStreamRenderEvent();
        }
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool uv_isSnapshotTextureStreamReady(string name, long streamId);
    public static bool IsSnapshotTextureStreamReady(string name, long streamId) {
        CheckPlatform();
        return uv_isSnapshotTextureStreamReady(name, streamId);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern int uv_getSnapshotTextureStreamWidth(string name, long streamId);
    public static int GetSnapshotTextureStreamWidth(string name, long streamId) {
        CheckPlatform();
        return uv_getSnapshotTextureStreamWidth(name, streamId);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern int uv_getSnapshotTextureStreamHeight(string name, long streamId);
    public static int GetSnapshotTextureStreamHeight(string name, long streamId) {
        CheckPlatform();
        return uv_getSnapshotTextureStreamHeight(name, streamId);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern long uv_getSnapshotTextureStreamFrameIndex(string name, long streamId);
    public static long GetSnapshotTextureStreamFrameIndex(string name, long streamId) {
        CheckPlatform();
        return uv_getSnapshotTextureStreamFrameIndex(name, streamId);
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern IntPtr uv_getSnapshotTextureStreamTexturePointer(string name, long streamId);
    public static IntPtr GetSnapshotTextureStreamTexturePointer(string name, long streamId) {
        CheckPlatform();
        return uv_getSnapshotTextureStreamTexturePointer(name, streamId);
    }

    public static TextureFormat GetSnapshotTextureStreamTextureFormat() {
        CheckPlatform();
        return TextureFormat.RGBA32;
    }

    [System.Runtime.InteropServices.DllImport("UniWebViewNativeTexture")]
    private static extern long uv_consumeSnapshotTextureStreamCpuFrame(string name, long streamId, IntPtr destination, int capacity);
    public static long ConsumeSnapshotTextureStreamCpuFrame(string name, long streamId, IntPtr destination, int capacity) {
        CheckPlatform();
        return uv_consumeSnapshotTextureStreamCpuFrame(name, streamId, destination, capacity);
    }

    // The optimized native texture path renders through OpenGL ES. On other graphics backends
    // (Vulkan), the stream falls back to a CPU readback that updates a stream-owned Texture2D.
    public static bool SnapshotTextureStreamUsesCpuFallback() {
        CheckPlatform();
        return !IsSnapshotTextureStreamGLBackend();
    }

    private static void LogSnapshotTextureStreamCpuFallbackIfNeeded() {
        if (SnapshotTextureStreamUsesCpuFallback()) {
            UniWebViewLogger.Instance.Info(
                "Snapshot texture stream is using the CPU readback fallback since the current " +
                "graphics backend is not OpenGL ES: " + SystemInfo.graphicsDeviceType
            );
        }
    }

    private static bool IsSnapshotTextureStreamGLBackend() {
#if UNITY_2023_1_OR_NEWER
        // OpenGL ES 2.0 is no longer a selectable graphics API since Unity 2023.1.
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
#else
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
               SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
#endif
    }

    public static string CopyBackForwardList(string name) {
        CheckPlatform();
        return plugin.CallStatic<string>("copyBackForwardList", name);
    }

    public static void GoToIndexInBackForwardList(string listenerName, int index) {
        CheckPlatform();
        plugin.CallStatic("goToIndexInBackForwardList", listenerName, index);
    }

    // Platform

    public static void CheckPlatform() {
        if (!correctPlatform) {
            throw new System.InvalidOperationException("Method can only be performed on Android.");
        }
    }
}
#endif
