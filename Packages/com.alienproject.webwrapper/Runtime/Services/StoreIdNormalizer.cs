using System;

namespace AlienProject.WebWrapper
{
    public static class StoreIdNormalizer
    {
        public static string NormalizeIos(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || trimmed.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return "id" + trimmed;
        }

        public static bool IsValidIos(string value)
        {
            var normalized = NormalizeIos(value);
            if (normalized.Length <= 2 || !normalized.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (var index = 2; index < normalized.Length; index++)
            {
                if (!char.IsDigit(normalized[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
