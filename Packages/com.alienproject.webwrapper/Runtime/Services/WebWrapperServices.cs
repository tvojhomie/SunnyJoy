namespace AlienProject.WebWrapper
{

    public static class WebWrapperServices
    {
        public static IAttributionProvider AttributionProvider { get; set; }
        public static IPushRegistrationProvider PushRegistrationProvider { get; set; }
        public static IPushOpenSource PushOpenSource { get; set; }
        public static IWebViewAdapter WebViewAdapter { get; set; }
        public static INotificationPermissionProvider NotificationPermissionProvider { get; set; }
        public static INotificationPromptView NotificationPromptView { get; set; }
        public static ILoadingView LoadingView { get; set; }

        public static void Reset()
        {
            AttributionProvider = null;
            PushRegistrationProvider = null;
            PushOpenSource = null;
            WebViewAdapter = null;
            NotificationPermissionProvider = null;
            NotificationPromptView = null;
            LoadingView = null;
        }
    }

    internal sealed class EmptyPushRegistrationProvider : IPushRegistrationProvider
    {
        public System.Threading.Tasks.Task<PushRegistration> GetRegistrationAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return System.Threading.Tasks.Task.FromResult(PushRegistration.Empty);
        }
    }
}
