using UnityEngine;

namespace AlienProject.WebWrapper
{
    public static class WebWrapperBootstrap
    {
        public static bool IsAutoStartEnabled
        {
            get
            {
#if UNITY_EDITOR
                if (WebWrapperEditorLiveSession.IsEnabled) return true;
                if (UnityEditor.SessionState.GetBool("AlienProject.WebWrapper.EditorPreview", false)) return false;
#endif
                var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
                return settings != null && settings.AutoStart;
            }
        }

#if UNITY_EDITOR

        public static void SetEditorPreview(bool enabled) =>
            UnityEditor.SessionState.SetBool("AlienProject.WebWrapper.EditorPreview", enabled);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            WebWrapperDiagnostics.EnsureInitialized();
            if (!IsAutoStartEnabled)
            {
                return;
            }

            if (Object.FindFirstObjectByType<WebWrapperController>() != null)
            {
                return;
            }

            var gameObject = new GameObject("[WebWrapper]");
            Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<WebWrapperController>();
        }
    }
}
