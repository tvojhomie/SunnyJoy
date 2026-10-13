using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace EggCatcher.Editor
{
    internal static class EggCatcherPhotoUsagePostBuild
    {

        [PostProcessBuild(110)]
        private static void ConfigurePhotoUsage(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;
            var path = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(path)) throw new BuildFailedException("EggCatcher could not find the exported iOS Info.plist.");
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var dictionary = document.Root?.Element("dict");
            if (dictionary == null) throw new BuildFailedException("The exported Info.plist has no root dictionary.");
            var changed = AddIfMissing(dictionary, "NSCameraUsageDescription", "Take a photo for your player profile.");
            changed |= AddIfMissing(dictionary, "NSPhotoLibraryUsageDescription", "Choose a photo for your player profile.");
            if (changed) document.Save(path);
#if UNITY_IOS
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            var projectChanged = false;
            if (!project.ContainsFramework(frameworkTarget, "AVFoundation.framework"))
            {
                project.AddFrameworkToProject(frameworkTarget, "AVFoundation.framework", false);
                projectChanged = true;
            }
            const string nativeSource = "Libraries/Plugins/iOS/EggCatcherPhotoPicker.mm";
            var sourceGuid = project.FindFileGuidByProjectPath(nativeSource);
            if (string.IsNullOrEmpty(sourceGuid)) sourceGuid = project.FindFileGuidByRealPath(nativeSource);
            if (string.IsNullOrEmpty(sourceGuid))
                throw new BuildFailedException("EggCatcherPhotoPicker.mm is missing from the Xcode project. " +
                    "Enable iOS for Assets/Plugins/iOS/EggCatcherPhotoPicker.mm in the Plugin Inspector and export again.");
            var flags = project.GetCompileFlagsForFile(frameworkTarget, sourceGuid) ?? new List<string>();
            projectChanged |= flags.RemoveAll(flag => flag == "-fno-objc-arc") > 0;
            if (!flags.Contains("-fobjc-arc"))
            {
                flags.Add("-fobjc-arc");
                projectChanged = true;
            }
            project.SetCompileFlagsForFile(frameworkTarget, sourceGuid, flags);
            if (projectChanged) project.WriteToFile(projectPath);
#endif
        }

        private static bool AddIfMissing(XElement dictionary, string name, string text)
        {
            if (dictionary.Elements("key").Any(key => key.Value == name)) return false;
            dictionary.Add(new XElement("key", name), new XElement("string", text));
            return true;
        }
    }
}
