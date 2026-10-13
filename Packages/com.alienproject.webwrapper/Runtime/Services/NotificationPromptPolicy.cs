namespace AlienProject.WebWrapper
{
    public static class NotificationPromptPolicy
    {
        public static bool ShouldShow(
            InstallState state,
            bool permissionGranted,
            bool systemRequestAvailable,
            long nowUnixSeconds)
        {
            if (permissionGranted ||
                !systemRequestAvailable ||
                state == null ||
                state.NotificationSystemPermissionDenied)
            {
                return false;
            }

            return nowUnixSeconds >= state.NotificationPromptDeferredUntilUnixSeconds;
        }

        public static void Defer(InstallState state, long nowUnixSeconds, int cooldownDays)
        {
            if (state == null)
            {
                return;
            }

            var safeDays = cooldownDays < 3 ? 3 : cooldownDays;
            state.NotificationPromptDeferredUntilUnixSeconds = nowUnixSeconds + safeDays * 86400L;
        }
    }
}
