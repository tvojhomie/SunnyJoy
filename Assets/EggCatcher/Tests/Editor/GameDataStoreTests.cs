using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace EggCatcher.Tests
{
    public sealed class GameDataStoreTests
    {
        private string _slot;
        private string _otherSlot;

        [SetUp]
        public void SetUp()
        {
            _slot = "test-" + Guid.NewGuid().ToString("N");
            _otherSlot = "test-" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            GameDataStore.Reset(_slot);
            GameDataStore.Reset(_otherSlot);
        }

        [Test]
        public void FreshProfile_HasRealEmptyProgressAndEnabledSettings()
        {
            var data = GameDataStore.Load(_slot);
            Assert.That(data.HasProfile, Is.False);
            Assert.That(data.Name, Is.Empty);
            Assert.That(data.JoinedUtcIso, Is.Empty);
            Assert.That(data.Level, Is.EqualTo(1));
            Assert.That(data.Coins, Is.Zero);
            Assert.That(data.GamesPlayed, Is.Zero);
            Assert.That(data.BestScore, Is.Zero);
            Assert.That(data.TotalScore, Is.Zero);
            Assert.That(data.TotalEggsCaught, Is.Zero);
            Assert.That(data.TotalEggsSpent, Is.Zero);
            Assert.That(data.TotalPlaySeconds, Is.Zero);
            Assert.That(data.BestCombo, Is.Zero);
            Assert.That(data.SoundEnabled && data.MusicEnabled, Is.True);
            Assert.That(data.HapticsEnabled, Is.False);
            Assert.That(data.CurrentSkinId, Is.EqualTo("classic"));
            Assert.That(data.BackgroundId, Is.EqualTo("day"));
        }

        [TestCase(" A ", false, "A")]
        [TestCase(" Al ", true, "Al")]
        [TestCase("ABCDEFGHIJKL", true, "ABCDEFGHIJKL")]
        [TestCase("ABCDEFGHIJKLM", false, "ABCDEFGHIJKLM")]
        [TestCase("  Лёва  ", true, "Лёва")]
        [TestCase("明月", true, "明月")]
        [TestCase(null, false, "")]
        public void NameValidation_TrimsAndChecksTextElementLength(string input, bool valid, string expected)
        {
            Assert.That(GameDataStore.TryValidateName(input, out var normalized, out var error), Is.EqualTo(valid));
            Assert.That(normalized, Is.EqualTo(expected));
            Assert.That(string.IsNullOrEmpty(error), Is.EqualTo(valid));
        }

        [Test]
        public void NameValidation_CombiningMarksAndSurrogatePairsDoNotConsumeExtraCharacters()
        {
            var twelveAccentedLetters = string.Empty;
            var twelveEmoji = string.Empty;
            for (var index = 0; index < 12; index++)
            {
                twelveAccentedLetters += "e\u0301";
                twelveEmoji += "\U0001F423";
            }

            Assert.That(GameDataStore.TryValidateName(twelveAccentedLetters, out _, out _), Is.True);
            Assert.That(GameDataStore.TryValidateName(twelveEmoji, out _, out _), Is.True);
            Assert.That(GameDataStore.TryValidateName("e\u0301", out _, out _), Is.False);
            Assert.That(GameDataStore.TryValidateName(twelveEmoji + "x", out _, out _), Is.False);
        }

        [TestCase("Al\nex")]
        [TestCase("\tAlex")]
        [TestCase("Al\0ex")]
        [TestCase("Al\u2028ex")]
        [TestCase("Al\u2029ex")]
        public void NameValidation_RejectsControlsLineBreaksAndUnpairedSurrogates(string input)
        {
            Assert.That(GameDataStore.TryValidateName(input, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void NameValidation_RejectsUnpairedSurrogate()
        {
            Assert.That(GameDataStore.TryValidateName("Al" + (char)0xD83D, out _, out _), Is.False);
        }

        [TestCase("broken json")]
        [TestCase("{\"Name\":")]
        [TestCase("[]")]
        [TestCase("null")]
        public void CorruptProfile_FallsBackToFreshDefaults(string json)
        {
            var key = GameDataStore.GetStorageKey(_slot);
            PlayerPrefs.SetString(key, json);
            var data = GameDataStore.Load(_slot);
            Assert.That(data.HasProfile, Is.False);
            Assert.That(data.Coins, Is.Zero);
            Assert.That(data.Level, Is.EqualTo(1));
            Assert.That(data.SoundEnabled, Is.True);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(json), "Reading should not overwrite potentially recoverable data.");
        }

        [Test]
        public void OlderPartialSave_PreservesDefaultsForMissingSettings()
        {
            var data = GameDataStore.Deserialize("{\"Name\":\"Alex\",\"HasProfile\":true,\"Coins\":15}");
            Assert.That(data.HasProfile, Is.True);
            Assert.That(data.Coins, Is.EqualTo(15));
            Assert.That(data.Level, Is.EqualTo(1));
            Assert.That(data.SoundEnabled && data.MusicEnabled, Is.True);
            Assert.That(data.HapticsEnabled, Is.False);
            Assert.That(data.CurrentSkinId, Is.EqualTo("classic"));
        }

        [Test]
        public void Normalize_RepairsImpossibleCountersAndAvatarWithoutInventingProgress()
        {
            var data = new EggCatcherData
            {
                Name = "Alex", HasProfile = true, Coins = -1, Level = -9, AvatarIndex = 100,
                BestScore = -2, TotalScore = -3, GamesPlayed = -4, TotalEggsCaught = -5,
                TotalEggsSpent = -6, TotalPlaySeconds = double.NaN, BestCombo = -7,
                CurrentSkinId = "../skin", BackgroundId = null, CustomAvatarFileName = "../../private/avatar.png"
            };
            GameDataStore.Normalize(data);
            Assert.That(data.HasProfile, Is.True);
            Assert.That(data.Level, Is.EqualTo(1));
            Assert.That(data.AvatarIndex, Is.Zero);
            Assert.That(data.Coins, Is.Zero);
            Assert.That(data.BestScore, Is.Zero);
            Assert.That(data.TotalScore, Is.Zero);
            Assert.That(data.GamesPlayed, Is.Zero);
            Assert.That(data.TotalEggsCaught, Is.Zero);
            Assert.That(data.TotalEggsSpent, Is.Zero);
            Assert.That(data.TotalPlaySeconds, Is.Zero);
            Assert.That(data.BestCombo, Is.Zero);
            Assert.That(data.CurrentSkinId, Is.EqualTo("classic"));
            Assert.That(data.BackgroundId, Is.EqualTo("day"));
            Assert.That(data.CustomAvatarFileName, Is.Empty);
            data.Level = int.MaxValue;
            data.AvatarIndex = 7;
            GameDataStore.Normalize(data);
            Assert.That(data.Level, Is.EqualTo(EggCatcherData.MaximumLevel));
            Assert.That(data.AvatarIndex, Is.EqualTo(7));
        }

        [Test]
        public void InvalidProfileName_ReopensOnboardingWithoutDeletingEarnedStats()
        {
            var data = GameDataStore.Deserialize("{\"Name\":\"A\",\"HasProfile\":true,\"Coins\":50,\"GamesPlayed\":3}");
            Assert.That(data.HasProfile, Is.False);
            Assert.That(data.Name, Is.Empty);
            Assert.That(data.Coins, Is.EqualTo(50));
            Assert.That(data.GamesPlayed, Is.EqualTo(3));
        }

        [Test]
        public void SaveLoad_PersistsRealProgressSettingsAndStableJoinDate()
        {
            var data = new EggCatcherData
            {
                Name = "  Alex  ", HasProfile = true, AvatarIndex = 3, Coins = 23, Level = 2,
                SoundEnabled = false, MusicEnabled = false, HapticsEnabled = false,
                BestScore = 12, TotalScore = 24, GamesPlayed = 2, TotalEggsCaught = 18,
                TotalEggsSpent = 4, TotalPlaySeconds = 65.25, BestCombo = 7,
                CurrentSkinId = "pirate", BackgroundId = "sunset", CustomAvatarFileName = "avatar.png"
            };
            GameDataStore.Save(data, _slot);
            var joined = data.JoinedUtcIso;
            Assert.That(DateTimeOffset.TryParse(joined, out _), Is.True);
            var loaded = GameDataStore.Load(_slot);
            Assert.That(loaded.Name, Is.EqualTo("Alex"));
            Assert.That(loaded.HasProfile, Is.True);
            Assert.That(loaded.AvatarIndex, Is.EqualTo(3));
            Assert.That(loaded.Coins, Is.EqualTo(23));
            Assert.That(loaded.Level, Is.EqualTo(2));
            Assert.That(loaded.SoundEnabled || loaded.MusicEnabled || loaded.HapticsEnabled, Is.False);
            Assert.That(loaded.BestScore, Is.EqualTo(12));
            Assert.That(loaded.TotalScore, Is.EqualTo(24));
            Assert.That(loaded.GamesPlayed, Is.EqualTo(2));
            Assert.That(loaded.TotalEggsCaught, Is.EqualTo(18));
            Assert.That(loaded.TotalEggsSpent, Is.EqualTo(4));
            Assert.That(loaded.TotalPlaySeconds, Is.EqualTo(65.25));
            Assert.That(loaded.BestCombo, Is.EqualTo(7));
            Assert.That(loaded.CurrentSkinId, Is.EqualTo("pirate"));
            Assert.That(loaded.BackgroundId, Is.EqualTo("sunset"));
            Assert.That(loaded.CustomAvatarFileName, Is.EqualTo("avatar.png"));
            GameDataStore.Save(loaded, _slot);
            Assert.That(GameDataStore.Load(_slot).JoinedUtcIso, Is.EqualTo(joined));
        }

        [Test]
        public void Reset_OnlyRemovesSelectedEggCatcherProfileKey()
        {
            GameDataStore.Save(new EggCatcherData { Name = "Alex", HasProfile = true, Coins = 10 }, _slot);
            GameDataStore.Save(new EggCatcherData { Name = "Lena", HasProfile = true, Coins = 20 }, _otherSlot);
            var reset = GameDataStore.Reset(_slot);
            Assert.That(reset.HasProfile, Is.False);
            Assert.That(PlayerPrefs.HasKey(GameDataStore.GetStorageKey(_slot)), Is.False);
            Assert.That(GameDataStore.Load(_otherSlot).Coins, Is.EqualTo(20));
            Assert.That(GameDataStore.GetStorageKey(_slot), Does.Contain(Application.identifier));
            Assert.That(GameDataStore.GetStorageKey(_slot), Is.Not.EqualTo(GameDataStore.StorageKey));
            Assert.Throws<ArgumentException>(() => GameDataStore.Reset("../another-feature"));
        }

        [Test]
        public void DisplayFormatting_DoesNotDependOnDeviceLocale()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                Assert.That(GameDataStore.FormatNumber(12345), Is.EqualTo("12,345"));
                Assert.That(GameDataStore.FormatJoinedDate("2026-10-12T21:30:00+03:00"), Is.EqualTo("Oct 12, 2026"));
                Assert.That(GameDataStore.FormatJoinedDate("invalid"), Is.EqualTo("—"));
                Assert.That(GameDataStore.FormatJoinedDate("12/31"), Is.EqualTo("—"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
