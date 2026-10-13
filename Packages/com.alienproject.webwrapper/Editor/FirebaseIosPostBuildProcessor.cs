using System;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

namespace AlienProject.WebWrapper.Editor
{
    internal static class FirebaseIosPostBuildProcessor
    {

        [PostProcessBuild(100)]
        private static void ConfigureIosProject(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                throw new BuildFailedException("[WebWrapper] iOS Info.plist was not found at " + plistPath);
            }

            var document = XDocument.Load(plistPath, LoadOptions.PreserveWhitespace);
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            IosNotificationBuildFiles.ConfigureMainPlist(document,
                settings?.WebViewCameraUsageDescription, settings?.WebViewPhotoLibraryUsageDescription);
            IosDeepLinkBuildFiles.ConfigureUriScheme(document, settings?.IosUrlScheme);
            document.Save(plistPath);

#if UNITY_IOS
            ConfigureCapabilitiesAndExtension(buildPath, document, settings);
#else
            throw new BuildFailedException("[WebWrapper] Switch the active build target to iOS before exporting.");
#endif
        }

#if UNITY_IOS
        private static void ConfigureCapabilitiesAndExtension(string buildPath, XDocument mainPlist, WebWrapperSettings settings)
        {
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var mainTarget = project.GetUnityMainTargetGuid();
            var entitlementsPath = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrWhiteSpace(entitlementsPath))
            {
                entitlementsPath = "WebWrapper.entitlements";
            }

            var capabilities = new ProjectCapabilityManager(projectPath, entitlementsPath, null, mainTarget);
            capabilities.AddPushNotifications(!IosNotificationProjectSettings.UsesProductionApns);
            capabilities.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            if (!string.IsNullOrWhiteSpace(settings?.OneLinkDomain))
            {
                var fullEntitlementsPath = Path.Combine(buildPath, entitlementsPath);
                var entitlements = File.Exists(fullEntitlementsPath) ? XDocument.Load(fullEntitlementsPath) : null;
                capabilities.AddAssociatedDomains(IosDeepLinkBuildFiles.AssociatedDomains(entitlements, settings.OneLinkDomain));
            }
            capabilities.WriteToFile();

            project.ReadFromFile(projectPath);
            var extensionDirectory = Path.Combine(buildPath, IosNotificationBuildFiles.ExtensionName);
            Directory.CreateDirectory(extensionDirectory);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FirebaseIosPostBuildProcessor).Assembly);
            if (package == null)
            {
                throw new BuildFailedException("[WebWrapper] Cannot locate the installed package's iOS notification template.");
            }
            var sourceTemplate = Path.Combine(package.resolvedPath,
                "Editor/Templates/WebWrapperNotificationService.m.txt");
            File.Copy(sourceTemplate, Path.Combine(extensionDirectory, "WebWrapperNotificationService.m"), true);
            var relativePlist = IosNotificationBuildFiles.ExtensionName + "/Info.plist";
            IosNotificationBuildFiles.CreateExtensionPlist(mainPlist).Save(Path.Combine(buildPath, relativePlist));

            var extensionTarget = project.TargetGuidByName(IosNotificationBuildFiles.ExtensionName);
            if (string.IsNullOrEmpty(extensionTarget))
            {
                extensionTarget = project.AddAppExtension(mainTarget,
                    IosNotificationBuildFiles.ExtensionName,
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) + ".notificationservice",
                    relativePlist);
            }

            var relativeSource = IosNotificationBuildFiles.ExtensionName + "/WebWrapperNotificationService.m";
            var fileGuid = project.FindFileGuidByProjectPath(relativeSource);
            if (string.IsNullOrEmpty(fileGuid))
            {
                fileGuid = project.AddFile(relativeSource, relativeSource, PBXSourceTree.Source);
            }
            project.AddFileToBuild(extensionTarget, fileGuid);
            project.AddFrameworkToProject(extensionTarget, "UserNotifications.framework", false);
            project.AddFrameworkToProject(extensionTarget, "Foundation.framework", false);
            project.SetBuildProperty(extensionTarget, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(extensionTarget, "CLANG_ENABLE_OBJC_ARC", "YES");
            project.SetBuildProperty(extensionTarget, "SKIP_INSTALL", "YES");
            project.SetBuildProperty(extensionTarget, "IPHONEOS_DEPLOYMENT_TARGET", PlayerSettings.iOS.targetOSVersionString);
            project.SetBuildProperty(extensionTarget, "TARGETED_DEVICE_FAMILY",
                project.GetBuildPropertyForAnyConfig(mainTarget, "TARGETED_DEVICE_FAMILY") ?? "1,2");
            project.SetBuildProperty(extensionTarget, "PRODUCT_BUNDLE_IDENTIFIER",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) + ".notificationservice");
            project.SetBuildProperty(extensionTarget, "GENERATE_INFOPLIST_FILE", "NO");
            project.SetBuildProperty(extensionTarget, "INFOPLIST_FILE", relativePlist);

            foreach (var configuration in project.BuildConfigNames())
            {
                var appConfiguration = project.BuildConfigByName(mainTarget, configuration);
                var extensionConfiguration = project.BuildConfigByName(extensionTarget, configuration);
                foreach (var key in new[] { "DEVELOPMENT_TEAM", "CODE_SIGN_STYLE", "CODE_SIGN_IDENTITY",
                             "CURRENT_PROJECT_VERSION", "MARKETING_VERSION" })
                {
                    var value = project.GetBuildPropertyForConfig(appConfiguration, key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        project.SetBuildPropertyForConfig(extensionConfiguration, key, value);
                    }
                }
            }

            project.WriteToFile(projectPath);
            Debug.Log("[WebWrapper] Added push/background capabilities and an embedded image notification extension. " +
                      "Sign the app and " + IosNotificationBuildFiles.ExtensionName + " with your Apple team. " +
                      "The extension uses native UserNotifications and needs no additional Firebase pod. " +
                      "Verify APNs credentials in Firebase; image pushes need aps.mutable-content=1 and fcm_options.image.");
        }
#endif
    }
}
