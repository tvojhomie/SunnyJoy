using System;
using System.Linq;

namespace EggCatcher
{
    public enum ShopCategory { Character, Background }
    public enum PurchaseResult { Purchased, AlreadyOwned, NotEnoughEggs, Locked, UnknownItem }

    public sealed class ShopItem
    {
        public readonly string Id, Name;
        public readonly ShopCategory Category;
        public readonly int Price, RequiredScore, RequiredLevel;
        internal ShopItem(string id, string name, ShopCategory category, int price = 0, int score = 0, int level = 0)
        { Id = id; Name = name; Category = category; Price = price; RequiredScore = score; RequiredLevel = level; }
        public string ScenePath => (Category == ShopCategory.Character ? "CharactersContent/" : "BGContent/") + Name;
    }

    public static class ShopCatalog
    {
        public static readonly System.Collections.Generic.IReadOnlyList<ShopItem> Items = Array.AsReadOnly(new[]
        {
            new ShopItem("classic", "Classic", ShopCategory.Character),
            new ShopItem("chick", "Chicka", ShopCategory.Character),
            new ShopItem("pirate", "Pirate", ShopCategory.Character),
            new ShopItem("ninja", "Ninja", ShopCategory.Character, 500),
            new ShopItem("space", "Astronaut", ShopCategory.Character, 1200),
            new ShopItem("gold", "Golden", ShopCategory.Character, score: 2000),
            new ShopItem("day", "Farm by Day", ShopCategory.Background),
            new ShopItem("sunset", "Sunset", ShopCategory.Background),
            new ShopItem("winter", "Winter", ShopCategory.Background, 800),
            new ShopItem("night", "Night Farm", ShopCategory.Background, level: 12)
        });

        public static ShopItem Find(ShopCategory category, string id) => Items.FirstOrDefault(item => item.Category == category && item.Id == id);
        public static bool IsLocked(EggCatcherData data, ShopItem item) =>
            data == null || item == null || data.BestScore < item.RequiredScore || data.Level < item.RequiredLevel;
        public static bool IsOwned(EggCatcherData data, ShopItem item)
        {
            if (IsLocked(data, item)) return false;
            var purchased = item.Category == ShopCategory.Character ? data.PurchasedSkins : data.PurchasedBackgrounds;
            return item.Price == 0 || (purchased != null && Array.IndexOf(purchased, item.Id) >= 0);
        }

        public static bool TrySelect(EggCatcherData data, ShopCategory category, string id)
        {
            var item = Find(category, id);
            if (!IsOwned(data, item)) return false;
            if (category == ShopCategory.Character) data.CurrentSkinId = id;
            else data.BackgroundId = id;
            return true;
        }

        public static PurchaseResult TryPurchase(EggCatcherData data, ShopCategory category, string id)
        {
            var item = Find(category, id);
            if (item == null || data == null) return PurchaseResult.UnknownItem;
            if (IsLocked(data, item)) return PurchaseResult.Locked;
            if (IsOwned(data, item)) return PurchaseResult.AlreadyOwned;
            if (data.Coins < item.Price) return PurchaseResult.NotEnoughEggs;
            var owned = category == ShopCategory.Character ? data.PurchasedSkins : data.PurchasedBackgrounds;
            var updated = (owned ?? Array.Empty<string>()).Concat(new[] { id }).Distinct().ToArray();
            data.Coins -= item.Price;
            data.TotalEggsSpent = Math.Min(long.MaxValue - item.Price, Math.Max(0, data.TotalEggsSpent)) + item.Price;
            if (category == ShopCategory.Character) data.PurchasedSkins = updated;
            else data.PurchasedBackgrounds = updated;
            TrySelect(data, category, id);
            return PurchaseResult.Purchased;
        }

        public static void Normalize(EggCatcherData data)
        {
            data.PurchasedSkins = CleanPurchases(data.PurchasedSkins, ShopCategory.Character);
            data.PurchasedBackgrounds = CleanPurchases(data.PurchasedBackgrounds, ShopCategory.Background);
            if (!IsOwned(data, Find(ShopCategory.Character, data.CurrentSkinId))) data.CurrentSkinId = "classic";
            if (!IsOwned(data, Find(ShopCategory.Background, data.BackgroundId))) data.BackgroundId = "day";
        }

        private static string[] CleanPurchases(string[] ids, ShopCategory category) =>
            (ids ?? Array.Empty<string>()).Where(id => Find(category, id)?.Price > 0).Distinct().ToArray();
    }
}
