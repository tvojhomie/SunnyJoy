using System.Linq;
using System.Xml.Linq;
using AlienProject.WebWrapper.Editor;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class LinkerPreservationTests
    {
        [Test]
        public void GeneratesExplicitLinkerFile_OutsidePackage()
        {
            var path = new WebWrapperLinkerProcessor().GenerateAdditionalLinkXmlFile(null, null);
            Assert.IsTrue(System.IO.File.Exists(path));
            StringAssert.Contains("Library/WebWrapperBuild/link.xml", path);
            Assert.IsNotEmpty(XDocument.Load(path).Root.Elements("assembly"));
        }

        [TestCase("AlienProject.WebWrapper.AppsFlyer")]
        [TestCase("AlienProject.WebWrapper.Firebase")]
        public void OptionalAdapterAssembly_IsAlwaysProcessedAndPreserved(string name)
        {
            var assembly = System.AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == name);
            Assert.IsTrue(assembly.GetCustomAttributes(false).Any(a => a.GetType().Name == "AlwaysLinkAssemblyAttribute"));
            var xml = XDocument.Parse(WebWrapperLinkerProcessor.CreateLinkXml());
            Assert.IsTrue(xml.Root.Elements("assembly").Any(a => (string)a.Attribute("fullname") == name &&
                (string)a.Attribute("preserve") == "all"));
        }

        [TestCase("AppsFlyer", "AppsFlyerSDK.AppsFlyer")]
        [TestCase("UniWebView-CSharp", "UniWebView")]
        [TestCase("UniWebView-CSharp", "UniWebViewEmbeddedToolbarConfig")]
        public void NativeAndReflectionReceivers_AreExplicitLinkerInputs(string assembly, string type)
        {
            var xml = XDocument.Parse(WebWrapperLinkerProcessor.CreateLinkXml());
            Assert.IsTrue(xml.Root.Elements("assembly").Where(a => (string)a.Attribute("fullname") == assembly)
                .Elements("type").Any(t => (string)t.Attribute("fullname") == type && (string)t.Attribute("preserve") == "all"));
        }
    }
}
