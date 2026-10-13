using UnityEngine;

namespace AlienProject.WebWrapper
{
    public sealed class PlayerPrefsInstallStateStore : IInstallStateStore
    {
        private readonly string _key;

        public PlayerPrefsInstallStateStore(string key = null)
        {
            _key = string.IsNullOrWhiteSpace(key) ? BuildDefaultKey() : key;
        }

        public InstallState Load()
        {
            if (!PlayerPrefs.HasKey(_key))
            {
                return new InstallState();
            }

            var json = PlayerPrefs.GetString(_key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new InstallState();
            }

            try
            {
                var state = JsonUtility.FromJson<InstallState>(json);
                if (state == null || state.SchemaVersion != InstallState.CurrentSchemaVersion)
                {
                    return new InstallState();
                }

                if (state.HandledPushMessageIds == null)
                {
                    state.HandledPushMessageIds = new System.Collections.Generic.List<string>();
                }

                if (state.PendingInteractionCallbacks == null)
                {
                    state.PendingInteractionCallbacks = new System.Collections.Generic.List<PendingInteractionCallback>();
                }

                return state;
            }
            catch
            {
                return new InstallState();
            }
        }

        public void Save(InstallState state)
        {
            if (state == null)
            {
                return;
            }

            PlayerPrefs.SetString(_key, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }

        public static string BuildDefaultKey()
        {
            return "AlienProject.WebWrapper.InstallState." + Application.identifier;
        }
    }
}
