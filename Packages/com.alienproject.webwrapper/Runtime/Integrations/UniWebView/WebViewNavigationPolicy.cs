using System;
using System.Text.RegularExpressions;

namespace AlienProject.WebWrapper.Integrations.UniWebView
{

    public static class WebViewNavigationPolicy
    {
        public static string NormalizeUserAgent(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return userAgent;
            }

            var androidWebView = userAgent.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 Regex.IsMatch(userAgent, @";\s*wv(?=[;)])", RegexOptions.IgnoreCase);
            var normalized = Regex.Replace(userAgent, @";\s*wv(?=[;)])", string.Empty, RegexOptions.IgnoreCase);
            if (androidWebView)
            {
                normalized = Regex.Replace(normalized, @"\s+Version/4\.0(?=\s|$)", string.Empty);
            }

            return normalized;
        }

        public static bool ShouldAutoGrantMedia(string[] resources)
        {
            if (resources == null || resources.Length == 0)
            {
                return false;
            }

            foreach (var resource in resources)
            {
                if (resource != "PROTECTED_MEDIA_ID" &&
                    resource != "android.webkit.resource.PROTECTED_MEDIA_ID")
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsRedirectLimitError(int errorCode, string message)
        {

            return errorCode == -9 || errorCode == -1007 ||
                   (!string.IsNullOrEmpty(message) &&
                    message.IndexOf("ERR_TOO_MANY_REDIRECTS", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static bool IsExternalNavigationError(int errorCode, string message, string failingUrl)
        {
            if (!string.IsNullOrWhiteSpace(failingUrl))
            {
                var colon = failingUrl.IndexOf(':');
                if (colon <= 0) return false;
                var scheme = failingUrl.Substring(0, colon).ToLowerInvariant();
                if (!Uri.CheckSchemeName(scheme)) return false;
                switch (scheme)
                {
                    case "http": case "https": case "about": case "data": case "blob":
                    case "javascript": case "file": case "content":
                        return false;
                    default:
                        return true;
                }
            }
            return errorCode == -10 || errorCode == -1002 ||
                (!string.IsNullOrEmpty(message) &&
                 message.IndexOf("ERR_UNKNOWN_URL_SCHEME", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    public sealed class WebViewRedirectRecovery
    {
        private bool _retried;

        public void Reset()
        {
            _retried = false;
        }

        public bool TryGetRetryUrl(int errorCode, string message, string failingUrl, string lastStartedUrl, out string retryUrl)
        {
            retryUrl = null;
            if (_retried || !WebViewNavigationPolicy.IsRedirectLimitError(errorCode, message))
            {
                return false;
            }

            var candidate = string.IsNullOrEmpty(failingUrl) ? lastStartedUrl : failingUrl;
            if (!WebViewNavigationPolicy.IsHttpUrl(candidate))
            {
                return false;
            }

            _retried = true;
            retryUrl = candidate;
            return true;
        }
    }
}
