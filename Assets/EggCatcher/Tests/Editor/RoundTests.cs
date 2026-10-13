using NUnit.Framework;

namespace EggCatcher.Tests
{
    public sealed class RoundTests
    {
        private static EggCatcherRound Running()
        {
            var round = new EggCatcherRound(new EggCatcherTuning());
            round.Tick(3);
            return round;
        }
        [TestCase(EggItemKind.Egg, 1, 1)]
        [TestCase(EggItemKind.Corn, 5, 0)]
        [TestCase(EggItemKind.Worm, 10, 0)]
        public void Food_HasReferenceValues(EggItemKind kind, int score, int eggs)
        {
            var round = Running();
            Assert.That(round.Catch(kind).Points, Is.EqualTo(score));
            Assert.That(round.Score, Is.EqualTo(score));
            Assert.That(round.EggsCaught, Is.EqualTo(eggs));
        }
        [Test]
        public void Countdown_RejectsCatchesAndDoesNotCountRoundTime()
        {
            var round = new EggCatcherRound(new EggCatcherTuning());
            Assert.That(round.Catch(EggItemKind.Egg).Accepted, Is.False);
            round.Tick(2.9f);
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Countdown));
            round.Tick(.2f);
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Running));
            Assert.That(round.Elapsed, Is.Zero);
        }
        [TestCase(EggItemKind.Rock)]
        [TestCase(EggItemKind.RottenApple)]
        [TestCase(EggItemKind.Bomb)]
        public void Hazards_RemoveOneLifeWithGracePeriod(EggItemKind kind)
        {
            var round = Running();
            Assert.That(round.Catch(kind).LifeChange, Is.EqualTo(-1));
            Assert.That(round.Catch(kind).Accepted, Is.False);
            Assert.That(round.Lives, Is.EqualTo(2));
            round.Tick(1.2f);
            round.Catch(kind);
            Assert.That(round.Lives, Is.EqualTo(1));
        }
        [Test]
        public void Heart_HealsButNeverExceedsThree()
        {
            var round = Running();
            Assert.That(round.Catch(EggItemKind.Heart).LifeChange, Is.Zero);
            round.Catch(EggItemKind.Rock);
            Assert.That(round.Lives, Is.EqualTo(3));
            round.Tick(1.2f);
            round.Catch(EggItemKind.Rock);
            Assert.That(round.Catch(EggItemKind.Heart).LifeChange, Is.EqualTo(1));
            Assert.That(round.Lives, Is.EqualTo(3));
        }
        [Test]
        public void Pausing_FreezesAllClocksAndRejectsCatches()
        {
            var round = Running();
            round.Catch(EggItemKind.Magnet);
            round.Catch(EggItemKind.Rock);
            round.Tick(.5f);
            double elapsed = round.Elapsed;
            float magnet = round.MagnetRemaining, grace = round.InvulnerableRemaining;
            round.Pause(); round.Tick(100);
            Assert.That(round.Elapsed, Is.EqualTo(elapsed));
            Assert.That(round.MagnetRemaining, Is.EqualTo(magnet));
            Assert.That(round.InvulnerableRemaining, Is.EqualTo(grace));
            Assert.That(round.Catch(EggItemKind.Egg).Accepted, Is.False);
            round.Resume(); round.Tick(3);
            Assert.That(round.MagnetRemaining, Is.EqualTo(magnet), "Resume countdown must not consume power-ups.");
            round.Tick(10);
            Assert.That(round.MagnetRemaining, Is.Zero);
        }
        [Test]
        public void Streak_BuildsMultiplierAndResetsOnFoodMissOrDamage()
        {
            var round = Running();
            for (int i = 0; i < 40; i++) round.Catch(EggItemKind.Egg);
            Assert.That(round.Multiplier, Is.EqualTo(5));
            Assert.That(round.BestStreak, Is.EqualTo(40));
            Assert.That(round.Catch(EggItemKind.Worm).Points, Is.EqualTo(100));
            round.Miss(EggItemKind.Rock);
            Assert.That(round.Multiplier, Is.EqualTo(5));
            round.Catch(EggItemKind.Bomb);
            Assert.That(round.Multiplier, Is.EqualTo(1));
            round.Catch(EggItemKind.Egg); round.Miss(EggItemKind.Egg);
            Assert.That(round.Streak, Is.Zero);
            Assert.That(round.BestMultiplier, Is.EqualTo(5));
        }
        [Test]
        public void GameOver_CommitsExactlyOnceAndUpdatesAllStatistics()
        {
            var round = Running();
            for (int i = 0; i < 12; i++) round.Catch(EggItemKind.Egg);
            for (int i = 0; i < 3; i++) { round.Tick(2); round.Catch(EggItemKind.Rock); }
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.GameOver));
            Assert.That(round.Catch(EggItemKind.Heart).Accepted, Is.False);
            var data = new EggCatcherData { Coins = 5, LevelXp = 499 };
            Assert.That(round.Commit(data), Is.True);
            Assert.That(round.Commit(data), Is.False);
            Assert.That(data.Coins, Is.EqualTo(17));
            Assert.That(data.TotalEggsCaught, Is.EqualTo(12));
            Assert.That(data.GamesPlayed, Is.EqualTo(1));
            Assert.That(data.TotalScore, Is.EqualTo(round.Score));
            Assert.That(data.BestScore, Is.EqualTo(round.Score));
            Assert.That(data.TotalPlaySeconds, Is.EqualTo(6));
            Assert.That(data.BestCombo, Is.EqualTo(12));
            Assert.That(data.Level, Is.EqualTo(2));
            Assert.That(data.LevelXp, Is.EqualTo(round.Score - 1));
            Assert.That(data.NextLevelXp, Is.EqualTo(550));
        }
        [Test]
        public void RestartOrQuit_DoesNotAwardPartialProgress()
        {
            var round = Running();
            round.Catch(EggItemKind.Egg);
            var data = new EggCatcherData();
            Assert.That(round.Commit(data), Is.False);
            round.Abandon();
            Assert.That(round.Commit(data), Is.False);
            Assert.That(round.Resume(), Is.False);
            Assert.That(data.Coins, Is.Zero);
            Assert.That(data.GamesPlayed, Is.Zero);
        }
        [Test]
        public void InvalidDelta_DoesNotCorruptRound()
        {
            var round = Running();
            round.Tick(float.NaN); round.Tick(float.PositiveInfinity); round.Tick(-1);
            Assert.That(round.Elapsed, Is.Zero);
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.Running));
        }
        [Test]
        public void FullSaveCounters_DoNotOverflow()
        {
            var round = Running(); round.Catch(EggItemKind.Egg);
            for (int i = 0; i < 3; i++) { round.Tick(2); round.Catch(EggItemKind.Bomb); }
            var data = new EggCatcherData { Coins = int.MaxValue, GamesPlayed = int.MaxValue, TotalScore = long.MaxValue,
                TotalEggsCaught = long.MaxValue, Level = 999, LevelXp = 100 };
            round.Commit(data);
            Assert.That(data.Coins, Is.EqualTo(int.MaxValue));
            Assert.That(data.TotalScore, Is.EqualTo(long.MaxValue));
            Assert.That(data.Level, Is.EqualTo(999));
            Assert.That(data.LevelXp, Is.Zero);
        }
    }
}
