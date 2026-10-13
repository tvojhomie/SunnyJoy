using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EggCatcher
{

    public static class EggCatcherPhotoPicker
    {
        [Serializable]
        public sealed class Result
        {
            public bool cancelled;
            public string error = string.Empty;
            public string path = string.Empty;
        }

        public static bool IsCameraSupported
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_IOS
                return ECG_PhotoPicker_IsCameraSupported() != 0;
#else
                return false;
#endif
            }
        }

        public static bool IsLibrarySupported
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_IOS
                return ECG_PhotoPicker_IsLibrarySupported() != 0;
#else
                return false;
#endif
            }
        }

        public static void PickFromLibrary(string receiverGameObjectName, string callbackMethod, string destinationPath)
        {
            Pick(false, receiverGameObjectName, callbackMethod, destinationPath);
        }

        public static void PickFromCamera(string receiverGameObjectName, string callbackMethod, string destinationPath)
        {
            Pick(true, receiverGameObjectName, callbackMethod, destinationPath);
        }

        private static void Pick(bool camera, string receiver, string method, string destination)
        {
            if (string.IsNullOrWhiteSpace(receiver) || string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("A receiver GameObject name and callback method are required.");
            if (!TryValidateDestination(destination, out var fullPath))
            {
                Deliver(receiver, method, new Result { error = "Choose a JPEG destination inside the app's private storage." });
                return;
            }
#if UNITY_EDITOR
            PickEditorImage(camera, receiver, method, fullPath);
#elif UNITY_IOS
            ECG_PhotoPicker_Pick(camera ? 1 : 0, receiver, method, fullPath);
#else
            Deliver(receiver, method, new Result { error = "Profile photo selection is currently available on iOS and in the Unity Editor." });
#endif
        }

        private static bool TryValidateDestination(string path, out string fullPath)
        {
            fullPath = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
                var extension = Path.GetExtension(path);
                if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) return false;
                fullPath = Path.GetFullPath(path);
                return IsInside(fullPath, Application.persistentDataPath) || IsInside(fullPath, Application.temporaryCachePath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsInside(string path, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        private static void PickEditorImage(bool camera, string receiver, string method, string destination)
        {
            Texture2D image = null;
            Result result;
            try
            {
                var source = EditorUtility.OpenFilePanelWithFilters(
                    camera ? "Simulate camera: choose a profile photo" : "Choose a profile photo",
                    string.Empty, new[] { "Image files", "png,jpg,jpeg" });
                if (string.IsNullOrEmpty(source))
                {
                    result = new Result { cancelled = true };
                }
                else if (new FileInfo(source).Length > 32 * 1024 * 1024)
                {
                    result = new Result { error = "Choose a photo smaller than 32 MB." };
                }
                else
                {
                    image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!image.LoadImage(File.ReadAllBytes(source), false))
                        throw new InvalidDataException("Choose a readable PNG or JPEG image.");
                    var jpeg = image.EncodeToJPG(90);
                    if (jpeg == null || jpeg.Length == 0) throw new InvalidDataException("The selected photo could not be converted.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllBytes(destination, jpeg);
                    result = new Result { path = destination };
                }
            }
            catch (Exception)
            {
                result = new Result { error = "The photo could not be read or saved. Please choose another image." };
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
            Deliver(receiver, method, result);
        }
#endif

        private static void Deliver(string receiver, string method, Result result)
        {
            var json = JsonUtility.ToJson(result);
#if UNITY_EDITOR

            EditorApplication.delayCall += () => SendToReceiver(receiver, method, json);
#else
            SendToReceiver(receiver, method, json);
#endif
        }

        private static void SendToReceiver(string receiver, string method, string json)
        {
            var target = GameObject.Find(receiver);
            if (target != null) target.SendMessage(method, json, SendMessageOptions.DontRequireReceiver);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int ECG_PhotoPicker_IsCameraSupported();
        [DllImport("__Internal")] private static extern int ECG_PhotoPicker_IsLibrarySupported();
        [DllImport("__Internal")] private static extern void ECG_PhotoPicker_Pick(
            int camera, string receiver, string method, string destination);
#endif
    }
}
