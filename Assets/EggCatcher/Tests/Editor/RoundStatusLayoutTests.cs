using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher.Tests
{
    public sealed class RoundStatusLayoutTests
    {
        [TestCase(320, 568, 20, 0)]
        [TestCase(390, 844, 59, 34)]
        [TestCase(591, 1280, 0, 0)]
        [TestCase(1080, 2400, 72, 32)]
        [TestCase(768, 1024, 24, 20)]
        [TestCase(2400, 1080, 0, 0)]
        public void HudStaysAtSafeTop_AndRushIsCenteredBelowIt(int width, int height, int topInset, int bottomInset)
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            var game = Object.FindFirstObjectByType<EggCatcherGame>(FindObjectsInactive.Include);
            var canvas = game.transform.Find("GameCanvas").GetComponent<Canvas>();
            var top = canvas.transform.Find("HudSafeArea/TopPanel");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EggCatcherGame).GetField("uiFont", flags).SetValue(game, top.Find("ScorePanel/Score").GetComponent<Text>().font);
            typeof(EggCatcherGame).GetMethod("BuildRoundStatusUi", flags).Invoke(game, null);
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.SetParent(null, false);
            canvasRect.localScale = Vector3.one;
            canvasRect.pivot = Vector2.one * .5f;
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.position = new Vector3(width / 2f, height / 2f, 0);
            var size = new Vector2Int(width, height);
            EggCatcherViewport.Ensure(canvas).ApplyViewport(size);
            var safe = new Rect(0, bottomInset, width, height - topInset - bottomInset);
            canvas.GetComponentInChildren<EggCatcherSafeArea>().ApplyViewport(safe, size);
            Canvas.ForceUpdateCanvases();

            var status = top.Find("Round Status");
            var labels = status.GetComponentsInChildren<Text>();
            labels.Single(label => label.name == "Rush Status").text = "SHIELD  •  EGG RUSH ×2  6s";
            labels.Single(label => label.name == "Combo").text = "×5 · 999";
            Assert.That(status.Find("Round Mission"), Is.Null);
            Assert.That(labels.Length, Is.EqualTo(2));
            Assert.That(Bounds(status.Find("Rush Status")).center.x, Is.EqualTo(width / 2f).Within(.1f));
            Assert.That(Bounds(status.Find("Rush Progress")).center.x, Is.EqualTo(width / 2f).Within(.1f));
            var mainPanels = new[] { top.Find("ScorePanel"), top.Find("Panel") };
            float scale = Bounds(top).width / ((RectTransform)top).rect.width;
            foreach (var panel in mainPanels)
            {
                Assert.That(safe.yMax - Bounds(panel).yMax, Is.EqualTo(24 * scale).Within(.1f),
                    panel.name + " must stay near the safe top edge, not the centered design frame");
                Assert.That(Bounds(panel).yMax, Is.LessThanOrEqualTo(safe.yMax));
            }
            var widgets = new[] { status.Find("Combo"), status.Find("Rush Status"), status.Find("Rush Progress") };
            foreach (var widget in widgets)
            {
                var bounds = Bounds(widget);
                foreach (var panel in mainPanels)
                    Assert.That(bounds.yMax, Is.LessThan(Bounds(panel).yMin - 1), widget.name + " touches main HUD");
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
            }
            for (int i = 0; i < widgets.Length; i++)
            for (int j = i + 1; j < widgets.Length; j++)
                Assert.That(Bounds(widgets[i]).Overlaps(Bounds(widgets[j])), Is.False, widgets[i].name + " overlaps " + widgets[j].name);
            foreach (var graphic in status.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            foreach (var label in labels)
                Assert.That(LayoutUtility.GetPreferredWidth(label.rectTransform), Is.LessThanOrEqualTo(label.rectTransform.rect.width), label.name + " wraps");
        }

        private static Rect Bounds(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [TearDown]
        public void RestoreScene() => EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
    }
}
