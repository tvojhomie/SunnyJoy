using System.IO;
using System.Xml.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEditor;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{

    public sealed class WebWrapperLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;
        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var path = Path.GetFullPath("Library/WebWrapperBuild/link.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, CreateLinkXml());
            return path;
        }

        public static string CreateLinkXml()
        {
            var root = new XElement("linker");
            foreach (var name in new[] { "AlienProject.WebWrapper.AppsFlyer", "AlienProject.WebWrapper.Firebase" })
                root.Add(new XElement("assembly", new XAttribute("fullname", name),
                    new XAttribute("preserve", "all"), new XAttribute("ignoreIfMissing", "1")));
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WebWrapperSettings).Assembly);
            var packagePath = package?.resolvedPath ?? "Packages/com.alienproject.webwrapper";
            foreach (var relative in new[] { "Runtime/Integrations/AppsFlyer/link.xml", "Runtime/Integrations/UniWebView/link.xml" })
                foreach (var assembly in XDocument.Load(Path.Combine(packagePath, relative)).Root.Elements("assembly"))
                    root.Add(new XElement(assembly));
            return new XDocument(root).ToString();
        }
    }

    public sealed class WebWrapperPlayerBuildGuard : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android ||
                PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                return;
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (settings == null || settings.UseMockServices) return;
            const string output = "Library/Bee/artifacts/Android/ManagedStripped";
            if (!Directory.Exists(output))
            {
                Debug.LogWarning("[WebWrapper] Stripped output not found; could not verify SDK assemblies for this Unity version.");
                return;
            }
            ValidateStrippedAssemblies(output);
            Debug.Log("[WebWrapper] Verified Android player contains AppsFlyer and Firebase adapters after stripping.");
        }

        public static void ValidateStrippedAssemblies(string output)
        {
            foreach (var name in new[] { "AlienProject.WebWrapper.AppsFlyer", "AlienProject.WebWrapper.Firebase", "AppsFlyer", "Firebase.App", "Firebase.Messaging" })
                if (!File.Exists(Path.Combine(output, name + ".dll")))
                    throw new BuildFailedException("[WebWrapper] Do not use this APK: required SDK assembly was stripped: " + name);
        }
    }
}
