using System;
using UnityEngine;
using UnityEngine.Events;

namespace AlienProject.WebWrapper
{

    [AddComponentMenu("Alien Project/Web Wrapper App"), DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class WebWrapperApp : MonoBehaviour, ILoadingView
    {
        [Tooltip("Enabled: Firebase decides whether to open WebView. Disabled: always start the game without requesting a remote route. Applies on the next launch.")]
        [SerializeField] private bool webViewEnabled = true;
        [SerializeField] private string applicationName = string.Empty;
        [SerializeField] private string bundleIdentifier = string.Empty;
        [SerializeField] private string appleAppId = string.Empty;
        [SerializeField] private string firebaseDatabaseUrl = string.Empty;
        [SerializeField] private string firebaseConfigPath = string.Empty;
        [SerializeField] private string appsFlyerDevKey = string.Empty;
        [SerializeField] private string oneLinkDomain = string.Empty;
        [SerializeField] private string iosUrlScheme = string.Empty;
        [SerializeField] private string privacyPolicyUrl = string.Empty;

        [Tooltip("Optional: keep this root inactive in the saved scene. It is activated only for the game route. Do not put WebWrapperApp under it.")]
        [SerializeField] private GameObject gameRoot;
        [Tooltip("Optional: a sibling loading panel, not an ancestor of this component or the game root.")]
        [SerializeField] private GameObject loadingRoot;
        [SerializeField] private UnityEvent onGameReady = new UnityEvent();
        private bool _gameStarted;
        private bool _gameVisible;
        private bool _controlsLoading;

        public bool WebViewEnabled => webViewEnabled;
        public string ApplicationName => applicationName ?? string.Empty;
        public string BundleIdentifier => bundleIdentifier ?? string.Empty;
        public string AppleAppId => appleAppId ?? string.Empty;
        public string FirebaseDatabaseUrl => firebaseDatabaseUrl ?? string.Empty;
        public string FirebaseConfigPath => firebaseConfigPath ?? string.Empty;
        public string AppsFlyerDevKey => appsFlyerDevKey ?? string.Empty;
        public string OneLinkDomain => oneLinkDomain ?? string.Empty;
        public string IosUrlScheme => iosUrlScheme ?? string.Empty;
        public string PrivacyPolicyUrl => privacyPolicyUrl ?? string.Empty;
        public GameObject GameRoot => gameRoot;
        public GameObject LoadingRoot => loadingRoot;
        public bool IsAvailable => isActiveAndEnabled && ValidRoot(loadingRoot);

#if UNITY_EDITOR
        public static event Action<WebWrapperApp> EditorConfigurationChanged;
        private void Reset()
        {
            applicationName = Application.productName;
            bundleIdentifier = Application.identifier;
        }
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorConfigurationChanged?.Invoke(this);
            };
        }
#endif

        private void Awake()
        {
            if (!WebWrapperBootstrap.IsAutoStartEnabled) return;
            if (ValidRoot(gameRoot)) gameRoot.SetActive(false);
            if (IsAvailable && WebWrapperServices.LoadingView == null)
            {
                _controlsLoading = true;
                WebWrapperServices.LoadingView = this;
                Show();
            }
        }

        private void Update()
        {
            ApplyGameVisibility(WebWrapperLaunchState.CanStartGame(WebWrapperBootstrap.IsAutoStartEnabled));
        }

        private void ApplyGameVisibility(bool showGame)
        {
            if (!showGame)
            {
                if (ValidRoot(gameRoot) && gameRoot.activeSelf) gameRoot.SetActive(false);
                _gameVisible = false;
                return;
            }
            if (_gameVisible) return;
            _gameVisible = true;
            Hide();
            if (ValidRoot(gameRoot)) gameRoot.SetActive(true);
            if (!_gameStarted)
            {
                _gameStarted = true;
                onGameReady.Invoke();
            }

            if (ValidRoot(gameRoot)) WebWrapperLaunchState.NotifyGameReady();
        }

        private bool ValidRoot(GameObject root) => root != null && !transform.IsChildOf(root.transform);
        public void Show() { if (_controlsLoading && IsAvailable) loadingRoot.SetActive(true); }
        public void Hide() { if (_controlsLoading && ValidRoot(loadingRoot)) loadingRoot.SetActive(false); }
        private void OnDestroy()
        {
            if (ReferenceEquals(WebWrapperServices.LoadingView, this)) WebWrapperServices.LoadingView = null;
        }
    }
}
