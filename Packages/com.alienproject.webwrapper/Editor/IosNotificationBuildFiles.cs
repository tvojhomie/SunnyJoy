using System;
using System.Linq;
using System.Xml.Linq;

namespace AlienProject.WebWrapper.Editor
{
    public static class IosNotificationBuildFiles
    {
        public const string ExtensionName = "WebWrapperNotificationService";

        public static void ConfigureMainPlist(XDocument document, string cameraDescription, string photoDescription)
        {
            var dictionary = document.Root?.Element("dict") ??
                throw new ArgumentException("Info.plist has no root dictionary.", nameof(document));
            SetValue(dictionary, "FirebaseMessagingInstallationIdEnabled", new XElement("false"));
            SetValue(dictionary, "FirebaseMessagingAutoInitEnabled", new XElement("false"));
            SetValue(dictionary, "UnityNotificationRequestAuthorizationOnAppLaunch", new XElement("false"));
            SetValue(dictionary, "UnityNotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch", new XElement("false"));
            SetValue(dictionary, "UnityAddRemoteNotificationCapability", new XElement("true"));
            SetOptionalString(dictionary, "NSCameraUsageDescription", cameraDescription);
            SetOptionalString(dictionary, "NSPhotoLibraryUsageDescription", photoDescription);
            var modes = GetValue(dictionary, "UIBackgroundModes");
            if (modes == null || modes.Name != "array")
            {
                modes = new XElement("array");
                SetValue(dictionary, "UIBackgroundModes", modes);
            }
            if (!modes.Elements("string").Any(element => element.Value == "remote-notification"))
            {
                modes.Add(new XElement("string", "remote-notification"));
            }
        }

        public static XDocument CreateExtensionPlist(XDocument mainPlist)
        {
            var mainDictionary = mainPlist.Root?.Element("dict");
            var dictionary = new XElement("dict");
            SetValue(dictionary, "CFBundleDisplayName", new XElement("string", "Notifications"));
            SetValue(dictionary, "CFBundleExecutable", new XElement("string", "$(EXECUTABLE_NAME)"));
            SetValue(dictionary, "CFBundleIdentifier", new XElement("string", "$(PRODUCT_BUNDLE_IDENTIFIER)"));
            SetValue(dictionary, "CFBundleName", new XElement("string", "$(PRODUCT_NAME)"));
            SetValue(dictionary, "CFBundlePackageType", new XElement("string", "XPC!"));
            SetValue(dictionary, "CFBundleInfoDictionaryVersion", new XElement("string", "6.0"));
            SetValue(dictionary, "CFBundleShortVersionString", new XElement("string",
                GetValue(mainDictionary, "CFBundleShortVersionString")?.Value ?? "1.0"));
            SetValue(dictionary, "CFBundleVersion", new XElement("string",
                GetValue(mainDictionary, "CFBundleVersion")?.Value ?? "1"));
            SetValue(dictionary, "NSExtension", new XElement("dict",
                new XElement("key", "NSExtensionPointIdentifier"),
                new XElement("string", "com.apple.usernotifications.service"),
                new XElement("key", "NSExtensionPrincipalClass"),
                new XElement("string", ExtensionName)));
            return new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), dictionary));
        }

        private static XElement GetValue(XElement dictionary, string name)
        {
            return dictionary?.Elements("key").FirstOrDefault(key => key.Value == name)
                ?.ElementsAfterSelf().FirstOrDefault();
        }

        private static void SetValue(XElement dictionary, string name, XElement value)
        {
            var key = dictionary.Elements("key").FirstOrDefault(element => element.Value == name);
            if (key == null)
            {
                dictionary.Add(new XElement("key", name), value);
                return;
            }
            var oldValue = key.ElementsAfterSelf().FirstOrDefault();
            if (oldValue == null)
            {
                key.AddAfterSelf(value);
            }
            else
            {
                oldValue.ReplaceWith(value);
            }
        }

        private static void SetOptionalString(XElement dictionary, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                SetValue(dictionary, name, new XElement("string", value.Trim()));
            }
        }
    }
}
