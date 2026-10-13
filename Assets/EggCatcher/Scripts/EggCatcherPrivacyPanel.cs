using System;
using System.Collections;
using AlienProject.WebWrapper;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EggCatcher
{

    [DisallowMultipleComponent]
    public sealed class EggCatcherPrivacyPanel : MonoBehaviour
    {
        public RectTransform Content { get; private set; }
        public string CurrentUrl { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsPageVisible { get; private set; }

        public Func<IPrivacyPolicyView> ViewFactory { private get; set; } = () => new NativePrivacyPolicyView();
        private IPrivacyPolicyView view;
        private Action close;
        private GameObject status;
        private Text message;
        private Button retry;
        private int generation;
        private bool paused;
        private bool frameDirty;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Vector3[] lastCorners = new Vector3[4];
        private Vector2Int lastScreen;

        public void Configure(RectTransform content, Action onClose)
        {
            Content = content;
            close = onClose;
            if (isActiveAndEnabled) Retry();
        }

        private void OnEnable() { if (Content != null) Retry(); }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        public void Retry()
        {
            Release();
            if (!isActiveAndEnabled || Content == null) return;
            EnsureStatusUi();
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (!TryGetPolicyUrl(settings?.PrivacyPolicyUrl, out var url))
            {
                CurrentUrl = string.Empty;
                ShowMessage("Privacy policy is not available yet.", false);
                return;
            }
            CurrentUrl = url;
            ShowMessage("Loading privacy policy…", false);
            IsLoading = true;
            StartCoroutine(OpenAfterLayout(generation));
        }

        public static bool TryGetPolicyUrl(string value, out string url)
        {
            url = null;
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            url = uri.AbsoluteUri;
            return true;
        }

        private IEnumerator OpenAfterLayout(int session)
        {

            yield return null;
            Canvas.ForceUpdateCanvases();
            try
            {
                view = ViewFactory();
                view.Loaded += code =>
                {
                    if (generation != session || !isActiveAndEnabled) return;
                    if (code >= 400) { Fail(); return; }
                    IsLoading = false;
                    IsPageVisible = true;
                    status.SetActive(false);
                    frameDirty = true;
                    if (!paused) view.Show();
                };
                view.Failed += () => { if (generation == session) Fail(); };
                view.CloseRequested += () => { if (generation == session) close?.Invoke(); };
                view.Open(Content, CurrentUrl);
                frameDirty = true;
            }
            catch (Exception)
            {
                Fail();
            }
            var deadline = Time.realtimeSinceStartup + 15f;
            while (generation == session && IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
            if (generation == session && IsLoading) Fail();
        }

        private void Fail()
        {
            Release();
            if (isActiveAndEnabled) ShowMessage("Unable to load the privacy policy.\nCheck your connection and try again.", true);
        }

        private void Release()
        {
            generation++;
            StopAllCoroutines();
            IsLoading = IsPageVisible = false;
            var old = view;
            view = null;
            old?.Dispose();
        }

        private void Update()
        {

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) close?.Invoke();
        }

        private void LateUpdate()
        {
            if (view == null || Content == null) return;
            var canvas = Content.GetComponentInParent<Canvas>();
            if (!Content.gameObject.activeInHierarchy || canvas == null || !canvas.isActiveAndEnabled ||
                !WebWrapperLaunchState.CanStartGame(WebWrapperBootstrap.IsAutoStartEnabled))
            {
                Release();
                return;
            }
            Content.GetWorldCorners(corners);
            var size = new Vector2Int(Screen.width, Screen.height);
            bool changed = frameDirty || size != lastScreen;
            for (int i = 0; i < 4; i++)
            {
                if (corners[i] != lastCorners[i]) changed = true;
                lastCorners[i] = corners[i];
            }
            lastScreen = size;
            frameDirty = false;
            if (changed) view.RefreshFrame();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (paused) view?.Hide();
            else if (IsPageVisible) { frameDirty = true; view?.RefreshFrame(); view?.Show(); }
        }

        private void ShowMessage(string text, bool canRetry)
        {
            EnsureStatusUi();
            status.SetActive(true);
            message.text = text;
            retry.gameObject.SetActive(canRetry);
        }

        private void EnsureStatusUi()
        {
            if (status != null) return;
            status = new GameObject("PrivacyLoading", typeof(RectTransform), typeof(Image));
            status.transform.SetParent(Content, false);
            var root = (RectTransform)status.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            status.GetComponent<Image>().color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            message = CreateText("Message", root, font, 42);
            message.rectTransform.anchorMin = new Vector2(.05f, .4f);
            message.rectTransform.anchorMax = new Vector2(.95f, .65f);
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
            var button = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(root, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.2f, .29f);
            rect.anchorMax = new Vector2(.8f, .38f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.GetComponent<Image>().color = new Color32(37, 139, 255, 255);
            retry = button.GetComponent<Button>();
            retry.targetGraphic = button.GetComponent<Image>();
            retry.onClick.AddListener(Retry);
            retry.onClick.AddListener(() => GetComponentInParent<EggCatcherAudio>()?.PlayUi());
            var label = CreateText("Label", rect, font, 42);
            label.text = "Try again";
            label.color = Color.white;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color32(28, 77, 132, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }

    public interface IPrivacyPolicyView : IDisposable
    {
        event Action<int> Loaded;
        event Action Failed;
        event Action CloseRequested;
        void Open(RectTransform content, string url);
        void Show();
        void Hide();
        void RefreshFrame();
    }

    internal sealed class NativePrivacyPolicyView : IPrivacyPolicyView
    {
        public event Action<int> Loaded;
        public event Action Failed;
        public event Action CloseRequested;
        private GameObject host;
        private UniWebView browser;

        public void Open(RectTransform content, string url)
        {
            host = new GameObject("PrivacyWebView");
            host.transform.SetParent(content, false);
            browser = host.AddComponent<UniWebView>();
            browser.ReferenceRectTransform = content;
            browser.BackgroundColor = Color.white;
            browser.SetBackButtonEnabled(false);
            browser.SetSupportMultipleWindows(false, false);
            browser.SetShowSpinnerWhileLoading(false);
            browser.OnPageFinished += (_, status, __) =>
            {
#if UNITY_EDITOR
                Debug.Log("[Privacy Policy] Native page response: HTTP " + status + "; viewport bound to " + content.name + ".");
#endif
                Loaded?.Invoke(status);
            };
            browser.OnLoadingErrorReceived += (_, __, ___, ____) => Failed?.Invoke();
            browser.OnShouldClose += _ => { CloseRequested?.Invoke(); return false; };
            browser.Load(url);
        }

        public void Show() { if (browser != null) browser.Show(); }
        public void Hide() { if (browser != null) browser.Hide(); }
        public void RefreshFrame() { if (browser != null) browser.UpdateFrame(); }
        public void Dispose()
        {
            Loaded = null;
            Failed = null;
            CloseRequested = null;
            Hide();
            if (host != null) UnityEngine.Object.Destroy(host);
            browser = null;
            host = null;
        }
    }
}
