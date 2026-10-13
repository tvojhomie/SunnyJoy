using System;

namespace AlienProject.WebWrapper
{
    public static class LaunchNavigationPolicy
    {
        public static LaunchDecision Resolve(LaunchDecision normal, string pushUrl, bool online)
        {
            if (!Uri.TryCreate(pushUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return normal;
            return online ? LaunchDecision.ShowWebView(pushUrl, "Opened notification for this session.") :
                LaunchDecision.ShowOffline("Internet access is required to open the notification.");
        }
    }
}
