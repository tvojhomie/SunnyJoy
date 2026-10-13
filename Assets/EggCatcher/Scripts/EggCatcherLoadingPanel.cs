using AlienProject.WebWrapper;
using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{

    [DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class EggCatcherLoadingPanel : MonoBehaviour, ILoadingView
    {
        private Canvas canvas;
        private GameObject panel;
        private Text label;
        private bool waiting;
        private float startedAt;
        private static readonly string[] LoadingFrames = { "Loading.", "Loading..", "Loading..." };

        public bool IsAvailable => this != null && isActiveAndEnabled && canvas != null && canvas.enabled &&
            panel != null && label != null;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            var root = transform.Find("LoadingPanel");
            if (root == null) return;
            panel = root.gameObject;
            var content = root.Find("SafeAreaContent") ?? root;
            content.Find("Loader")?.gameObject.SetActive(false);
            label = content.Find("LoaingTXT")?.GetComponent<Text>();
            startedAt = Time.unscaledTime;
            if (label != null) AnimateWaiting();
            var background = panel.GetComponent<Image>();
            if (background != null) background.raycastTarget = true;
        }

        private void OnEnable()
        {
            if (IsAvailable) WebWrapperServices.LoadingView = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(WebWrapperServices.LoadingView, this)) WebWrapperServices.LoadingView = null;
            Hide();
        }

        public void Show()
        {
            if (!IsAvailable || waiting) return;
            waiting = true;
            startedAt = Time.unscaledTime;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            AnimateWaiting();
        }

        public void Hide()
        {
            if (!waiting) return;
            waiting = false;

            if (panel != null && !WebWrapperLaunchState.CanStartGame(WebWrapperBootstrap.IsAutoStartEnabled))
                panel.SetActive(false);
        }

        private void Update()
        {
            if (IsAvailable && (waiting || panel.activeSelf)) AnimateWaiting();
        }

        private void AnimateWaiting()
        {
            var elapsed = Time.unscaledTime - startedAt;

            label.text = LoadingFrames[Mathf.FloorToInt(elapsed * 2f) % LoadingFrames.Length];
        }
    }
}
