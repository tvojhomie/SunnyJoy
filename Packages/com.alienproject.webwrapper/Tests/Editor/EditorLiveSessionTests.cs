using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class EditorLiveSessionTests
    {
        private string permissionKey;
        [SetUp] public void SetUp() => permissionKey = "WebWrapper.EditorLive.UnitTest." + Guid.NewGuid().ToString("N");
        [TearDown] public void TearDown() => PlayerPrefs.DeleteKey(permissionKey);

        [TestCase(WebWrapperEditorLiveSession.AttributionMode.NonOrganicQa, "Non-organic")]
        [TestCase(WebWrapperEditorLiveSession.AttributionMode.OrganicQa, "Organic")]
        public void QaPayload_IsExplicitlyTagged_AndNeverFabricatesPushToken(WebWrapperEditorLiveSession.AttributionMode mode, string status)
        {
            var snapshot = WebWrapperEditorLiveSession.CreateAttribution(mode, "unity-editor-test");
            var request = ConfigPayloadBuilder.Build(snapshot, PushRegistration.Empty,
                new AppIdentity("com.example.test", "iOS", "id123", "en-US"));
            Assert.That(request.Values["af_id"], Is.EqualTo("unity-editor-test"));
            Assert.That(request.Values["af_status"], Is.EqualTo(status));
            Assert.That(request.Values["media_source"], Is.EqualTo("unity_editor"));
            Assert.That(request.Values["campaign"], Is.EqualTo("unity_editor_test"));
            Assert.That(request.Values["is_unity_editor"], Is.EqualTo(true));
            Assert.That(request.Values.ContainsKey("push_token"), Is.False);
            Assert.That(request.Values.ContainsKey("firebase_project_id"), Is.False);
        }

        [Test]
        public void NativeUnavailable_HasNoInventedAttribution()
        {
            var snapshot = WebWrapperEditorLiveSession.CreateAttribution(WebWrapperEditorLiveSession.AttributionMode.NoNativeData, "ignored");
            Assert.That(snapshot.AppsFlyerId, Is.Empty);
            Assert.That(snapshot.Payloads[0].Values.ContainsKey("af_status"), Is.False);
            Assert.That(WebWrapperEditorLiveSession.StateKey, Is.Not.EqualTo(PlayerPrefsInstallStateStore.BuildDefaultKey()));
        }

        [TestCase(true, NotificationPermissionStatus.Granted)]
        [TestCase(false, NotificationPermissionStatus.DeniedPermanently)]
        public async Task Permission_WaitsForExplicitChoice_AndPersistsOnlyToItsTestKey(bool allow, NotificationPermissionStatus expected)
        {
            var provider = new WebWrapperEditorLiveSession.EditorPermissionProvider(permissionKey);
            var task = provider.RequestAsync(CancellationToken.None);
            Assert.That(provider.IsPending, Is.True);
            Assert.That(task.IsCompleted, Is.False);
            provider.Complete(allow);
            Assert.That(await task, Is.EqualTo(expected));
            provider.Complete(!allow);
            Assert.That(provider.Status, Is.EqualTo(expected), "Repeated clicks cannot change the completed choice.");
            var repeated = new WebWrapperEditorLiveSession.EditorPermissionProvider(permissionKey);
            Assert.That(await repeated.RequestAsync(CancellationToken.None), Is.EqualTo(expected));
            Assert.That(repeated.IsPending, Is.False);
        }

        [Test]
        public async Task Permission_StopPlayModeCancelsWithoutSavingGrantOrDenial()
        {
            var provider = new WebWrapperEditorLiveSession.EditorPermissionProvider(permissionKey);
            using (var cancellation = new CancellationTokenSource())
            {
                var task = provider.RequestAsync(cancellation.Token);
                cancellation.Cancel();
                try { await task; Assert.Fail("Canceled permission must not complete successfully."); }
                catch (OperationCanceledException) { }
                provider.Complete(true);
                Assert.That(provider.Status, Is.EqualTo(NotificationPermissionStatus.Unknown));
            }
        }

        [Test]
        public void TraceUrl_ExcludesQueryTokensAndFragment() => Assert.That(
            WebWrapperEditorLiveSession.DescribeUrl("https://example.com/offer?token=secret#private"),
            Is.EqualTo("https://example.com/offer"));

        [TestCase(1080, 2400)]
        [TestCase(2400, 1080)]
        public void WebViewPreview_FitsDesktop_WithoutChangingAspect(int width, int height)
        {
            var result = WebWrapperEditorLiveSession.FitWebViewPreview(new Rect(0, 0, width, height), new Vector2(1818, 950));
            Assert.That(result.width, Is.LessThanOrEqualTo(520));
            Assert.That(result.height, Is.LessThanOrEqualTo(820));
            Assert.That(result.width / result.height, Is.EqualTo((float)width / height).Within(.01));
        }
    }
}
