using System;

namespace EggCatcher
{
    public enum EggItemKind { Egg, Corn, Worm, Heart, Magnet, Rock, RottenApple, Bomb }
    public enum RoundPhase { Countdown, Running, Paused, GameOver, Abandoned }

    [Serializable]
    public sealed class EggCatcherTuning
    {
        public float CountdownSeconds = 3;
        public float MagnetSeconds = 8;
        public float DamageGraceSeconds = 1.1f;
        public float FirstSpawnInterval = .65f;
        public float FastestSpawnInterval = .3f;
        public float DifficultyRampSeconds = 150;
        public float FirstFallSpeed = 1.7f;
        public float FastestFallSpeed = 3.1f;
        public float PlayerSpeed = 7;
        public int CatchesPerMultiplier = 10;
        public int MaximumMultiplier = 5;
        public int FrenzyCatches = 20;
        public float FrenzySeconds = 6;
    }

    public readonly struct CatchResult
    {
        public readonly bool Accepted;
        public readonly int Points, LifeChange;
        public readonly bool ShieldBlocked, ShieldGained, FrenzyStarted;
        public readonly int MissionReward;
        public CatchResult(bool accepted, int points = 0, int lifeChange = 0, bool shieldBlocked = false,
            bool shieldGained = false, bool frenzyStarted = false, int missionReward = 0)
        {
            Accepted = accepted; Points = points; LifeChange = lifeChange;
            ShieldBlocked = shieldBlocked; ShieldGained = shieldGained;
            FrenzyStarted = frenzyStarted; MissionReward = missionReward;
        }
    }

    public sealed class EggCatcherRound
    {
        private readonly EggCatcherTuning tuning;
        private bool committed;
        public RoundPhase Phase { get; private set; } = RoundPhase.Countdown;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; }
        public int EggsCaught { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int BestMultiplier { get; private set; } = 1;
        public int Multiplier => Math.Min(Math.Max(1, tuning.MaximumMultiplier), 1 + Streak / Math.Max(1, tuning.CatchesPerMultiplier));
        public double Elapsed { get; private set; }
        public float CountdownRemaining { get; private set; }
        public float MagnetRemaining { get; private set; }
        public float InvulnerableRemaining { get; private set; }
        public bool HasShield { get; private set; }
        public float FrenzyRemaining { get; private set; }
        public int FrenzyCharge { get; private set; }
        public bool IsFrenzy => FrenzyRemaining > 0;
        public float FrenzyProgress => IsFrenzy ? FrenzyRemaining / Math.Max(.1f, tuning.FrenzySeconds) :
            (float)FrenzyCharge / Math.Max(1, tuning.FrenzyCatches);
        public int CornCaught { get; private set; }
        public int WormsCaught { get; private set; }
        public int MissionBonus { get; private set; }
        public int EarnedEggs => AddInt(EggsCaught, MissionBonus);
        public int MissionsCompleted { get; private set; }
        private int missionMask;
        public string MissionLabel => EggsCaught < 20 ? "Collect eggs: " + EggsCaught + "/20  •  +10 eggs" :
            CornCaught < 5 ? "Collect corn: " + CornCaught + "/5  •  +15 eggs" :
            WormsCaught < 3 ? "Collect worms: " + WormsCaught + "/3  •  +20 eggs" : "All 3 missions complete!  +45 eggs";

        public EggCatcherRound(EggCatcherTuning tuning)
        {
            this.tuning = tuning ?? new EggCatcherTuning();
            CountdownRemaining = Math.Max(.1f, this.tuning.CountdownSeconds);
        }

        public void Tick(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0) return;
            if (Phase == RoundPhase.Countdown)
            {
                CountdownRemaining = Math.Max(0, CountdownRemaining - delta);
                if (CountdownRemaining == 0) Phase = RoundPhase.Running;
                return;
            }
            if (Phase != RoundPhase.Running) return;
            Elapsed += delta;
            MagnetRemaining = Math.Max(0, MagnetRemaining - delta);
            InvulnerableRemaining = Math.Max(0, InvulnerableRemaining - delta);
            FrenzyRemaining = Math.Max(0, FrenzyRemaining - delta);
        }

        public bool Pause()
        {
            if (Phase != RoundPhase.Running && Phase != RoundPhase.Countdown) return false;
            Phase = RoundPhase.Paused;
            return true;
        }

        public bool Resume()
        {
            if (Phase != RoundPhase.Paused) return false;
            CountdownRemaining = Math.Max(.1f, tuning.CountdownSeconds);
            Phase = RoundPhase.Countdown;
            return true;
        }

        public void Abandon()
        {
            if (Phase != RoundPhase.GameOver) Phase = RoundPhase.Abandoned;
        }

