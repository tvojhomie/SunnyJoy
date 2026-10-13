using System;

namespace AlienProject.WebWrapper
{
    public static class InstallStateUpdates
    {

        public static void Update(this IInstallStateStore store, Action<InstallState> change)
        {
            lock (store)
            {
                var current = store.Load() ?? new InstallState();
                change(current);
                store.Save(current);
            }
        }
    }
}
