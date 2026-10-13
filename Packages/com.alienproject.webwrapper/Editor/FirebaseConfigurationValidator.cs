using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    public static class FirebaseConfigurationValidator
    {
        public static bool AndroidMatches(string json, string bundleId)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(bundleId))
            {
                return false;
            }

            try
            {
                var config = JsonUtility.FromJson<AndroidConfiguration>(json);
                return config?.client != null && config.client.Any(client =>
                    string.Equals(client?.client_info?.android_client_info?.package_name,
                        bundleId, StringComparison.Ordinal));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool IosMatches(string plist, string bundleId)
        {
            if (string.IsNullOrWhiteSpace(plist) || string.IsNullOrWhiteSpace(bundleId))
            {
                return false;
            }

            try
            {
                using (var text = new StringReader(plist))
                using (var reader = XmlReader.Create(text, new XmlReaderSettings
                       { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                {
                    var dictionary = XDocument.Load(reader).Root?.Element("dict");
                    var key = dictionary?.Elements("key").FirstOrDefault(element =>
                        string.Equals(element.Value, "BUNDLE_ID", StringComparison.Ordinal));
                    var value = key?.ElementsAfterSelf().FirstOrDefault();
                    return value?.Name.LocalName == "string" &&
                           string.Equals(value.Value, bundleId, StringComparison.Ordinal);
                }
            }
            catch (XmlException)
            {
                return false;
            }
        }

        [Serializable] private sealed class AndroidConfiguration { public AndroidClient[] client = null; }
        [Serializable] private sealed class AndroidClient { public AndroidClientInfo client_info = null; }
        [Serializable] private sealed class AndroidClientInfo { public AndroidClientIdentity android_client_info = null; }
        [Serializable] private sealed class AndroidClientIdentity { public string package_name = null; }
    }
}
