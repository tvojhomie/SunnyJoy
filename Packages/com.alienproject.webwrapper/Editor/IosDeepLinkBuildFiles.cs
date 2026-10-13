using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AlienProject.WebWrapper.Editor
{
    public static class IosDeepLinkBuildFiles
    {
        public static string[] AssociatedDomains(XDocument entitlements, string host)
        {
            var domains = Value(entitlements?.Root?.Element("dict"), "com.apple.developer.associated-domains")
                ?.Elements("string").Select(element => element.Value).ToList() ?? new List<string>();
            if (!string.IsNullOrWhiteSpace(host))
            {
                host = host.Trim().ToLowerInvariant();
                if (!host.Contains(".") || Uri.CheckHostName(host) != UriHostNameType.Dns)
                    throw new ArgumentException("OneLink Domain must be a hostname without https://, path or port.");
                domains.Add("applinks:" + host);
            }
            return domains.Distinct(StringComparer.Ordinal).ToArray();
        }

        public static void ConfigureUriScheme(XDocument plist, string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme)) return;
            scheme = scheme.Trim();
            if (!Regex.IsMatch(scheme, @"^[A-Za-z][A-Za-z0-9+.-]*$") ||
                string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("iOS URL scheme must be a custom scheme without :// or a path.");
            var root = plist.Root?.Element("dict") ?? throw new ArgumentException("Info.plist has no root dictionary.");
            var types = Value(root, "CFBundleURLTypes");
            if (types == null)
            {
                types = new XElement("array");
                root.Add(new XElement("key", "CFBundleURLTypes"), types);
            }
            if (types.Name != "array") throw new ArgumentException("CFBundleURLTypes must be an array.");
            if (types.Elements("dict").Any(type => Value(type, "CFBundleURLSchemes")?.Elements("string")
                .Any(value => string.Equals(value.Value, scheme, StringComparison.OrdinalIgnoreCase)) == true)) return;
            types.Add(new XElement("dict", new XElement("key", "CFBundleURLName"), new XElement("string", "WebWrapper.OneLink"),
                new XElement("key", "CFBundleURLSchemes"), new XElement("array", new XElement("string", scheme))));
        }

        private static XElement Value(XElement dict, string key) => dict?.Elements("key")
            .FirstOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault();
    }
}
