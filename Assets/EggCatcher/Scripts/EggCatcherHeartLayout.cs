using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{
    public static class EggCatcherHeartLayout
    {
        public static void Configure(Transform health)
        {
            var layout = health.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
            layout.padding = new RectOffset();
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childScaleWidth = layout.childScaleHeight = false;
            foreach (var image in health.GetComponentsInChildren<Image>(true))
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(80, 90);
                image.rectTransform.localScale = Vector3.one;
            }
        }

        public static void Set(Image image, bool filled, Sprite full, Sprite empty)
        {
            image.sprite = filled ? full : empty;

            image.rectTransform.localScale = Vector3.one * (filled ? 1 : .8f);
        }
    }
}
