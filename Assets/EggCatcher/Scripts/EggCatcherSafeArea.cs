using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class EggCatcherSafeArea : MonoBehaviour
    {

        [SerializeField] private Vector2 referenceSize = new Vector2(960, 1920);
        private bool alignToTop;
        private RectTransform rect;
        private Rect lastArea;
        private Vector2Int lastSize;
        private Vector2 lastParentSize;
        private readonly Vector3[] parentCorners = new Vector3[4];

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            Apply(true);
        }

        private void Update() => Apply(false);

        public void AlignToTop()
        {
            alignToTop = true;
            Apply(true);
        }

        private void Apply(bool force)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            if (!force && area == lastArea && size == lastSize && parent.rect.size == lastParentSize) return;
            ApplyViewport(area, size);
        }

        public void ApplyViewport(Rect area, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0) return;
            rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            lastArea = area;
            lastSize = size;
            lastParentSize = parent.rect.size;

            for (var ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var inherited = ancestor.GetComponent<EggCatcherSafeArea>();
                if (inherited == null || !inherited.enabled) continue;
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                return;
            }

            var parentFrame = ParentScreenRect(parent, size);
            var safe = area.width > 0 && area.height > 0 ? area : new Rect(0, 0, size.x, size.y);
            safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, parentFrame.xMin), Mathf.Max(safe.yMin, parentFrame.yMin),
                Mathf.Min(safe.xMax, parentFrame.xMax), Mathf.Min(safe.yMax, parentFrame.yMax));
            if (safe.width <= 0 || safe.height <= 0) safe = parentFrame;
            var frame = FitViewport(safe, size, referenceSize);
            if (alignToTop) frame.y = safe.yMax - frame.height;
            rect.anchorMin = rect.anchorMax = new Vector2((frame.center.x - parentFrame.xMin) / parentFrame.width,
                (frame.center.y - parentFrame.yMin) / parentFrame.height);
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = referenceSize;
            float scale = Mathf.Min(frame.width / parentFrame.width * parent.rect.width / referenceSize.x,
                frame.height / parentFrame.height * parent.rect.height / referenceSize.y);
            rect.localScale = Vector3.one * scale;
            if (TryGetComponent<Image>(out var artwork)) artwork.preserveAspect = true;
        }

        private Rect ParentScreenRect(RectTransform parent, Vector2Int size)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return new Rect(0, 0, size.x, size.y);
            var root = (RectTransform)canvas.rootCanvas.transform;
            if (root.rect.width <= 0 || root.rect.height <= 0) return new Rect(0, 0, size.x, size.y);
            parent.GetWorldCorners(parentCorners);
            var min = root.InverseTransformPoint(parentCorners[0]);
            var max = root.InverseTransformPoint(parentCorners[2]);
            return Rect.MinMaxRect((min.x - root.rect.xMin) / root.rect.width * size.x,
                (min.y - root.rect.yMin) / root.rect.height * size.y,
                (max.x - root.rect.xMin) / root.rect.width * size.x,
                (max.y - root.rect.yMin) / root.rect.height * size.y);
        }

        public static Rect FitViewport(Rect safeArea, Vector2Int screenSize, Vector2 designSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0 || designSize.x <= 0 || designSize.y <= 0)
                return Rect.zero;
            var safe = Rect.MinMaxRect(Mathf.Clamp(safeArea.xMin, 0, screenSize.x),
                Mathf.Clamp(safeArea.yMin, 0, screenSize.y), Mathf.Clamp(safeArea.xMax, 0, screenSize.x),
                Mathf.Clamp(safeArea.yMax, 0, screenSize.y));
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, screenSize.x, screenSize.y);
            float scale = Mathf.Min(safe.width / designSize.x, safe.height / designSize.y);
            var fitted = designSize * scale;
            return new Rect(safe.center - fitted * .5f, fitted);
        }
    }
}
