namespace AlienProject.WebWrapper
{
    public static class LaunchTimeoutPolicy
    {
        public static LaunchDecision Resolve(IInstallStateStore store, bool online)
        {
            var state = store.Load() ?? new InstallState();
            if (state.Mode == LaunchMode.Wrapper) return LaunchDecision.ShowWrapper("Saved game mode.");

            return LaunchDecision.ShowWrapper("Startup timed out; playing the game for this session.");
        }
    }
}
