using AlienProject.WebWrapper;
using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{

    public sealed class AndroidTestConsole : MonoBehaviour
    {
        private RectTransform safeRoot;
        private GameObject panel;
        private Text output;
        private ScrollRect scroll;
        private Font font;
        private int revision = -1;
        private bool follow = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static void EnsureInstalled()
        {
            WebWrapperDiagnostics.EnsureInitialized();
            if (!WebWrapperDiagnostics.Enabled) return;
            if (FindFirstObjectByType<AndroidTestConsole>(FindObjectsInactive.Include) != null) return;
            var host = new GameObject("[Android Test Console]", typeof(RectTransform));
            DontDestroyOnLoad(host);
            host.AddComponent<AndroidTestConsole>();
            WebWrapperDiagnostics.Record("Console", "Logs UI installed. Startup fallback revision 2.");
        }

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            safeRoot = Rect("Safe Area", transform);
            panel = Rect("Console", safeRoot).gameObject;
            panel.AddComponent<Image>().color = new Color(0.035f, 0.05f, 0.08f, 0.99f);
            var viewport = Rect("Viewport", panel.transform);
            viewport.offsetMin = new Vector2(12, 64);
            viewport.offsetMax = new Vector2(-12, -58);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = Rect("Log Text", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            output = content.gameObject.AddComponent<Text>();
            output.font = font;
            output.fontSize = 15;
            output.color = new Color(0.83f, 0.92f, 0.94f);
            output.supportRichText = false;
            output.raycastTarget = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            Button("Copy", panel.transform, 0, 0.25f, () => GUIUtility.systemCopyBuffer = WebWrapperDiagnostics.Snapshot(), true);
            Button("Clear", panel.transform, 0.25f, 0.5f, WebWrapperDiagnostics.Clear, true);
            Button("Follow", panel.transform, 0.5f, 0.75f, () => { follow = !follow; }, true);
            Button("Close", panel.transform, 0.75f, 1, Toggle, true);
            Button("Logs (test APK)", safeRoot, 0, 1, Toggle, false);
            panel.SetActive(false);
        }

        private void Update()
        {

            if (panel.activeSelf != WebWrapperDiagnostics.ConsoleOpen)
            {
                panel.SetActive(WebWrapperDiagnostics.ConsoleOpen);
                revision = -1;
            }
            var scale = Mathf.Max(1f, Screen.width / 420f);
            GetComponent<CanvasScaler>().scaleFactor = scale;
            var safe = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            if (!panel.activeSelf || revision == WebWrapperDiagnostics.Revision) return;
            revision = WebWrapperDiagnostics.Revision;
            var snapshot = WebWrapperDiagnostics.Snapshot();

            output.text = snapshot.Length > 14000
                ? "[Earlier entries hidden here; Copy includes the full buffer]\n" + snapshot.Substring(snapshot.Length - 14000)
                : snapshot;
            Canvas.ForceUpdateCanvases();
            if (follow) scroll.verticalNormalizedPosition = 0;
        }

        private void Toggle()
        {
            WebWrapperDiagnostics.ConsoleOpen = !panel.activeSelf;
            panel.SetActive(WebWrapperDiagnostics.ConsoleOpen);
            revision = -1;
        }

        private void OnDestroy() { WebWrapperDiagnostics.ConsoleOpen = false; }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void Button(string title, Transform parent, float left, float right, UnityEngine.Events.UnityAction action, bool top)
        {
            var rect = Rect(title, parent);
            rect.anchorMin = new Vector2(left, top ? 1 : 0);
            rect.anchorMax = new Vector2(right, top ? 1 : 0);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0);
            rect.sizeDelta = new Vector2(-4, 52);
            rect.anchoredPosition = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1);
            rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
            var label = Rect("Label", rect).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 17;
            label.text = title;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }
    }
}
