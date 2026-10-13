#if UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    internal sealed class AndroidNotificationPostBuildProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (settings == null)
            {
                Debug.LogWarning("[WebWrapper] Settings were not found; Android notification resources were not configured.");
                return;
            }

            AndroidNotificationBuildConfigurator.Configure(path, settings);
        }
    }

    public static class AndroidNotificationBuildConfigurator
    {
        private static readonly XNamespace AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public static void Configure(string path, WebWrapperSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                manifestPath = Path.Combine(path, "AndroidManifest.xml");
            }

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[WebWrapper] Generated AndroidManifest.xml was not found under " + path);
                return;
            }

            var document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
            var application = document.Root?.Element("application");
            if (application == null)
            {
                Debug.LogWarning("[WebWrapper] Generated Android manifest has no application node.");
                return;
            }

            SetMetadata(application, "firebase_messaging_installation_id_enabled", "false", false);

            SetMetadata(application, "custom_notification_android_activity",
                "com.google.firebase.MessagingUnityPlayerActivity", false);
            SetMetadata(
                application,
                "com.google.firebase.messaging.default_notification_icon",
                "@drawable/webwrapper_notification_icon",
                true);
            SetMetadata(
                application,
                "com.google.firebase.messaging.default_notification_color",
                "@color/webwrapper_notification_color",
                true);
            SetMetadata(
                application,
                "com.google.firebase.messaging.default_notification_channel_id",
                SafeChannelId(settings.NotificationChannelId),
                false);

            document.Save(manifestPath);
            WriteNotificationResources(path, settings.NotificationAccentColor);
            Debug.Log("[WebWrapper] Applied Android FCM icon, accent color, default channel, and token registration mode.");
        }

        private static void SetMetadata(
            XElement application,
            string name,
            string value,
            bool resource)
        {
            var element = application.Elements("meta-data")
                .FirstOrDefault(candidate =>
                    string.Equals(
                        (string)candidate.Attribute(AndroidNamespace + "name"),
                        name,
                        StringComparison.Ordinal));
            if (element == null)
            {
                element = new XElement("meta-data");
                application.Add(element);
            }

            element.SetAttributeValue(AndroidNamespace + "name", name);
            element.SetAttributeValue(AndroidNamespace + (resource ? "resource" : "value"), value);
            element.SetAttributeValue(AndroidNamespace + (resource ? "value" : "resource"), null);
        }

        private static void WriteNotificationResources(string projectPath, Color color)
        {
            var resourcesPath = Path.Combine(projectPath, "src/main/res");
            var drawablePath = Path.Combine(resourcesPath, "drawable");
            var valuesPath = Path.Combine(resourcesPath, "values");
            Directory.CreateDirectory(drawablePath);
            Directory.CreateDirectory(valuesPath);

            File.WriteAllText(
                Path.Combine(drawablePath, "webwrapper_notification_icon.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<vector xmlns:android=\"http://schemas.android.com/apk/res/android\" " +
                "android:width=\"24dp\" android:height=\"24dp\" " +
                "android:viewportWidth=\"24\" android:viewportHeight=\"24\">\n" +
                "  <path android:fillColor=\"#FFFFFFFF\" " +
                "android:pathData=\"M12,22c1.1,0 2,-0.9 2,-2h-4c0,1.1 0.9,2 2,2zM18,16v-5c0,-3.1 -1.6,-5.6 -4.5,-6.3V4c0,-0.8 -0.7,-1.5 -1.5,-1.5S10.5,3.2 10.5,4v0.7C7.6,5.4 6,7.9 6,11v5l-2,2v1h16v-1l-2,-2z\" />\n" +
                "</vector>\n");

            File.WriteAllText(
                Path.Combine(valuesPath, "webwrapper_notification_colors.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<resources>\n" +
                "  <color name=\"webwrapper_notification_color\">#" +
                ColorUtility.ToHtmlStringRGB(color) +
                "</color>\n" +
                "</resources>\n");
        }

        private static string SafeChannelId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "webwrapper_messages" : value.Trim();
        }
    }
}
#endif
