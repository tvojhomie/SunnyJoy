using System;
using System.Globalization;
using UnityEngine;

namespace EggCatcher
{
    public static class GameDataStore
    {
        public const int MinimumNameLength = 2;
        public const int MaximumNameLength = 12;
        private const string KeyPrefix = "EggCatcher.Profile.v1.";

        public static string StorageKey => KeyPrefix + Application.identifier;

        public static EggCatcherData Load(string profileSlot = null)
        {
            var key = GetStorageKey(profileSlot);
            try
            {
                return Deserialize(PlayerPrefs.GetString(key, string.Empty));
            }
            catch (Exception)
            {
                Debug.LogWarning("[EggCatcher] Saved profile could not be read; using a new local profile.");
                return new EggCatcherData();
            }
        }

        public static void Save(EggCatcherData data, string profileSlot = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Normalize(data);
            if (data.HasProfile && string.IsNullOrEmpty(data.JoinedUtcIso))
            {

                data.JoinedUtcIso = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            }

            PlayerPrefs.SetString(GetStorageKey(profileSlot), Serialize(data));
            PlayerPrefs.Save();
        }

        public static EggCatcherData Reset(string profileSlot = null)
        {
            PlayerPrefs.DeleteKey(GetStorageKey(profileSlot));
            PlayerPrefs.Save();
            return new EggCatcherData();
        }

        public static string GetStorageKey(string profileSlot = null)
        {
            if (string.IsNullOrEmpty(profileSlot))
            {
                return StorageKey;
            }

            if (profileSlot != NormalizeIdentifier(profileSlot, string.Empty))
            {
                throw new ArgumentException("Profile slot must contain only letters, numbers, hyphens or underscores.",
                    nameof(profileSlot));
            }

            return StorageKey + ".Slot." + profileSlot;
        }

        public static string Serialize(EggCatcherData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(Normalize(data));
        }

        public static EggCatcherData Deserialize(string json)
        {
            var data = new EggCatcherData();
            if (string.IsNullOrWhiteSpace(json))
            {
                return data;
            }

            var trimmed = json.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
                !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return data;
            }

            try
            {

                JsonUtility.FromJsonOverwrite(trimmed, data);
                return Normalize(data);
            }
            catch (Exception)
            {

                return new EggCatcherData();
            }
        }

        public static bool TryValidateName(string value, out string normalized, out string error)
        {
            normalized = value?.Trim() ?? string.Empty;
            error = string.Empty;
            if (ContainsInvalidNameCharacter(value))
            {
                error = "Name cannot contain control characters or line breaks.";
                return false;
            }

            var textElements = StringInfo.ParseCombiningCharacters(normalized).Length;
            if (textElements < MinimumNameLength || textElements > MaximumNameLength)
            {
                error = "Name must be 2–12 characters.";
                return false;
            }

            return true;
        }

        public static EggCatcherData Normalize(EggCatcherData data)
        {
            if (data == null)
            {
                return new EggCatcherData();
            }

            if (TryValidateName(data.Name, out var name, out _))
            {
                data.Name = name;
            }
            else
            {
                data.Name = string.Empty;
                data.HasProfile = false;
            }

            if (data.AvatarIndex < 0 || data.AvatarIndex >= EggCatcherData.AvatarCount)
            {
                data.AvatarIndex = 0;
            }

            data.Level = Math.Max(1, Math.Min(EggCatcherData.MaximumLevel, data.Level));
            data.NextLevelXp = Math.Max(1, data.NextLevelXp);
            data.LevelXp = Math.Max(0, Math.Min(data.NextLevelXp, data.LevelXp));
            data.Coins = Math.Max(0, data.Coins);
            data.BestScore = Math.Max(0, data.BestScore);
            data.TotalScore = Math.Max(0L, data.TotalScore);
            data.GamesPlayed = Math.Max(0, data.GamesPlayed);
            data.TotalEggsCaught = Math.Max(0L, data.TotalEggsCaught);
            data.TotalEggsSpent = Math.Max(0L, data.TotalEggsSpent);
            data.BestCombo = Math.Max(0, data.BestCombo);
            if (double.IsNaN(data.TotalPlaySeconds) || double.IsInfinity(data.TotalPlaySeconds) ||
                data.TotalPlaySeconds < 0)
            {
                data.TotalPlaySeconds = 0;
            }

            data.JoinedUtcIso = NormalizeUtcDate(data.JoinedUtcIso);
            data.CurrentSkinId = NormalizeIdentifier(data.CurrentSkinId, "classic");
            data.BackgroundId = NormalizeIdentifier(data.BackgroundId, "day");
            data.CustomAvatarFileName = NormalizeFileName(data.CustomAvatarFileName);
            ShopCatalog.Normalize(data);
            return data;
        }

        public static string FormatNumber(long value) =>
            Math.Max(0L, value).ToString("N0", CultureInfo.InvariantCulture);

        public static string FormatJoinedDate(string utcIso)
        {
            return TryReadUtcDate(utcIso, out var date)
                ? date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : "—";
        }

        private static bool ContainsInvalidNameCharacter(string value)
        {
            if (value == null)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var category = char.GetUnicodeCategory(character);
                if (char.IsControl(character) || category == UnicodeCategory.LineSeparator ||
                    category == UnicodeCategory.ParagraphSeparator)
                {
                    return true;
                }

                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return true;
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeUtcDate(string value) =>
            TryReadUtcDate(value, out var date)
                ? date.ToString("O", CultureInfo.InvariantCulture)
                : string.Empty;

        private static bool TryReadUtcDate(string value, out DateTimeOffset date)
        {
            date = default;

            if (string.IsNullOrEmpty(value) || value.Length < 20 || value[4] != '-' ||
                value[7] != '-' || value[10] != 'T' || value[13] != ':' || value[16] != ':')
            {
                return false;
            }

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        private static string NormalizeIdentifier(string value, string fallback)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.Length > 64)
            {
                return fallback;
            }

            foreach (var character in normalized)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return fallback;
                }
            }

            return normalized;
        }

        private static string NormalizeFileName(string value)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.Length > 128 ||
                normalized == "." || normalized == "..")
            {
                return string.Empty;
            }

            foreach (var character in normalized)
            {
                if (char.IsControl(character) || character == '/' || character == '\\' || character == ':')
                {
                    return string.Empty;
                }
            }

            return normalized;
        }
    }
}
