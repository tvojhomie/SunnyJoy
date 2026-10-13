using System.Collections.Generic;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class DiagnosticsTests
    {
        [Test]
        public void Url_RemovesCredentialsQueryAndFragment()
        {
            Assert.AreEqual("https://example.com/page?[redacted]#[redacted]",
                WebWrapperDiagnostics.SafeUrl("https://user:pass@example.com/page?token=private#secret"));
        }

        [TestCase(null)]
        [TestCase("javascript:alert(1)")]
        [TestCase("not a URL")]
        public void InvalidUrl_IsNotEchoed(string input)
        {
            Assert.AreEqual("[invalid URL]", WebWrapperDiagnostics.SafeUrl(input));
        }

        [Test]
        public void Redaction_RemovesKnownKeyTokensAndPersonalFields()
        {
            var safe = WebWrapperDiagnostics.Sanitize(
                "key=test-dev-key {\"push_token\":\"private-fcm\",\"af_id\":\"private-id\",\"email\":\"user@example.com\"}", "test-dev-key");
            StringAssert.DoesNotContain("test-dev-key", safe);
            StringAssert.DoesNotContain("private-fcm", safe);
            StringAssert.DoesNotContain("private-id", safe);
            StringAssert.DoesNotContain("user@example.com", safe);
        }

        [Test]
        public void Redaction_RemovesUrlQueryInSdkLog()
        {
            var safe = WebWrapperDiagnostics.Sanitize("Load https://example.com/page?af_id=private&foo=bar");
            StringAssert.Contains("https://example.com/page", safe);
            StringAssert.DoesNotContain("private", safe);
            StringAssert.DoesNotContain("bar", safe);
        }

        [Test]
        public void Request_PreservesRoutingAndHidesAllOtherValues()
        {
            var source = new Dictionary<string, object>
            {
                ["bundle_id"] = "com.example.game", ["os"] = "android",
                ["push_token"] = "secret-fcm", ["af_id"] = "device-id",
                ["custom_field"] = new Dictionary<string, object> { ["password"] = "secret-password" }
            };
            var safe = WebWrapperDiagnostics.DescribeRequest(source);
            StringAssert.Contains("com.example.game", safe);
            StringAssert.Contains("android", safe);
            StringAssert.Contains("custom_field", safe);
            StringAssert.DoesNotContain("secret-fcm", safe);
            StringAssert.DoesNotContain("device-id", safe);
            StringAssert.DoesNotContain("secret-password", safe);
            Assert.AreEqual("secret-fcm", source["push_token"], "Diagnostics must not change the real outgoing request.");
        }

        [Test]
        public void Editor_DoesNotAutomaticallyCollectLogs()
        {
            Assert.IsFalse(WebWrapperDiagnostics.Enabled);
            WebWrapperDiagnostics.Clear();
            WebWrapperDiagnostics.Record("Test", "not collected");
            Assert.IsEmpty(WebWrapperDiagnostics.Snapshot());
        }
    }
}
