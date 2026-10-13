using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    public sealed class WebWrapperSetupWindow : EditorWindow
    {
        private const string SettingsDirectory = "Assets/AlienProject/WebWrapper/Resources";
        private const string SettingsPath = SettingsDirectory + "/WebWrapperSettings.asset";

        private WebWrapperSettings _settings;
        private UnityEditor.Editor _settingsEditor;
        private Vector2 _scroll;

        [MenuItem("Tools/Alien Project/Web Wrapper Setup")]
        public static void Open()
        {
            GetWindow<WebWrapperSetupWindow>("Web Wrapper");
        }

        public static void CreateDefaultSettingsAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WebWrapperSettings>(SettingsPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            Directory.CreateDirectory(SettingsDirectory);
            var settings = CreateInstance<WebWrapperSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log("[WebWrapper] Created settings at " + SettingsPath);
        }

        [MenuItem("Tools/Alien Project/Clear Web Wrapper Install State")]
        public static void ClearInstallState()
        {
            new PlayerPrefsInstallStateStore().Clear();
            Debug.Log("[WebWrapper] Saved installation state cleared.");
        }

        [MenuItem("Tools/Alien Project/Apply Documented Android Settings")]
        public static void ApplyDocumentedAndroidSettings()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Plugins/Android"));
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.Android.forceInternetPermission = true;
            SetAndroidProperty("useCustomMainGradleTemplate", true);
            SetAndroidProperty("useCustomGradlePropertiesTemplate", true);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EnsureFirebaseAndroidManifestMetadata();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[WebWrapper] Prepared Assets/Plugins/Android and applied Android min API 30, " +
                "target API 35, INTERNET permission, custom Gradle templates, autorotation, " +
                "and Firebase FCM token metadata.");
        }

        [MenuItem("Tools/Alien Project/Resolve Android SDK Dependencies")]
        public static void ResolveAndroidSdkDependencies()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Plugins/Android"));
            var settingsType = FindType("GooglePlayServices.SettingsDialog");
            SetStaticBoolProperty(settingsType, "EnableAutoResolution", true);
            SetStaticBoolProperty(settingsType, "PromptBeforeAutoResolution", false);
            SetStaticBoolProperty(settingsType, "PatchMainTemplateGradle", true);
            SetStaticBoolProperty(settingsType, "PatchPropertiesTemplateGradle", true);

            var resolverType = FindType("GooglePlayServices.PlayServicesResolver");
            var resolveMethod = resolverType?.GetMethod(
                "Resolve",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(Action), typeof(bool), typeof(Action<bool>) },
                null);
            if (resolveMethod == null)
            {
                Debug.LogWarning(
                    "[WebWrapper] EDM4U Resolve API is unavailable. " +
                    "Install EDM4U 1.2.187 or newer first.");
                return;
            }

            Action<bool> onCompleted = succeeded => Debug.Log(succeeded
                ? "[WebWrapper] Android SDK dependency resolution completed."
                : "[WebWrapper] Android SDK dependency resolution failed; inspect the Console.");
            try
            {
                Debug.Log("[WebWrapper] Android SDK dependency resolution started.");
                resolveMethod.Invoke(null, new object[] { null, true, onCompleted });
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[WebWrapper] Could not start Android SDK dependency resolution: " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }

        [MenuItem("Tools/Alien Project/Apply Documented iOS Settings")]
        public static void ApplyDocumentedIosSettings()
        {
            PlayerSettings.iOS.targetOSVersionString = "18.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            if (!IosNotificationProjectSettings.Apply())
            {
                Debug.LogWarning("[WebWrapper] Install Mobile Notifications to configure deferred iOS authorization.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[WebWrapper] Applied iOS 18.0, autorotation, push capability and authorization after the custom prompt.");
        }

        private void OnEnable()
        {
            minSize = new Vector2(600f, 720f);
            FindSettings();
        }

        private void OnDisable()
        {
            if (_settingsEditor != null)
            {
                DestroyImmediate(_settingsEditor);
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Web Wrapper — приложение", EditorStyles.boldLabel);
            var apps = WebWrapperAppSetup.FindInLoadedScenes();
            if (apps.Length != 1)
            {
                EditorGUILayout.HelpBox(apps.Length > 1 ? "Оставь один активный WebWrapperApp в стартовой сцене." :
                    "Открой первую сцену игры и добавь WebWrapperApp. Старые рабочие настройки будут перенесены автоматически.", MessageType.Info);
                if (apps.Length == 0 && GUILayout.Button("Добавить компонент на сцену")) WebWrapperAppSetup.AddToScene();
                EditorGUILayout.EndScrollView();
                return;
            }
            if (_settingsEditor == null || _settingsEditor.target != apps[0])
            {
                if (_settingsEditor != null) DestroyImmediate(_settingsEditor);
                _settingsEditor = UnityEditor.Editor.CreateEditor(apps[0]);
            }
            if (GUILayout.Button("Выбрать объект в сцене")) Selection.activeGameObject = apps[0].gameObject;
            _settingsEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void FindSettings()
        {
            var guid = AssetDatabase.FindAssets("t:WebWrapperSettings").FirstOrDefault();
            _settings = string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<WebWrapperSettings>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void CreateSettings()
        {
            CreateDefaultSettingsAsset();
            _settings = AssetDatabase.LoadAssetAtPath<WebWrapperSettings>(SettingsPath);
        }

        private static void DrawCheck(string label, bool passed, string detail)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(passed ? "✓" : "•", GUILayout.Width(18f));
            EditorGUILayout.LabelField(label, GUILayout.Width(180f));
            EditorGUILayout.LabelField(passed ? "Ready" : detail, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsAtLeastIos18(string value)
        {
            return Version.TryParse(value, out var version) && version.Major >= 18;
        }

        private static bool IsValidApplicationIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var segments = value.Split('.');
            if (segments.Length < 2)
            {
                return false;
            }

            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment) || !char.IsLetter(segment[0]))
                {
                    return false;
                }

                for (var index = 1; index < segment.Length; index++)
                {
                    var character = segment[index];
                    if (!char.IsLetterOrDigit(character) && character != '_')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ConfigurationFileMatches(string fileName, string value, bool ios)
        {
            var path = Path.Combine(Application.dataPath, fileName);
            if (!File.Exists(path))
            {
                return false;
            }

            var contents = File.ReadAllText(path);
            return ios
                ? FirebaseConfigurationValidator.IosMatches(contents, value)
                : FirebaseConfigurationValidator.AndroidMatches(contents, value);
        }

        private static void SetAndroidProperty(string name, bool value)
        {
            var serializedSettings = GetPlayerSettingsSerializedObject();
            if (serializedSettings == null)
            {
                Debug.LogWarning("[WebWrapper] Could not access serialized PlayerSettings.");
                return;
            }
            var property = serializedSettings.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[WebWrapper] PlayerSettings property was not found: " + name);
                return;
            }

            property.boolValue = value;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool HasFirebaseAndroidManifestMetadata()
        {
            var path = Path.Combine(Application.dataPath, "Plugins/Android/AndroidManifest.xml");
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                XNamespace android = "http://schemas.android.com/apk/res/android";
                var metadata = XDocument.Load(path).Root?.Element("application")?.Elements("meta-data")
                    .FirstOrDefault(element =>
                        (string)element.Attribute(android + "name") == "firebase_messaging_installation_id_enabled");
                return (string)metadata?.Attribute(android + "value") == "false";
            }
            catch (System.Xml.XmlException)
            {
                return false;
            }
        }

        private static void EnsureFirebaseAndroidManifestMetadata()
        {
            var path = Path.Combine(Application.dataPath, "Plugins/Android/AndroidManifest.xml");
            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "[WebWrapper] AndroidManifest.xml is not available yet. " +
                    "Install Firebase Messaging, refresh Unity, then run this action again.");
                return;
            }

            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var application = document.Root?.Element("application");
            if (application == null)
            {
                Debug.LogWarning("[WebWrapper] Could not find the Android application manifest node.");
                return;
            }

            XNamespace android = "http://schemas.android.com/apk/res/android";
            var metadata = application.Elements("meta-data").FirstOrDefault(element =>
                (string)element.Attribute(android + "name") == "firebase_messaging_installation_id_enabled");
            if (metadata == null)
            {
                metadata = new XElement("meta-data", new XAttribute(android + "name", "firebase_messaging_installation_id_enabled"));
                application.Add(metadata);
            }
            metadata.SetAttributeValue(android + "value", "false");
            document.Save(path);
            AssetDatabase.ImportAsset("Assets/Plugins/Android/AndroidManifest.xml");
        }

        private static bool GetAndroidProperty(string name)
        {
            var serializedSettings = GetPlayerSettingsSerializedObject();
            var property = serializedSettings?.FindProperty(name);
            return property != null && property.boolValue;
        }

        private static SerializedObject GetPlayerSettingsSerializedObject()
        {
            var method = typeof(PlayerSettings).GetMethod(
                "GetSerializedObject",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(null, null) as SerializedObject;
        }

        private static void SetStaticBoolProperty(Type type, string propertyName, bool value)
        {
            var property = type?.GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            try
            {
                property.SetValue(null, value);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[WebWrapper] Could not configure EDM4U setting " + propertyName + ": " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }
    }
}
