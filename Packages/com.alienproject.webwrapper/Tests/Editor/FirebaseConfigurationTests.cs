using System.Linq;
using System.Xml.Linq;
using AlienProject.WebWrapper.Editor;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class FirebaseConfigurationTests
    {
        [Test]
        public void AndroidBundle_RejectsSubstringAndUnrelatedValues()
        {
            const string json = "{\"project_info\":{\"project_id\":\"com.test.app\"},\"client\":[" +
                                "{\"client_info\":{\"android_client_info\":{\"package_name\":\"com.test.app.extra\"}}}]}";
            Assert.That(FirebaseConfigurationValidator.AndroidMatches(json, "com.test.app"), Is.False);
            Assert.That(FirebaseConfigurationValidator.AndroidMatches(json, "com.test.app.extra"), Is.True);
        }

        [Test]
        public void AndroidBundle_MatchesAnyExactClientAndNotItsCaseVariant()
        {
            const string json = "{\"client\":[{\"client_info\":{\"android_client_info\":{\"package_name\":\"com.other.app\"}}}," +
                                "{\"client_info\":{\"android_client_info\":{\"package_name\":\"com.test.App\"}}}]}";
            Assert.That(FirebaseConfigurationValidator.AndroidMatches(json, "com.test.App"), Is.True);
            Assert.That(FirebaseConfigurationValidator.AndroidMatches(json, "com.test.app"), Is.False);
        }

        [Test]
        public void InvalidFirebaseFiles_DoNotPassOrThrow()
        {
            Assert.That(FirebaseConfigurationValidator.AndroidMatches("{invalid", "com.test.app"), Is.False);
            Assert.That(FirebaseConfigurationValidator.AndroidMatches("{}", "com.test.app"), Is.False);
            Assert.That(FirebaseConfigurationValidator.IosMatches("<plist", "com.test.app"), Is.False);
            Assert.That(FirebaseConfigurationValidator.IosMatches("<plist><dict/></plist>", "com.test.app"), Is.False);
        }

        [Test]
        public void IosBundle_OnlyReadsExactTopLevelBundleId()
        {
            const string plist = "<?xml version=\"1.0\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " +
                                 "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist><dict>" +
                                 "<key>PROJECT_ID</key><string>com.test.app</string>" +
                                 "<key>BUNDLE_ID</key><string>com.test.app.extra</string></dict></plist>";
            Assert.That(FirebaseConfigurationValidator.IosMatches(plist, "com.test.app"), Is.False);
            Assert.That(FirebaseConfigurationValidator.IosMatches(plist, "com.test.app.extra"), Is.True);
        }

        [Test]
        public void IosBuild_OverridesAutomaticPermissionAndPreservesUnrelatedPlistValues()
        {
            var plist = XDocument.Parse("<plist><dict>" +
                "<key>UnityNotificationRequestAuthorizationOnAppLaunch</key><true/>" +
                "<key>UIBackgroundModes</key><array><string>audio</string></array>" +
                "<key>FirebaseAppDelegateProxyEnabled</key><true/>" +
                "<key>NSMicrophoneUsageDescription</key><string>Existing microphone use</string>" +
                "</dict></plist>");

            IosNotificationBuildFiles.ConfigureMainPlist(plist, "Camera upload", "Photo upload");
            IosNotificationBuildFiles.ConfigureMainPlist(plist, "Camera upload", "Photo upload");
            var dictionary = plist.Root.Element("dict");
            Assert.That(Value(dictionary, "UnityNotificationRequestAuthorizationOnAppLaunch").Name.LocalName, Is.EqualTo("false"));
            Assert.That(Value(dictionary, "UnityNotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch").Name.LocalName, Is.EqualTo("false"));
            Assert.That(Value(dictionary, "FirebaseMessagingInstallationIdEnabled").Name.LocalName, Is.EqualTo("false"));
            Assert.That(Value(dictionary, "FirebaseMessagingAutoInitEnabled").Name.LocalName, Is.EqualTo("false"));
            Assert.That(Value(dictionary, "FirebaseAppDelegateProxyEnabled").Name.LocalName, Is.EqualTo("true"));
            Assert.That(Value(dictionary, "NSMicrophoneUsageDescription").Value, Is.EqualTo("Existing microphone use"));
            Assert.That(Value(dictionary, "NSCameraUsageDescription").Value, Is.EqualTo("Camera upload"));
            Assert.That(Value(dictionary, "UIBackgroundModes").Elements("string").Select(element => element.Value),
                Is.EquivalentTo(new[] { "audio", "remote-notification" }));
            Assert.That(dictionary.Elements("key").Count(key => key.Value == "FirebaseMessagingAutoInitEnabled"), Is.EqualTo(1));
        }

        [Test]
        public void ImageExtension_UsesNativeServiceClassAndSameAppVersions()
        {
            var main = XDocument.Parse("<plist><dict><key>CFBundleVersion</key><string>42</string>" +
                                       "<key>CFBundleShortVersionString</key><string>2.3.4</string></dict></plist>");
            var extension = IosNotificationBuildFiles.CreateExtensionPlist(main).Root.Element("dict");
            Assert.That(Value(extension, "CFBundleVersion").Value, Is.EqualTo("42"));
            Assert.That(Value(extension, "CFBundleShortVersionString").Value, Is.EqualTo("2.3.4"));
            Assert.That(Value(extension, "CFBundlePackageType").Value, Is.EqualTo("XPC!"));
            var extensionPoint = Value(extension, "NSExtension");
            Assert.That(Value(extensionPoint, "NSExtensionPointIdentifier").Value, Is.EqualTo("com.apple.usernotifications.service"));
            Assert.That(Value(extensionPoint, "NSExtensionPrincipalClass").Value, Is.EqualTo(IosNotificationBuildFiles.ExtensionName));
        }

        private static XElement Value(XElement dictionary, string key)
        {
            return dictionary.Elements("key").Single(element => element.Value == key).ElementsAfterSelf().First();
        }
    }
}
