using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public interface ILaunchCoordinator
    {
        Task<LaunchDecision> ResolveAsync(CancellationToken cancellationToken);
    }

    public interface IClock
    {
        long UtcNowUnixSeconds { get; }
    }

    public interface IConnectivityService
    {
        bool IsInternetReachable { get; }
    }

    public interface IInstallStateStore
    {
        InstallState Load();
        void Save(InstallState state);
        void Clear();
    }

    public interface IAttributionProvider
    {
        Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
    }

    public interface IPushRegistrationProvider
    {
        Task<PushRegistration> GetRegistrationAsync(CancellationToken cancellationToken);
    }

    public interface IPushOpenSource
    {
        event Action<PushOpenMessage> Opened;
    }

    public interface IConfigClient
    {
        Task<ConfigResult> FetchAsync(ConfigRequest request, CancellationToken cancellationToken);
    }

    public interface IInteractionCallbackClient
    {
        Task<bool> SendAsync(
            string messageId,
            string appsFlyerId,
            CancellationToken cancellationToken);
    }

    public interface IWebViewAdapter
    {
        bool IsAvailable { get; }
        bool CanGoBack { get; }
        void Show(string url);
        void Hide();
        void GoBack();
    }

    public interface INotificationPermissionProvider
    {
        NotificationPermissionStatus Status { get; }
        Task<NotificationPermissionStatus> RequestAsync(CancellationToken cancellationToken);
    }
}
