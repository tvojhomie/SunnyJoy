using System;
using AlienProject.WebWrapper;
using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{

    [DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class EggCatcherPushPanel : MonoBehaviour, INotificationPromptView
    {
        private Canvas canvas;
        private GameObject panel;
        private Button yesButton, skipButton;
        private Text yesLabel;
        private string originalLabel;
        private Action allow, skip;
        private bool busy;

        public bool IsAvailable => this != null && isActiveAndEnabled && canvas != null && canvas.enabled &&
            panel != null && yesButton != null && skipButton != null;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            var root = transform.Find("PushPanel");
            if (root == null) return;
            panel = root.gameObject;
            var content = root.Find("SafeAreaContent") ?? root;
            yesButton = content.Find("Yes")?.GetComponent<Button>();
            skipButton = content.Find("Skip")?.GetComponent<Button>();
            yesLabel = yesButton != null ? yesButton.GetComponentInChildren<Text>(true) : null;
            originalLabel = yesLabel != null ? yesLabel.text : string.Empty;
            if (yesButton != null) yesButton.onClick.AddListener(Allow);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);

            var image = panel.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;
            Hide();
        }

        private void OnEnable()
        {
            if (IsAvailable) WebWrapperServices.NotificationPromptView = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(WebWrapperServices.NotificationPromptView, this))
                WebWrapperServices.NotificationPromptView = null;
            Hide();
        }

        public void Show(Action onAllow, Action onSkip)
        {
            if (!IsAvailable) return;
            allow = onAllow;
            skip = onSkip;
            SetBusy(false);
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }

        public void SetBusy(bool value)
        {
            busy = value;
            if (yesButton != null) yesButton.interactable = !value;
            if (skipButton != null) skipButton.interactable = !value;
            if (yesLabel != null) yesLabel.text = value ? "Please wait..." : originalLabel;
        }

        public void Hide()
        {
            allow = skip = null;
            if (panel != null) panel.SetActive(false);
            SetBusy(false);
        }

        private void Allow()
        {
            if (busy || panel == null || !panel.activeInHierarchy || allow == null) return;
            var action = allow;
            SetBusy(true);
            action();
        }

        private void Skip()
        {
            if (busy || panel == null || !panel.activeInHierarchy || skip == null) return;
            var action = skip;
            SetBusy(true);
            action();
        }

#if UNITY_EDITOR
        public void PreviewInEditor()
        {
            Show(() => StartCoroutine(PreviewPermission()), () =>
            {
                Hide();
                Debug.Log("[EggCatcher] Push preview: skipped. Real notification preferences were not changed.");
            });
        }

        private System.Collections.IEnumerator PreviewPermission()
        {
            yield return new WaitForSecondsRealtime(.75f);
            Hide();
            Debug.Log("[EggCatcher] Push preview: simulated permission response. The real iOS dialog appears only on a device.");
        }
#endif
    }
}
