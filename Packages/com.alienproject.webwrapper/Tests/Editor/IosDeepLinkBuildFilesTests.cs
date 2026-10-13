using System;
using System.Linq;
using System.Xml.Linq;
using AlienProject.WebWrapper.Editor;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class IosDeepLinkBuildFilesTests
    {
        [Test]
        public void UriScheme_IsIdempotent_AndPreservesOtherAppSchemes()
        {
            var plist = XDocument.Parse("<plist><dict><key>CFBundleURLTypes</key><array><dict><key>CFBundleURLSchemes</key>" +
                "<array><string>existing-signin</string></array></dict></array></dict></plist>");
            IosDeepLinkBuildFiles.ConfigureUriScheme(plist, "com.gameandjoy.sunnyjoygame");
            IosDeepLinkBuildFiles.ConfigureUriScheme(plist, "com.gameandjoy.sunnyjoygame");
            Assert.That(plist.Descendants("string").Count(value => value.Value == "com.gameandjoy.sunnyjoygame"), Is.EqualTo(1));
            Assert.That(plist.ToString(), Does.Contain("existing-signin"));
        }

        [TestCase("https")]
        [TestCase("com.game://")]
        [TestCase("bad scheme")]
        public void RejectsInvalidUriScheme(string scheme) => Assert.Throws<ArgumentException>(() =>
            IosDeepLinkBuildFiles.ConfigureUriScheme(XDocument.Parse("<plist><dict/></plist>"), scheme));

        [Test]
        public void AssociatedDomains_PreserveExistingEntitlementsWithoutDuplicates()
        {
            var entitlements = XDocument.Parse("<plist><dict><key>com.apple.developer.associated-domains</key><array>" +
                "<string>webcredentials:example.com</string><string>applinks:sunnyjoygame.onelink.me</string></array></dict></plist>");
            CollectionAssert.AreEqual(new[] { "webcredentials:example.com", "applinks:sunnyjoygame.onelink.me" },
                IosDeepLinkBuildFiles.AssociatedDomains(entitlements, "sunnyjoygame.onelink.me"));
        }

        [TestCase("https://sunnyjoygame.onelink.me")]
        [TestCase("sunnyjoygame.onelink.me/etr4")]
        [TestCase("sunnyjoygame.onelink.me:443")]
        public void RejectsInvalidAssociatedDomain(string domain) => Assert.Throws<ArgumentException>(() =>
            IosDeepLinkBuildFiles.AssociatedDomains(null, domain));
    }
}
