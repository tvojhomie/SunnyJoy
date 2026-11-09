using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyJoy.Tests
{
    public sealed class ResponsiveLayoutTests
    {
        [TestCase(320, 568, 0, 20, 0, 0)]
        [TestCase(375, 812, 0, 44, 0, 34)]
        [TestCase(390, 844, 0, 59, 0, 34)]
        [TestCase(430, 932, 0, 59, 0, 34)]
        [TestCase(768, 1024, 0, 24, 0, 20)]
        [TestCase(1024, 1366, 0, 24, 0, 20)]
        [TestCase(1366, 1024, 0, 24, 0, 20)]
        [TestCase(844, 390, 59, 0, 59, 21)]
        [TestCase(844, 390, 59, 0, 0, 21)]
        [TestCase(280, 720, 0, 24, 0, 24)]
        [TestCase(2400, 1080, 0, 0, 0, 0)]
        public void AuthoredButtonsAndHeaders_StayInsideSafeArea(int width, int height, int left, int top, int right, int bottom)
        {
            var scene = EditorSceneManager.OpenScene(SunnyJoy.Editor.SunnyJoyUiSetup.ScenePath);
            var safe = new Rect(left, bottom, width - left - right, height - top - bottom);
            var size = new Vector2Int(width, height);
            int buttons = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.SetParent(null, false);
                canvasRect.localScale = Vector3.one;
                canvasRect.pivot = Vector2.one * .5f;
                canvasRect.sizeDelta = size;
                canvasRect.position = new Vector3(width / 2f, height / 2f, 0);
                SunnyJoyViewport.Ensure(canvas).ApplyViewport(size, safe);
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    AssertInside((RectTransform)button.transform, safe);
                    buttons++;
                }
                foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "TopPanel") AssertInside(rect, safe);
                foreach (Transform child in canvas.transform)
                    if (child.GetComponent<Image>() != null) AssertInside((RectTransform)child, SunnyJoyViewport.CalculateFrame(size, safe));
            }
            Assert.That(buttons, Is.GreaterThanOrEqualTo(77), "Exercise the actual menu, photo, shop and gameplay controls.");
        }

        [TestCase(390, 844, 390)]
        [TestCase(1080, 1920, 1080)]
        [TestCase(768, 1024, 682.6667f)]
        [TestCase(2400, 1080, 720)]
        public void BackgroundFrame_FillsPortrait_AndLimitsWideScreens(int width, int height, float expectedWidth)
        {
            var size = new Vector2Int(width, height);
            var frame = SunnyJoyViewport.CalculateFrame(size, Rect.zero);
            Assert.That(frame.width, Is.EqualTo(expectedWidth).Within(.01f));
            Assert.That(frame.height, Is.EqualTo(height));
            Assert.That(frame.center, Is.EqualTo(new Vector2(width / 2f, height / 2f)));
            var camera = SunnyJoyViewport.CalculateCameraRect(size, Rect.zero);
            Assert.That(camera.x * width, Is.EqualTo(frame.x).Within(.01f));
            Assert.That(camera.width * width, Is.EqualTo(frame.width).Within(.01f));
            Assert.That(camera.height, Is.EqualTo(1));
        }

        [Test]
        public void FrameAndCamera_CenterInsideAnAsymmetricSafeArea()
        {
            var size = new Vector2Int(844, 390);
            var safe = new Rect(59, 21, 785, 369);
            var frame = SunnyJoyViewport.CalculateFrame(size, safe);
            var camera = SunnyJoyViewport.CalculateCameraRect(size, safe);
            Assert.That(frame.center.x, Is.EqualTo(safe.center.x).Within(.01f));
            Assert.That(camera.center.x * size.x, Is.EqualTo(safe.center.x).Within(.01f));
        }

        [Test]
        public void InnerContent_KeepsItsSize_ThroughPortraitLandscapeAndBack()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(go.transform, false);
                var content = MakeSafe(panel.transform);
                var viewport = SunnyJoyViewport.Ensure(canvas);
                foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(2400, 1080), new Vector2Int(390, 844) })
                {
                    var rect = (RectTransform)go.transform;
                    rect.sizeDelta = size;
                    rect.position = new Vector3(size.x / 2f, size.y / 2f, 0);
                    var safe = new Rect(0, 0, size.x, size.y);
                    viewport.ApplyViewport(size, safe);
                    var corners = new Vector3[4];
                    ((RectTransform)content.transform).GetWorldCorners(corners);
                    var expected = SunnyJoySafeArea.FitViewport(safe, size, new Vector2(960, 1920));
                    Assert.That(corners[0].x, Is.EqualTo(expected.xMin).Within(.1f));
                    Assert.That(corners[2].x, Is.EqualTo(expected.xMax).Within(.1f));
                    Assert.That(corners[0].y, Is.EqualTo(expected.yMin).Within(.1f));
                    Assert.That(corners[2].y, Is.EqualTo(expected.yMax).Within(.1f));
                    Assert.That(go.transform.Find(SunnyJoyViewport.MarginsName).childCount, Is.EqualTo(2));
                    Assert.That(go.transform.childCount, Is.EqualTo(2));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NestedSafeArea_InheritsFit_AndRepeatedUpdatesDoNotShrinkIt()
        {
            var root = new GameObject("Viewport", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(390, 844);
                var outer = MakeSafe(root.transform);
                var inner = MakeSafe(outer.transform);
                var safe = new Rect(0, 34, 390, 751);
                for (int i = 0; i < 3; i++)
                {
                    outer.ApplyViewport(safe, new Vector2Int(390, 844));
                    inner.ApplyViewport(safe, new Vector2Int(390, 844));
                    Assert.That(inner.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(((RectTransform)inner.transform).rect.size,
                        Is.EqualTo(((RectTransform)outer.transform).rect.size));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void InvalidSafeArea_FallsBackToScreen_AndZeroSizeDoesNotDivideByZero()
        {
            var design = new Vector2(960, 1920);
            Assert.That(SunnyJoySafeArea.FitViewport(Rect.zero, new Vector2Int(400, 800), design),
                Is.EqualTo(new Rect(0, 0, 400, 800)));
            Assert.That(SunnyJoySafeArea.FitViewport(Rect.zero, Vector2Int.zero, design), Is.EqualTo(Rect.zero));
        }

        [TearDown]
        public void RestoreAuthoredScene() => EditorSceneManager.OpenScene(SunnyJoy.Editor.SunnyJoyUiSetup.ScenePath);

        private static SunnyJoySafeArea MakeSafe(Transform parent)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<SunnyJoySafeArea>();
        }

        private static void AssertInside(RectTransform rect, Rect safe)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            string path = rect.name;
            for (var parent = rect.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            foreach (var corner in corners)
            {
                Assert.That(corner.x, Is.InRange(safe.xMin - 1, safe.xMax + 1), path + " horizontal clipping");
                Assert.That(corner.y, Is.InRange(safe.yMin - 1, safe.yMax + 1), path + " notch/home indicator overlap");
            }
        }
    }
}
