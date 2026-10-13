using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EggCatcher.Tests
{
    public sealed class AudioFeedbackTests
    {
        private const string Slot = "AudioFeedbackTest";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static Array AllSounds => Enum.GetValues(typeof(EggSound));

        [TestCaseSource(nameof(AllSounds))]
        public void EveryEffect_HasFiniteAudibleSamplesWithoutClipping(EggSound sound)
        {
            var clip = (AudioClip)typeof(EggCatcherAudio).GetMethod("MakeEffect", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { sound });
            try
            {
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True);
                double energy = 0;
                foreach (var sample in samples)
                {
                    Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
                    Assert.That(Mathf.Abs(sample), Is.LessThanOrEqualTo(.71f));
                    energy += sample * sample;
                }
                Assert.That(Math.Sqrt(energy / samples.Length), Is.GreaterThan(.02));
                Assert.That(clip.length, Is.InRange(.1f, 2f));
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [UnityTest]
        public IEnumerator AuthoredUiAndGame_DispatchFeedbackAndCancelPendingResults()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            GameDataStore.Save(new EggCatcherData { Name = "Audio test", HasProfile = true, Coins = 500,
                MusicEnabled = false, SoundEnabled = true }, Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            yield return new WaitForSecondsRealtime(.9f);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            var audio = app.GetComponent<EggCatcherAudio>();
            var played = new List<EggSound>();
            audio.SoundPlayed += played.Add;
            Click(app, "MainMenu/Skins");
            CollectionAssert.AreEqual(new[] { EggSound.Click }, played);
            yield return null;
            played.Clear();
            Click(app, "SkinsPanel/CharactersContent/Pirate/Btn");
            CollectionAssert.AreEqual(new[] { EggSound.SkinSelect }, played, "Semantic sound replaces the UI click.");
            played.Clear();
            Click(app, "SkinsPanel/BackgroundsTab");
            Click(app, "SkinsPanel/BGContent/Sunset/Btn");
            CollectionAssert.AreEqual(new[] { EggSound.BackgroundSelect }, played);
            Click(app, "SkinsPanel/CharactersTab");
            Click(app, "SkinsPanel/CharactersContent/Ninja/Buy");
            played.Clear();
            Click(app, "SkinsPanel/BuyPanel/Panel/Reset");
            CollectionAssert.AreEqual(new[] { EggSound.Purchase }, played);
            Click(app, "SkinsPanel/BackgroundsTab");
            Click(app, "SkinsPanel/BGContent/Winter/Buy");
            played.Clear();
            Click(app, "SkinsPanel/BuyPanel/Panel/Reset");
            CollectionAssert.AreEqual(new[] { EggSound.Error }, played);
            Click(app, "SkinsPanel/Not enough eggsPanel");
            Click(app, "SkinsPanel/TopPanel/Back");

            Call(app, "ShowNotice", "Test", "Test message", "Close");
            yield return null;
            played.Clear();
            var modal = (GameObject)typeof(EggCatcherApp).GetField("modal", Private).GetValue(app);
            modal.GetComponentInChildren<Button>().onClick.Invoke();
            CollectionAssert.AreEqual(new[] { EggSound.Click }, played);

            var game = Object.FindFirstObjectByType<EggCatcherGame>(FindObjectsInactive.Include);
            Call(app, "RequestPlay");
            foreach (var field in new[] { "spawnTimer", "hazardTimer", "powerTimer" })
                typeof(EggCatcherGame).GetField(field, Private).SetValue(game, float.MaxValue);
            game.Round.Tick(3);
            played.Clear();
            var foods = new[] { EggItemKind.Egg, EggItemKind.Corn, EggItemKind.Worm, EggItemKind.Heart, EggItemKind.Magnet };
            foreach (var kind in foods) Call(game, "HandleCatch", kind, game.PlayerPosition);
            CollectionAssert.AreEqual(new[] { EggSound.Catch, EggSound.Corn, EggSound.Worm, EggSound.Shield, EggSound.Magnet }, played);
            played.Clear();
            Call(game, "HandleCatch", EggItemKind.Rock, game.PlayerPosition);
            CollectionAssert.AreEqual(new[] { EggSound.ShieldBlock }, played);
            game.Round.Tick(1.2f);
            played.Clear();
            Call(game, "HandleCatch", EggItemKind.Rock, game.PlayerPosition);
            game.Round.Tick(1.2f);
            Call(game, "HandleCatch", EggItemKind.RottenApple, game.PlayerPosition);
            game.Round.Tick(1.2f);
            Call(game, "HandleCatch", EggItemKind.Bomb, game.PlayerPosition);
            Call(game, "EndRound");
            CollectionAssert.AreEqual(new[] { EggSound.Hurt, EggSound.Hurt, EggSound.Bomb }, played);
            yield return new WaitForSecondsRealtime(1.55f);
            CollectionAssert.AreEqual(new[] { EggSound.Hurt, EggSound.Hurt, EggSound.Bomb, EggSound.Lose, EggSound.Record }, played);

            audio.PlayResult(true);
            game.StartRound();
            played.Clear();
            yield return new WaitForSecondsRealtime(1.55f);
            Assert.That(played, Has.No.Member(EggSound.Record));
            Assert.That(played, Has.No.Member(EggSound.Lose));
            game.ReturnToMenu();
            played.Clear();
            audio.PlayResult(false);
            yield return new WaitForSecondsRealtime(.3f);
            CollectionAssert.AreEqual(new[] { EggSound.Lose }, played);
            audio.PlayResult(true);
            audio.ApplySettings(false, false);
            played.Clear();
            audio.Play(EggSound.Catch);
            audio.PlayUi(EggSound.Toggle);
            yield return new WaitForSecondsRealtime(1.55f);
            Assert.That(played, Is.Empty, "Mute stops every sound bus and queued result.");
            foreach (var source in audio.GetComponents<AudioSource>()) Assert.That(source.isPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            GameDataStore.Reset(Slot);
            SessionState.EraseString("EggCatcher.PreviewSlot");
        }

        private static void Click(EggCatcherApp app, string path)
        {
            int slash = path.IndexOf('/');
            var node = (Transform)typeof(EggCatcherApp).GetMethod("Node", Private).Invoke(app,
                new object[] { path.Substring(0, slash), path.Substring(slash + 1) });
            var button = node.GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, path);
            button.onClick.Invoke();
        }
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}
