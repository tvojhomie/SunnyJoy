using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EggCatcher.Editor
{

    public static class EggCatcherLayoutPreview
    {
        [MenuItem("Tools/Egg Catcher/Capture Screen Adaptation Preview")]
        public static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the scene and exit Play Mode before capturing layout previews.");
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            if (app == null) throw new System.InvalidOperationException("Open the MainMenu scene first.");
            string directory = Path.GetFullPath("Library/ScreenAdaptation");
            Directory.CreateDirectory(directory);
            var clone = Object.Instantiate(app.gameObject);
            var cameraObject = new GameObject("Layout preview camera");
            clone.hideFlags = cameraObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {

                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is EggCatcherApp || behaviour is EggCatcherPushPanel ||
                        behaviour is CanvasScaler || behaviour is GraphicRaycaster) behaviour.enabled = false;
                foreach (var transform in clone.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                var canvas = clone.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)clone.transform;
                rect.pivot = Vector2.one * .5f;
                rect.localScale = Vector3.one;
                rect.position = new Vector3(10000, 10000, 0);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Camera.main != null ? Camera.main.backgroundColor : Color.black;
                camera.cullingMask = 1 << 31;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200;
                camera.transform.position = rect.position + Vector3.back * 100;
                canvas.worldCamera = camera;
                var viewport = EggCatcherViewport.Ensure(canvas);
                var sizes = new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(768, 1024), new Vector2Int(2400, 1080) };
                var areas = new[] { new Rect(0, 0, 320, 548), new Rect(0, 34, 390, 751), new Rect(0, 20, 768, 980), new Rect(0, 0, 2400, 1080) };
                for (int i = 0; i < sizes.Length; i++)
                {
                    rect.sizeDelta = sizes[i];
                    camera.orthographicSize = sizes[i].y / 2f;
                    camera.aspect = (float)sizes[i].x / sizes[i].y;
                    viewport.ApplyViewport(sizes[i]);
                    foreach (var screen in new[] { "LoadingPanel", "MainMenu", "CreateProfilePanel", "SkinsPanel", "SettingPanel", "How to playPanel", "EditProfilePanel", "PushPanel", "Privacy PolicyPanel" })
                    {
                        foreach (Transform child in clone.transform)
                            child.gameObject.SetActive(child.name == screen || child.name == EggCatcherViewport.MarginsName);
                        foreach (var layout in clone.GetComponentsInChildren<EggCatcherSafeArea>(true))
                        {
                            layout.enabled = true;
                            layout.ApplyViewport(areas[i], sizes[i]);
                        }
                        Canvas.ForceUpdateCanvases();
                        Render(camera, sizes[i], Path.Combine(directory, sizes[i].x + "x" + sizes[i].y + "-" + screen + ".png"));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(cameraObject);
            }
            Debug.Log("[Egg Catcher] Authored layout previews saved to " + directory + ". These are layout renders, not device screenshots.");
        }

        private static void Render(Camera camera, Vector2Int size, string path)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(size.x, size.y, 24);
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
