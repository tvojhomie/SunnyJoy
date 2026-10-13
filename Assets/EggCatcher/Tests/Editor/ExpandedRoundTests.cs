using NUnit.Framework;

namespace EggCatcher.Tests
{
    public sealed class ExpandedRoundTests
    {
        private static EggCatcherRound Running()
        {
            var round = new EggCatcherRound(new EggCatcherTuning());
            round.Tick(3);
            return round;
        }

        [Test]
        public void Frenzy_NeedsUnbrokenCatches_DoublesPoints_AndExpires()
        {
            var round = Running();
            for (int i = 0; i < 19; i++) round.Catch(EggItemKind.Egg);
            Assert.That(round.IsFrenzy, Is.False);
            round.Miss(EggItemKind.Corn);
            Assert.That(round.FrenzyCharge, Is.Zero);
            for (int i = 0; i < 19; i++) round.Catch(EggItemKind.Egg);
            Assert.That(round.Catch(EggItemKind.Egg).FrenzyStarted, Is.True);
            Assert.That(round.Catch(EggItemKind.Corn).Points, Is.EqualTo(30));
            round.Tick(6);
            Assert.That(round.IsFrenzy, Is.False);
            Assert.That(round.Catch(EggItemKind.Corn).Points, Is.EqualTo(15));
        }

        [Test]
        public void Shield_BlocksOneHit_WithoutBreakingStreak_AndCannotStack()
        {
            var round = Running();
            round.Catch(EggItemKind.Egg);
            Assert.That(round.Catch(EggItemKind.Heart).ShieldGained, Is.True);
            Assert.That(round.Catch(EggItemKind.Heart).ShieldGained, Is.False);
            Assert.That(round.Catch(EggItemKind.Bomb).ShieldBlocked, Is.True);
            Assert.That(round.Lives, Is.EqualTo(3));
            Assert.That(round.Streak, Is.EqualTo(1));
            Assert.That(round.HasShield, Is.False);
            Assert.That(round.Catch(EggItemKind.Rock).Accepted, Is.False);
            round.Tick(1.2f);
            Assert.That(round.Catch(EggItemKind.Rock).LifeChange, Is.EqualTo(-1));
        }

        [Test]
        public void Missions_AwardEachOnce_OnlyCommitAtGameOver()
        {
            var round = Running();
            for (int i = 0; i < 25; i++) round.Catch(EggItemKind.Egg);
            for (int i = 0; i < 7; i++) round.Catch(EggItemKind.Corn);
            for (int i = 0; i < 6; i++) round.Catch(EggItemKind.Worm);
            Assert.That(round.MissionsCompleted, Is.EqualTo(3));
            Assert.That(round.MissionBonus, Is.EqualTo(45));
            Assert.That(round.MissionLabel, Does.StartWith("All 3"));
            var profile = new EggCatcherData();
            Assert.That(round.Commit(profile), Is.False);
            for (int i = 0; i < 3; i++) { round.Tick(2); round.Catch(EggItemKind.Rock); }
            Assert.That(round.Commit(profile), Is.True);
            Assert.That(profile.Coins, Is.EqualTo(70));
            Assert.That(profile.TotalEggsCaught, Is.EqualTo(25));
            Assert.That(round.Commit(profile), Is.False);
        }

        [Test]
        public void Pause_FreezesNewPowerups_AbandonDiscardsMissionRewards()
        {
            var round = Running();
            for (int i = 0; i < 20; i++) round.Catch(EggItemKind.Egg);
            round.Catch(EggItemKind.Heart);
            round.Pause();
            round.Tick(100);
            Assert.That(round.FrenzyRemaining, Is.EqualTo(6));
            Assert.That(round.HasShield, Is.True);
            round.Resume(); round.Tick(3);
            Assert.That(round.FrenzyRemaining, Is.EqualTo(6));
            round.Abandon();
            var profile = new EggCatcherData();
            Assert.That(round.Commit(profile), Is.False);
            Assert.That(profile.Coins, Is.Zero);
            Assert.That(new EggCatcherRound(new EggCatcherTuning()).MissionBonus, Is.Zero);
        }
    }
}
