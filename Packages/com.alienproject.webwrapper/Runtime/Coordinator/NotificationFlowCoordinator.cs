using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{
    public sealed class NotificationFlowCoordinator
    {
        private readonly IInstallStateStore _stateStore;
        private readonly INotificationPermissionProvider _permissionProvider;
        private readonly IClock _clock;
        private readonly int _cooldownDays;

        public NotificationFlowCoordinator(
            IInstallStateStore stateStore,
            INotificationPermissionProvider permissionProvider,
            IClock clock,
            int cooldownDays)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _permissionProvider = permissionProvider ?? throw new ArgumentNullException(nameof(permissionProvider));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _cooldownDays = Math.Max(3, cooldownDays);
        }

        public bool ShouldShowCustomPrompt()
        {
            var status = _permissionProvider.Status;
            var canRequest = status == NotificationPermissionStatus.Unknown ||
                             status == NotificationPermissionStatus.DeniedCanAskAgain;
            return NotificationPromptPolicy.ShouldShow(
                _stateStore.Load(),
                status == NotificationPermissionStatus.Granted,
                canRequest,
                _clock.UtcNowUnixSeconds);
        }

        public void Skip()
        {
            var state = _stateStore.Load() ?? new InstallState();
            NotificationPromptPolicy.Defer(state, _clock.UtcNowUnixSeconds, _cooldownDays);
            _stateStore.Save(state);
        }

        public async Task<NotificationPermissionStatus> RequestSystemPermissionAsync(
            CancellationToken cancellationToken)
        {
            var status = await _permissionProvider.RequestAsync(cancellationToken);
            if (status != NotificationPermissionStatus.Granted)
            {
                var state = _stateStore.Load() ?? new InstallState();
                state.NotificationSystemPermissionDenied = true;
                _stateStore.Save(state);
            }

            return status;
        }
    }
}
