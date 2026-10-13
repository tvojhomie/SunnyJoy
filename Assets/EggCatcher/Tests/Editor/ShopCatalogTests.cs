using NUnit.Framework;

namespace EggCatcher.Tests
{
    public sealed class ShopCatalogTests
    {
        [TestCase(ShopCategory.Character, "ninja", 500)]
        [TestCase(ShopCategory.Character, "space", 1200)]
        [TestCase(ShopCategory.Background, "winter", 800)]
        public void Purchase_ChargesOnceAndSurvivesSerialization(ShopCategory category, string id, int price)
        {
            var data = new EggCatcherData { Name = "Test", HasProfile = true, Coins = price };
            Assert.That(ShopCatalog.TrySelect(data, category, id), Is.False);
            Assert.That(ShopCatalog.TryPurchase(data, category, id), Is.EqualTo(PurchaseResult.Purchased));
            Assert.That(data.Coins, Is.Zero);
            Assert.That(data.TotalEggsSpent, Is.EqualTo(price));
            var loaded = GameDataStore.Deserialize(GameDataStore.Serialize(data));
            Assert.That(ShopCatalog.IsOwned(loaded, ShopCatalog.Find(category, id)), Is.True);
            Assert.That(ShopCatalog.TryPurchase(loaded, category, id), Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(loaded.Coins, Is.Zero);
            Assert.That(loaded.TotalEggsSpent, Is.EqualTo(price));
        }

        [Test]
        public void InsufficientFunds_DoesNotChangeSelectionCurrencyOrOwnership()
        {
            var data = new EggCatcherData { Coins = 499 };
            Assert.That(ShopCatalog.TryPurchase(data, ShopCategory.Character, "ninja"), Is.EqualTo(PurchaseResult.NotEnoughEggs));
            Assert.That(data.Coins, Is.EqualTo(499));
            Assert.That(data.TotalEggsSpent, Is.Zero);
            Assert.That(data.PurchasedSkins, Is.Empty);
            Assert.That(data.CurrentSkinId, Is.EqualTo("classic"));
        }

        [Test]
        public void AchievementLocks_UseInclusiveScoreAndLevelThresholds()
        {
            var data = new EggCatcherData { BestScore = 1999, Level = 11, Coins = 100000 };
            Assert.That(ShopCatalog.TryPurchase(data, ShopCategory.Character, "gold"), Is.EqualTo(PurchaseResult.Locked));
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Background, "night"), Is.False);
            data.BestScore = 2000;
            data.Level = 12;
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Character, "gold"), Is.True);
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Background, "night"), Is.True);
            Assert.That(data.Coins, Is.EqualTo(100000));
            Assert.That(data.TotalEggsSpent, Is.Zero);
        }

        [Test]
        public void FreeChoices_DoNotUnlockPaidItemsOrChangeAvatar()
        {
            var data = new EggCatcherData { AvatarIndex = 4 };
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Character, "pirate"), Is.True);
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Background, "sunset"), Is.True);
            Assert.That(data.AvatarIndex, Is.EqualTo(4));
            Assert.That(data.Coins, Is.Zero);
            Assert.That(ShopCatalog.TrySelect(data, ShopCategory.Character, "space"), Is.False);
            Assert.That(ShopCatalog.TryPurchase(data, ShopCategory.Character, "unknown"), Is.EqualTo(PurchaseResult.UnknownItem));
        }

        [Test]
        public void OldAndCorruptSaves_CannotEquipUnownedOrUnknownItems()
        {
            var data = GameDataStore.Deserialize("{\"Name\":\"Test\",\"CurrentSkinId\":\"ninja\",\"BackgroundId\":\"night\",\"PurchasedSkins\":[\"space\",\"space\",\"gold\",\"unknown\"],\"PurchasedBackgrounds\":null}");
            Assert.That(data.CurrentSkinId, Is.EqualTo("classic"));
            Assert.That(data.BackgroundId, Is.EqualTo("day"));
            Assert.That(data.PurchasedSkins, Is.EqualTo(new[] { "space" }));
            Assert.That(data.PurchasedBackgrounds, Is.Empty);
            Assert.That(data.NextLevelXp, Is.EqualTo(500));
        }
    }
}
