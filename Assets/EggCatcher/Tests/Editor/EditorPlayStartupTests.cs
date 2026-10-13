using System.Reflection;
using AlienProject.WebWrapper;
using NUnit.Framework;
using UnityEngine;

namespace EggCatcher.Tests
{
    public sealed class EditorPlayStartupTests
    {
        [TestCase(true, false, false, true, false, true)]
        [TestCase(false, false, false, true, false, false)]
        [TestCase(true, true, false, true, false, false)]
        [TestCase(true, false, true, true, false, false)]
        [TestCase(true, false, false, false, false, false)]
        [TestCase(true, false, false, true, true, false)]
        public void OrdinaryPlay_EnablesQaOnlyForHostProductionStartup(bool isHostScene, bool previewOrTests,
            bool preparedSession, bool autoStart, bool mock, bool expected)
        {
            var settings = ScriptableObject.CreateInstance<WebWrapperSettings>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(WebWrapperSettings).GetField("autoStart", flags).SetValue(settings, autoStart);
                typeof(WebWrapperSettings).GetField("useMockServices", flags).SetValue(settings, mock);
                Assert.That(Editor.EggCatcherUiSetup.ShouldPrepareServerTest(settings, isHostScene,
                    previewOrTests, preparedSession), Is.EqualTo(expected));
                Assert.That(settings.AutoStart, Is.EqualTo(autoStart));
                Assert.That(settings.UseMockServices, Is.EqualTo(mock));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void MissingSettings_DoesNotPrepareNetworkSession() => Assert.That(
            Editor.EggCatcherUiSetup.ShouldPrepareServerTest(null, true, false, false), Is.False);
    }
}
