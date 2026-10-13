using System;

namespace EggCatcher
{
    [Serializable]
    public sealed class EggCatcherData
    {
        public const int AvatarCount = 8;
        public const int MaximumLevel = 999;

        public string Name = string.Empty;
        public int AvatarIndex;
        public bool HasProfile;
        public int Coins;
        public int Level = 1;
        public string JoinedUtcIso = string.Empty;

        public bool SoundEnabled = true;
        public bool MusicEnabled = true;
        public bool HapticsEnabled;
        public int LevelXp;
        public int NextLevelXp = 500;

        public int BestScore;
        public long TotalScore;
        public int GamesPlayed;
        public long TotalEggsCaught;
        public long TotalEggsSpent;
        public double TotalPlaySeconds;
        public int BestCombo;

        public string CurrentSkinId = "classic";
        public string BackgroundId = "day";
        public string[] PurchasedSkins = Array.Empty<string>();
        public string[] PurchasedBackgrounds = Array.Empty<string>();
        public string CustomAvatarFileName = string.Empty;
    }
}
