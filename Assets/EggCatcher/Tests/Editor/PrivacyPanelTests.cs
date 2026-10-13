using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienProject.WebWrapper;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EggCatcher.Tests
{
    public sealed class PrivacyPanelTests
    {
        private const string Slot = "PrivacyUiTest";
        private static List<FakeView> views;
        private static IPrivacyPolicyView CreateFake()
        {
            var view = new FakeView();
            views.Add(view);
            return view;
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("http://example.com/privacy", false)]
        [TestCase("file:///privacy.html", false)]
        [TestCase("https://user:password@example.com/privacy", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase(" https://example.com/privacy ", true)]
        public void PolicyUrl_AcceptsOnlyHttpsWithoutCredentials(string url, bool expected)
            => Assert.That(EggCatcherPrivacyPanel.TryGetPolicyUrl(url, out _), Is.EqualTo(expected));

        [UnityTest]
        public IEnumerator SettingsPolicy_UsesContentAndConfiguredUrl_RetryBackAndReopenDisposeNativeView()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            GameDataStore.Save(new EggCatcherData { Name = "Privacy", HasProfile = true }, Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!app.transform.Find("MainMenu").gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(app.transform.Find("MainMenu").gameObject.activeInHierarchy, Is.True);
            yield return null;
            var panel = app.transform.Find("Privacy PolicyPanel").GetComponent<EggCatcherPrivacyPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.activeSelf, Is.False);

            views = new List<FakeView>();
            panel.ViewFactory = CreateFake;
            Click(app, "MainMenu", "Settings");
            Click(app, "SettingPanel", "Politic");
            yield return null;
            yield return null;
            Assert.That(views.Count, Is.EqualTo(1));
            var first = views[0];
            Assert.That(first.Content, Is.SameAs(app.transform.Find("Privacy PolicyPanel/SafeAreaContent/Content")));
            Assert.That(first.Url, Is.EqualTo(Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName).PrivacyPolicyUrl));
            Assert.That(panel.IsLoading, Is.True);
            first.Complete(200);
            Assert.That(panel.IsPageVisible && first.Visible, Is.True);
            Assert.That(first.Content.Find("PrivacyLoading").gameObject.activeSelf, Is.False);
            yield return null;
            int previousUpdates = first.Updates;
            first.Content.anchoredPosition += Vector2.one * 5;
            yield return null;
            Assert.That(first.Updates, Is.GreaterThan(previousUpdates), "Native frame follows Content's layout.");
            typeof(EggCatcherPrivacyPanel).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { true });
            Assert.That(first.Visible, Is.False);
            typeof(EggCatcherPrivacyPanel).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { false });
            Assert.That(first.Visible, Is.True);
            Click(app, "Privacy PolicyPanel", "TopPanel/Back");
            Assert.That(first.Disposed && !first.Visible, Is.True);
            Assert.That(app.transform.Find("SettingPanel").gameObject.activeInHierarchy, Is.True);
            first.Complete(200);
            Assert.That(panel.IsPageVisible, Is.False);

            Click(app, "SettingPanel", "Politic");
            yield return null;
            yield return null;
            Assert.That(views.Count, Is.EqualTo(2), "Reopening creates just one new native view.");
            views[1].Complete(503);
            Assert.That(views[1].Disposed, Is.True);
            Assert.That(panel.IsLoading || panel.IsPageVisible, Is.False);
            var retry = panel.Content.Find("PrivacyLoading/Retry").GetComponent<Button>();
            Assert.That(retry.gameObject.activeInHierarchy, Is.True);
            retry.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(views.Count, Is.EqualTo(3));
            views[2].NetworkError();
            Assert.That(views[2].Disposed && retry.gameObject.activeInHierarchy, Is.True);
            retry.onClick.Invoke();
            yield return null;
            yield return null;
            views[3].Complete(200);
            views[3].RequestClose();
            Assert.That(views[3].Disposed, Is.True);
            Assert.That(app.transform.Find("SettingPanel").gameObject.activeInHierarchy, Is.True);
            Click(app, "SettingPanel", "Politic");
            Click(app, "Privacy PolicyPanel", "TopPanel/Back");
            yield return null;
            yield return null;
            Assert.That(views.Count, Is.EqualTo(4), "Closing during layout must cancel the pending native browser creation.");
            Assert.That(Object.FindFirstObjectByType<WebWrapperController>(), Is.Null, "Policy must not start the main wrapper during game-only preview.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            SessionState.EraseString("EggCatcher.PreviewSlot");
            GameDataStore.Reset(Slot);
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
        }

        private static void Click(EggCatcherApp app, string screen, string path)
        {
            var button = app.transform.Find(screen + "/SafeAreaContent/" + path).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
            Assert.That(button.targetGraphic.raycastTarget, Is.True, "The button graphic must accept taps: " + path);
            button.onClick.Invoke();
        }

        private sealed class FakeView : IPrivacyPolicyView
        {
            public event Action<int> Loaded;
            public event Action Failed;
            public event Action CloseRequested;
            public RectTransform Content;
            public string Url;
            public bool Visible, Disposed;
            public int Updates;
            public void Open(RectTransform content, string url) { Content = content; Url = url; }
            public void Show() => Visible = true;
            public void Hide() => Visible = false;
            public void RefreshFrame() => Updates++;
            public void Dispose() { Disposed = true; Visible = false; }
            public void Complete(int status) => Loaded?.Invoke(status);
            public void NetworkError() => Failed?.Invoke();
            public void RequestClose() => CloseRequested?.Invoke();
        }
    }
}
