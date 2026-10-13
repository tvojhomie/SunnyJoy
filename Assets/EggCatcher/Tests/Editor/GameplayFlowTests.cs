using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EggCatcher.Tests
{
    public sealed class GameplayFlowTests
    {
        private const string Slot = "GameplayFlowTest";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator AuthoredGameplay_CollisionPauseResumeRestartResultsAndPersistence()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            GameDataStore.Save(new EggCatcherData { HasProfile = true, Name = "Player", SoundEnabled = false, MusicEnabled = false }, Slot);
            SessionState.SetString("EggCatcher.PreviewSlot", Slot);
            yield return new EnterPlayMode();
            yield return new WaitForSecondsRealtime(.9f);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            var music = app.GetComponents<AudioSource>()[0];
            Assert.That(music.loop, Is.True);
            Assert.That(music.clip, Is.Not.Null);
            var waveform = new float[2048];
            Assert.That(music.clip.GetData(waveform, 0), Is.True);
            float energy = 0;
            foreach (float sample in waveform) { Assert.That(float.IsNaN(sample), Is.False); energy += sample * sample; }
            Assert.That(energy, Is.GreaterThan(.01f), "The procedural soundtrack must contain audible samples.");
            var game = Object.FindFirstObjectByType<EggCatcherGame>(FindObjectsInactive.Include);
            Assert.That(game, Is.Not.Null);
            Call(app, "RequestPlay");
            Assert.That(game.Round.Phase, Is.EqualTo(RoundPhase.Countdown));
            Assert.That(app.GetComponent<Canvas>().enabled, Is.False);
            var viewportSize = new Vector2Int(Screen.width, Screen.height);
            var worldCamera = Camera.main;
            Assert.That(worldCamera.rect, Is.EqualTo(EggCatcherViewport.CalculateCameraRect(viewportSize)));
            Assert.That(worldCamera.aspect, Is.EqualTo(worldCamera.pixelRect.width / worldCamera.pixelRect.height).Within(.01f));
            Canvas.ForceUpdateCanvases();
            var touch = (RectTransform)game.transform.Find("GameCanvas/Playfield Touch");
            var canvasRect = (RectTransform)touch.parent;
            var touchCorners = new Vector3[4];
            touch.GetWorldCorners(touchCorners);
            float touchMin = (canvasRect.InverseTransformPoint(touchCorners[0]).x - canvasRect.rect.xMin) / canvasRect.rect.width;
            float touchMax = (canvasRect.InverseTransformPoint(touchCorners[2]).x - canvasRect.rect.xMin) / canvasRect.rect.width;
            Assert.That(touchMin, Is.EqualTo(worldCamera.rect.xMin).Within(.001f));
            Assert.That(touchMax, Is.EqualTo(worldCamera.rect.xMax).Within(.001f));
            FreezeSpawns(game);
            game.Round.Tick(3);
            Call(game, "SpawnItem", EggItemKind.Egg, new Vector2(game.PlayerPosition.x, game.PlayerPosition.y + .4f));

            float catchDeadline = Time.realtimeSinceStartup + 3;
            while (game.Round.EggsCaught == 0 && Time.realtimeSinceStartup < catchDeadline) yield return null;
            Assert.That(game.Round.EggsCaught, Is.EqualTo(1), "Real falling-item collision awards an egg.");
            Assert.That(game.ActiveItemCount, Is.Zero, "Caught item returned to the pool.");
            var health = game.transform.Find("GameCanvas/HudSafeArea/TopPanel/Panel/Health");
            var hearts = health.GetComponentsInChildren<Image>();
            Assert.That(hearts.Length, Is.EqualTo(3));
            Assert.That(health.GetComponent<HorizontalLayoutGroup>().spacing, Is.GreaterThanOrEqualTo(0));
            Call(game, "HandleCatch", EggItemKind.Rock, game.PlayerPosition);
            Call(game, "UpdateHud");
            foreach (var heart in hearts)
            {
                Assert.That(heart.preserveAspect, Is.True);
                Assert.That(heart.rectTransform.sizeDelta, Is.EqualTo(new Vector2(80, 90)));
            }
            Assert.That(hearts[2].rectTransform.localScale.x, Is.EqualTo(.8f));
            Call(game, "HandleCatch", EggItemKind.Heart, game.PlayerPosition);
            Call(game, "UpdateHud");
            Assert.That(hearts[2].rectTransform.localScale, Is.EqualTo(Vector3.one));
            Call(game, "HandleCatch", EggItemKind.Magnet, game.PlayerPosition);
            game.PauseRound();
            double elapsed = game.Round.Elapsed;
            float magnet = game.Round.MagnetRemaining;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.Round.Elapsed, Is.EqualTo(elapsed));
            Assert.That(game.Round.MagnetRemaining, Is.EqualTo(magnet));
            Click(game, "PausedPanel/SafeAreaContent/Panel/Sound/Toggle");
            Assert.That(GameDataStore.Load(Slot).SoundEnabled, Is.True);
            Assert.That(app.GetComponent<EggCatcherAudio>().SoundsEnabled, Is.True);
            Click(game, "PausedPanel/SafeAreaContent/Panel/Music/Toggle");
            Assert.That(app.GetComponent<EggCatcherAudio>().MusicEnabled, Is.True);
            Click(game, "PausedPanel/SafeAreaContent/Panel/Restart");
            Click(game, "PausedPanel/SafeAreaContent/RestartGame?/SafeAreaContent/Panel/Back");
            Assert.That(game.Round.Score, Is.EqualTo(1));
            Click(game, "PausedPanel/SafeAreaContent/Panel/Resume");
            Assert.That(game.Round.Phase, Is.EqualTo(RoundPhase.Countdown));
            game.Round.Tick(3);
            Assert.That(game.Round.MagnetRemaining, Is.EqualTo(magnet));
            game.PauseRound();
            Click(game, "PausedPanel/SafeAreaContent/Panel/Restart");
            Click(game, "PausedPanel/SafeAreaContent/RestartGame?/SafeAreaContent/Panel/Restart");
            Assert.That(game.Round.Score, Is.Zero);
            Assert.That(game.Round.Lives, Is.EqualTo(3));
            Assert.That(game.ActiveItemCount, Is.Zero);
            Assert.That(GameDataStore.Load(Slot).Coins, Is.Zero);
            FreezeSpawns(game);
            game.Round.Tick(3);
            Call(game, "HandleCatch", EggItemKind.Egg, game.PlayerPosition);
            Call(game, "HandleCatch", EggItemKind.Corn, game.PlayerPosition);
            for (int i = 0; i < 3; i++)
            { game.Round.Tick(1.2f); Call(game, "HandleCatch", EggItemKind.Rock, game.PlayerPosition); }
            Call(game, "EndRound");
            Call(game, "EndRound");
            var saved = GameDataStore.Load(Slot);
            Assert.That(saved.Coins, Is.EqualTo(1));
            Assert.That(saved.GamesPlayed, Is.EqualTo(1));
            Assert.That(saved.BestScore, Is.EqualTo(6));
            Assert.That(game.transform.Find("GameCanvas/GameOver").gameObject.activeSelf, Is.True);
            Assert.That(game.transform.Find("GameCanvas/GameOver/SafeAreaContent/Panel/Score").GetComponent<Text>().text, Is.EqualTo("6"));
            Assert.That(game.transform.Find("GameCanvas/HudSafeArea/TopPanel").gameObject.activeSelf, Is.False);
            Click(game, "GameOver/SafeAreaContent/Panel/Again");
            Assert.That(game.Round.Phase, Is.EqualTo(RoundPhase.Countdown));
            Assert.That(game.transform.Find("GameCanvas/HudSafeArea/TopPanel").gameObject.activeSelf, Is.True);
            game.PauseRound();
            Click(game, "PausedPanel/SafeAreaContent/Panel/MainMenu");
            Click(game, "PausedPanel/SafeAreaContent/QuitMenu?/SafeAreaContent/Panel/Quit");
            Assert.That(game.gameObject.activeSelf, Is.False);
            Assert.That(worldCamera.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
            Assert.That(app.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(GameDataStore.Load(Slot).GamesPlayed, Is.EqualTo(1));
            Call(app, "LoadProfile");
            Assert.That(app.Profile.Coins, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            GameDataStore.Reset(Slot);
            SessionState.EraseString("EggCatcher.PreviewSlot");
        }
        private static void FreezeSpawns(EggCatcherGame game)
        {
            foreach (var field in new[] { "spawnTimer", "hazardTimer", "powerTimer" })
                typeof(EggCatcherGame).GetField(field, Private).SetValue(game, float.MaxValue);
        }
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Click(EggCatcherGame game, string path)
        {
            var button = game.transform.Find("GameCanvas/" + path).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, path);
            button.onClick.Invoke();
        }
    }
}
