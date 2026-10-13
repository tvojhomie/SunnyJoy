using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace EggCatcher
{
    public sealed partial class EggCatcherApp
    {
        private Sprite savedPhoto, draftPhoto;
        private Texture2D cropTexture;
        private RawImage cropPreview;
        private Slider cropZoom;
        private Vector2 cropCenter = new Vector2(.5f, .5f);
        private bool draftPhotoRemoved;
        private string pendingPhotoPath;
        private Sprite SavedAvatar => savedPhoto != null ? savedPhoto : avatars[data.AvatarIndex];
        private Sprite DraftPhoto => draftPhoto != null ? draftPhoto : draftPhotoRemoved ? null : savedPhoto;

        private void BindPhotos()
        {
            Bind("EditProfilePanel", "Icon/ProfilePhotoBtn", () => Node("EditProfilePanel", "ProfilePhotoPanel").gameObject.SetActive(true));
            const string choices = "ProfilePhotoPanel/Panel/Content/";
            Bind("EditProfilePanel", choices + "Cancel", () => Node("EditProfilePanel", "ProfilePhotoPanel").gameObject.SetActive(false));
            Bind("EditProfilePanel", choices + "TakeAPhotoBtn", () => PickPhoto(true));
            Bind("EditProfilePanel", choices + "ChooseFromGalery", () => PickPhoto(false));
            Get<Button>("EditProfilePanel", choices + "TakeAPhotoBtn").interactable = EggCatcherPhotoPicker.IsCameraSupported;
            Get<Button>("EditProfilePanel", choices + "ChooseFromGalery").interactable = EggCatcherPhotoPicker.IsLibrarySupported;
            Bind("EditProfilePanel", choices + "Remove", () =>
            {
                ClearDraftPhoto();
                draftPhotoRemoved = true;
                Get<Image>("EditProfilePanel", "Icon").sprite = avatars[editingAvatar];
                Node("EditProfilePanel", "ProfilePhotoPanel").gameObject.SetActive(false);
                RefreshAvatarSelection(true);
            });
            Bind("EditProfilePanel", "TakeAPhotoPanel/TopPanel/Back", CancelCrop);
            Bind("EditProfilePanel", "TakeAPhotoPanel/Cancel", CancelCrop);
            Bind("EditProfilePanel", "TakeAPhotoPanel/Done", AcceptCrop);
            var frame = Node("EditProfilePanel", "TakeAPhotoPanel/PhotoRamka");
            var viewport = new GameObject("PhotoViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(frame, false);
            var rect = (RectTransform)viewport.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(EggCatcherPhotoDrag));
            photo.transform.SetParent(viewport.transform, false);
            rect = (RectTransform)photo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            cropPreview = photo.GetComponent<RawImage>();

            var guide = new GameObject("CropGuide", typeof(RectTransform), typeof(Image));
            guide.transform.SetParent(viewport.transform, false);
            var guideRect = (RectTransform)guide.transform;
            guideRect.anchorMin = Vector2.zero;
            guideRect.anchorMax = Vector2.one;
            guideRect.offsetMin = guideRect.offsetMax = Vector2.zero;
            var guideImage = guide.GetComponent<Image>();
            guideImage.sprite = frame.GetComponent<Image>().sprite;
            guideImage.raycastTarget = false;
            photo.GetComponent<EggCatcherPhotoDrag>().Dragged = delta =>
            {
                if (cropTexture == null) return;
                var rect = cropPreview.rectTransform;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Vector2.zero, camera, out var from);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, delta, camera, out var to);
                var movement = to - from;
                cropCenter -= new Vector2(movement.x / Mathf.Max(1, rect.rect.width) * cropPreview.uvRect.width,
                    movement.y / Mathf.Max(1, rect.rect.height) * cropPreview.uvRect.height);
                UpdateCrop();
            };
            var slider = Node("EditProfilePanel", "TakeAPhotoPanel/Zoom/Slider");
            cropZoom = slider.gameObject.AddComponent<Slider>();
            cropZoom.minValue = 1;
            cropZoom.maxValue = 3;
            cropZoom.fillRect = slider.Find("Fill") as RectTransform;
            var handle = slider.Find("Fill/Image") as RectTransform;
            handle.SetParent(slider, false);
            cropZoom.handleRect = handle;
            cropZoom.targetGraphic = cropZoom.handleRect.GetComponent<Image>();
            cropZoom.onValueChanged.AddListener(_ => UpdateCrop());
            AddZoomButton("MinusIcon", -.2f);
            AddZoomButton("PlusIcon", .2f);
        }

        private void AddZoomButton(string name, float step)
        {
            var node = Node("EditProfilePanel", "TakeAPhotoPanel/Zoom/" + name);
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = node.GetComponent<Image>();
            button.onClick.AddListener(() => cropZoom.value += step);
        }

        private void PickPhoto(bool camera)
        {
            Node("EditProfilePanel", "ProfilePhotoPanel").gameObject.SetActive(false);
            pendingPhotoPath = Path.Combine(Application.temporaryCachePath, "egg-profile-import.jpg");
            if (camera) EggCatcherPhotoPicker.PickFromCamera(gameObject.name, nameof(OnPhotoPicked), pendingPhotoPath);
            else EggCatcherPhotoPicker.PickFromLibrary(gameObject.name, nameof(OnPhotoPicked), pendingPhotoPath);
        }

        [Preserve]
        public void OnPhotoPicked(string json)
        {
            var expectedPath = pendingPhotoPath;
            pendingPhotoPath = null;
            if (!screens["EditProfilePanel"].activeInHierarchy || string.IsNullOrEmpty(expectedPath)) return;
            try
            {
                var result = JsonUtility.FromJson<EggCatcherPhotoPicker.Result>(json);
                if (result == null || result.cancelled) return;
                if (!string.IsNullOrEmpty(result.error)) { ShowNotice("Photo unavailable", result.error); return; }
                if (result.path != expectedPath) throw new InvalidDataException("Unexpected photo destination.");
                ReleaseCrop();
                cropTexture = ReadPhoto(result.path);
                cropPreview.texture = cropTexture;
                cropCenter = new Vector2(.5f, .5f);
                cropZoom.SetValueWithoutNotify(1);
                Node("EditProfilePanel", "TakeAPhotoPanel").gameObject.SetActive(true);
                UpdateCrop();
            }
            catch (Exception)
            {
                ReleaseCrop();
                ShowNotice("Photo unavailable", "Please choose another photo.");
            }
            finally
            {
                if (File.Exists(expectedPath)) File.Delete(expectedPath);
            }
        }

        private void UpdateCrop()
        {
            if (cropTexture == null) return;
            float side = Mathf.Min(cropTexture.width, cropTexture.height) / Mathf.Max(1, cropZoom.value);
            var size = new Vector2(side / cropTexture.width, side / cropTexture.height);
            cropCenter.x = Mathf.Clamp(cropCenter.x, size.x / 2, 1 - size.x / 2);
            cropCenter.y = Mathf.Clamp(cropCenter.y, size.y / 2, 1 - size.y / 2);
            cropPreview.uvRect = new Rect(cropCenter - size / 2, size);
        }

        private void CancelCrop()
        {
            ReleaseCrop();
            Node("EditProfilePanel", "TakeAPhotoPanel").gameObject.SetActive(false);
        }

        private void AcceptCrop()
        {
            if (cropTexture == null) return;
            var pixels = new Color[256 * 256];
            var uv = cropPreview.uvRect;

            uv = new Rect(uv.x + uv.width * .1f, uv.y + uv.height * .1f, uv.width * .8f, uv.height * .8f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    var pixel = cropTexture.GetPixelBilinear(uv.x + (x + .5f) / 256 * uv.width,
                        uv.y + (y + .5f) / 256 * uv.height);
                    pixel.a *= Mathf.Clamp01(128f - new Vector2(x - 127.5f, y - 127.5f).magnitude);
                    pixels[y * 256 + x] = pixel;
                }
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            image.SetPixels(pixels);
            image.Apply();
            ClearDraftPhoto();
            draftPhoto = Sprite.Create(image, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f));
            draftPhotoRemoved = false;
            Get<Image>("EditProfilePanel", "Icon").sprite = draftPhoto;
            RefreshAvatarSelection(true);
            CancelCrop();
        }

        private bool CommitPhoto()
        {
            try
            {
                if (draftPhoto != null)
                {
                    var file = "egg-profile-" + Guid.NewGuid().ToString("N") + ".png";
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, file), draftPhoto.texture.EncodeToPNG());
                    ForgetSavedPhoto();
                    data.CustomAvatarFileName = file;
                }
                else if (draftPhotoRemoved) ForgetSavedPhoto();
                return true;
            }
            catch (Exception)
            {
                ShowNotice("Couldn't save photo", "Please try again. Your profile has not been changed.");
                return false;
            }
        }

        private void LoadSavedPhoto()
        {
            ReleaseSavedPhoto();
            if (string.IsNullOrEmpty(data.CustomAvatarFileName)) return;
            try
            {
                var image = ReadPhoto(Path.Combine(Application.persistentDataPath, data.CustomAvatarFileName));
                savedPhoto = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(.5f, .5f));
            }
            catch (Exception) {                                                                      }
        }

        private static Texture2D ReadPhoto(string path)
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Photo is too large.");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (image.LoadImage(File.ReadAllBytes(path), false)) return image;
            Destroy(image);
            throw new InvalidDataException("Photo is not readable.");
        }

        private void ForgetSavedPhoto()
        {
            string file = data.CustomAvatarFileName;
            if (!string.IsNullOrEmpty(file) && file.StartsWith("egg-profile-", StringComparison.Ordinal) && Path.GetFileName(file) == file)
            {
                var path = Path.Combine(Application.persistentDataPath, file);
                if (File.Exists(path)) File.Delete(path);
            }
            data.CustomAvatarFileName = string.Empty;
            ReleaseSavedPhoto();
        }

        private void ReleaseSavedPhoto()
        {
            if (savedPhoto != null) { Destroy(savedPhoto.texture); Destroy(savedPhoto); }
            savedPhoto = null;
        }
        private void ClearDraftPhoto()
        {
            if (draftPhoto != null) { Destroy(draftPhoto.texture); Destroy(draftPhoto); }
            draftPhoto = null;
        }
        private void ReleaseCrop()
        {
            if (cropPreview != null) cropPreview.texture = null;
            if (cropTexture != null) Destroy(cropTexture);
            cropTexture = null;
        }
    }

    public sealed class EggCatcherPhotoDrag : MonoBehaviour, IDragHandler
    {
        public Action<Vector2> Dragged;
        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.delta);
    }
}
