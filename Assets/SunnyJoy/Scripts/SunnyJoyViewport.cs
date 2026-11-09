using UnityEngine;
using UnityEngine.UI;

namespace SunnyJoy
{
    [DefaultExecutionOrder(-500), DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class SunnyJoyViewport : MonoBehaviour
    {
        public const string MarginsName = "Camera Margins";
        private RectTransform margins;
        private Image leftMargin, rightMargin;
        private Vector2Int lastSize;
        private Rect lastSafeArea;
        private Vector2 lastCanvasSize;
        private int lastChildCount;

        public static SunnyJoyViewport Ensure(Canvas canvas)
        {
            var viewport = canvas.GetComponent<SunnyJoyViewport>() ?? canvas.gameObject.AddComponent<SunnyJoyViewport>();
            viewport.ApplyViewport(new Vector2Int(Screen.width, Screen.height), Screen.safeArea);
            return viewport;
        }

        private void OnEnable() => ApplyViewport(new Vector2Int(Screen.width, Screen.height), Screen.safeArea);

        private void LateUpdate()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (size != lastSize || safeArea != lastSafeArea || ((RectTransform)transform).rect.size != lastCanvasSize ||
                transform.childCount != lastChildCount)
                ApplyViewport(size, safeArea);
            if (margins != null && margins.GetSiblingIndex() != transform.childCount - 1) margins.SetAsLastSibling();
            UpdateColor();
        }

        public void ApplyViewport(Vector2Int size, Rect safeArea)
        {
            if (size.x <= 0 || size.y <= 0) return;
            EnsureMargins();
            var frame = CalculateFrame(size, safeArea);
            float min = frame.xMin / size.x;
            float max = frame.xMax / size.x;
            foreach (Transform child in transform)
            {
                if (child == margins || !(child is RectTransform rect) || child.GetComponent<SunnyJoySafeArea>() != null) continue;
                SetRect(rect, new Vector2(min, 0), new Vector2(max, 1));
            }
            SetRect(leftMargin.rectTransform, Vector2.zero, new Vector2(min, 1));
            SetRect(rightMargin.rectTransform, new Vector2(max, 0), Vector2.one);
            leftMargin.gameObject.SetActive(min > .0001f);
            rightMargin.gameObject.SetActive(max < .9999f);
            margins.SetAsLastSibling();
            UpdateColor();
            lastSize = size;
            lastSafeArea = safeArea;
            lastCanvasSize = ((RectTransform)transform).rect.size;
            lastChildCount = transform.childCount;
            foreach (var layout in GetComponentsInChildren<SunnyJoySafeArea>(true))
                layout.ApplyViewport(safeArea, size);
        }

        public static Rect CalculateFrame(Vector2Int size, Rect safeArea)
        {
            if (size.x <= 0 || size.y <= 0) return Rect.zero;
            float left = Mathf.Clamp(safeArea.xMin, 0, size.x);
            float right = Mathf.Clamp(safeArea.xMax, 0, size.x);
            if (right <= left)
            {
                left = 0;
                right = size.x;
            }

            float width = Mathf.Min(right - left, size.y * (2f / 3f));
            return new Rect((left + right - width) * .5f, 0, width, size.y);
        }

        public static Rect CalculateCameraRect(Vector2Int size, Rect safeArea)
        {
            if (size.x <= 0 || size.y <= 0) return new Rect(0, 0, 1, 1);
            var frame = CalculateFrame(size, safeArea);
            return new Rect(frame.x / size.x, 0, frame.width / size.x, 1);
        }

        private void EnsureMargins()
        {
            if (margins != null) return;
            margins = transform.Find(MarginsName) as RectTransform;
            if (margins != null)
            {
                leftMargin = margins.Find("Left").GetComponent<Image>();
                rightMargin = margins.Find("Right").GetComponent<Image>();
                return;
            }
            var root = new GameObject(MarginsName, typeof(RectTransform));
            root.layer = gameObject.layer;
            margins = (RectTransform)root.transform;
            margins.SetParent(transform, false);
            SetRect(margins, Vector2.zero, Vector2.one);
            leftMargin = CreateMargin("Left");
            rightMargin = CreateMargin("Right");
        }

        private Image CreateMargin(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(margins, false);
            return go.GetComponent<Image>();
        }

        private void UpdateColor()
        {
            if (leftMargin == null || rightMargin == null) return;
            var camera = Camera.main;
            var color = camera != null ? camera.backgroundColor : Color.black;
            color.a = 1;
            if (leftMargin.color != color) leftMargin.color = color;
            if (rightMargin.color != color) rightMargin.color = color;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