        public static bool IsHazard(EggItemKind kind) => kind >= EggItemKind.Rock;
        public static bool IsFood(EggItemKind kind) => kind <= EggItemKind.Worm;

        public CatchResult Catch(EggItemKind kind)
        {
            if (Phase != RoundPhase.Running) return default;
            if (IsHazard(kind))
            {
                if (InvulnerableRemaining > 0) return default;
                if (HasShield)
                {
                    HasShield = false;
                    InvulnerableRemaining = Math.Max(.1f, tuning.DamageGraceSeconds);
                    return new CatchResult(true, shieldBlocked: true);
                }
                Lives--;
                Streak = 0;
                FrenzyCharge = 0;
                FrenzyRemaining = 0;
                InvulnerableRemaining = Math.Max(.1f, tuning.DamageGraceSeconds);
                if (Lives == 0) Phase = RoundPhase.GameOver;
                return new CatchResult(true, lifeChange: -1);
            }
            if (kind == EggItemKind.Heart)
            {
                int gained = Lives < 3 ? 1 : 0;
                Lives += gained;
                bool shieldGained = gained == 0 && !HasShield;
                if (gained == 0) HasShield = true;
                return new CatchResult(true, lifeChange: gained, shieldGained: shieldGained);
            }
            if (kind == EggItemKind.Magnet)
            {
                MagnetRemaining = Math.Max(.1f, tuning.MagnetSeconds);
                return new CatchResult(true);
            }
            int points = (kind == EggItemKind.Egg ? 1 : kind == EggItemKind.Corn ? 5 : 10) * Multiplier * (IsFrenzy ? 2 : 1);
            Score = AddInt(Score, points);
            if (kind == EggItemKind.Egg) EggsCaught = AddInt(EggsCaught, 1);
            if (kind == EggItemKind.Corn) CornCaught = AddInt(CornCaught, 1);
            if (kind == EggItemKind.Worm) WormsCaught = AddInt(WormsCaught, 1);
            Streak = AddInt(Streak, 1);
            BestStreak = Math.Max(BestStreak, Streak);
            BestMultiplier = Math.Max(BestMultiplier, Multiplier);
            bool frenzyStarted = false;
            if (!IsFrenzy && ++FrenzyCharge >= Math.Max(1, tuning.FrenzyCatches))
            {
                FrenzyCharge = 0;
                FrenzyRemaining = Math.Max(.1f, tuning.FrenzySeconds);
                frenzyStarted = true;
            }
            int reward = AwardMission(1, EggsCaught >= 20, 10) + AwardMission(2, CornCaught >= 5, 15) +
                AwardMission(4, WormsCaught >= 3, 20);
            return new CatchResult(true, points, frenzyStarted: frenzyStarted, missionReward: reward);
        }

        private int AwardMission(int flag, bool complete, int reward)
        {
            if (!complete || (missionMask & flag) != 0) return 0;
            missionMask |= flag;
            MissionsCompleted++;
            MissionBonus += reward;
            return reward;
        }

        public void Miss(EggItemKind kind)
        {
            if (Phase == RoundPhase.Running && IsFood(kind)) { Streak = 0; FrenzyCharge = 0; }
        }

        public bool Commit(EggCatcherData profile)
        {
            if (profile == null || Phase != RoundPhase.GameOver || committed) return false;
            committed = true;
            profile.Coins = AddInt(profile.Coins, EarnedEggs);
            profile.BestScore = Math.Max(profile.BestScore, Score);
            profile.TotalScore = AddLong(profile.TotalScore, Score);
            profile.GamesPlayed = AddInt(profile.GamesPlayed, 1);
            profile.TotalEggsCaught = AddLong(profile.TotalEggsCaught, EggsCaught);
            profile.TotalPlaySeconds += Elapsed;
            profile.BestCombo = Math.Max(profile.BestCombo, BestStreak);
            long xp = (long)profile.LevelXp + Score;
            while (profile.Level < EggCatcherData.MaximumLevel && xp >= XpForLevel(profile.Level))
            { xp -= XpForLevel(profile.Level); profile.Level++; }
            profile.NextLevelXp = XpForLevel(profile.Level);
            profile.LevelXp = profile.Level == EggCatcherData.MaximumLevel ? 0 : (int)Math.Max(0, xp);
            return true;
        }

        public static int XpForLevel(int level) => 500 + (Math.Max(1, Math.Min(EggCatcherData.MaximumLevel, level)) - 1) * 50;
        private static int AddInt(int value, int amount) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, value) + amount);
        private static long AddLong(long value, int amount) => Math.Min(long.MaxValue - amount, Math.Max(0, value)) + amount;
    }
}
