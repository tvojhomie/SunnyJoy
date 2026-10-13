using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EggCatcher
{
    public sealed partial class EggCatcherApp
    {
        [SerializeField] private Sprite selectedCardSprite;
        [SerializeField] private Sprite selectedActionSprite;
        [SerializeField] private Sprite toggleOffSprite;
        private readonly Dictionary<ShopItem, ShopCard> shopCards = new Dictionary<ShopItem, ShopCard>();
        private string shopReturnScreen = "MainMenu";
        private ShopItem pendingPurchase;
        private Sprite toggleOnSprite;
        private Text xpText;
        public Sprite SelectedCharacterSprite => shopCards[ShopCatalog.Find(ShopCategory.Character, data.CurrentSkinId)].Art;
        public Sprite SelectedBackgroundSprite => shopCards[ShopCatalog.Find(ShopCategory.Background, data.BackgroundId)].Art;

        private sealed class ShopCard
        {
            public Transform Root;
            public Image Card, Action;
            public Sprite Art, Thumbnail, UnselectedCard, SelectAction;
            public GameObject Buy, Lock, PreviewLock;
            public Button Select;
        }

        private void BindShop()
        {
            foreach (var item in ShopCatalog.Items)
            {
                var root = Node("SkinsPanel", item.ScenePath);
                var card = new ShopCard
                {
                    Root = root,
                    Card = root.GetComponent<Image>(),
                    Select = root.Find("Btn").GetComponent<Button>(),
                    Action = root.Find("Btn").GetComponent<Image>(),
                    Art = root.Find(item.Category == ShopCategory.Character ? "bg/Icon" : "bg/BG").GetComponent<Image>().sprite,
                    Buy = root.Find("Buy")?.gameObject,
                    Lock = root.Find("Lock")?.gameObject,
                    PreviewLock = root.Find("bg/Lock")?.gameObject
                };
                card.UnselectedCard = card.Card.sprite;
                card.SelectAction = card.Action.sprite;
                if (item.Category == ShopCategory.Background)
                {

                    var bounds = card.Art.rect;
                    float side = Mathf.Min(bounds.width, bounds.height);
                    float bottom = Mathf.Clamp(bounds.height * .28f - side / 2, 0, bounds.height - side);
                    card.Thumbnail = Sprite.Create(card.Art.texture,
                        new Rect(bounds.x + (bounds.width - side) / 2, bounds.y + bottom, side, side), Vector2.one * .5f);
                }
                shopCards.Add(item, card);
                card.Select.onClick.AddListener(() => ChooseShopItem(item));
                if (card.Buy != null)
                {
                    var buy = card.Buy.GetComponent<Button>() ?? card.Buy.AddComponent<Button>();
                    buy.targetGraphic = card.Buy.GetComponent<Image>();
                    buy.onClick.AddListener(() => ChooseShopItem(item));
                }
                if (card.PreviewLock != null) card.PreviewLock.GetComponent<Image>().raycastTarget = false;
            }

            Get<Button>("SkinsPanel", "BackgroundsBtn").enabled = false;
            Get<Button>("SkinsPanel", "CharactersBtn").enabled = false;
            foreach (var name in new[] { "BackgroundsBtn", "CharactersBtn" })
                Get<Image>("SkinsPanel", name).raycastTarget = false;
            MakeTabHit("CharactersTab", 0, .5f, () => SetShopTab(false));
            MakeTabHit("BackgroundsTab", .5f, 1, () => SetShopTab(true));
            Bind("SkinsPanel", "TopPanel/Back", () => ShowScreen(shopReturnScreen));
            Bind("SkinsPanel", "BuyPanel/Panel/Cancel", CloseShopDialogs);
            Bind("SkinsPanel", "BuyPanel/Panel/Reset", ConfirmPurchase);
            Bind("SkinsPanel", "Not enough eggsPanel/Panel/Play", () => { CloseShopDialogs(); RequestPlay(); });
            AddDismissArea("BuyPanel");
            AddDismissArea("Not enough eggsPanel");
            SetShopTab(false);
        }

        private void MakeTabHit(string name, float from, float to, UnityEngine.Events.UnityAction action)
        {
            var strip = Node("SkinsPanel", "BackgroundsBtn").GetComponent<RectTransform>();
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(strip.parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = strip.anchoredPosition + Vector2.right * ((from + to - 1) * strip.sizeDelta.x / 2);
            rect.sizeDelta = new Vector2(strip.sizeDelta.x * (to - from), strip.sizeDelta.y);
            go.GetComponent<Image>().color = Color.clear;
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);

            go.transform.SetSiblingIndex(Node("SkinsPanel", "BuyPanel").GetSiblingIndex());
        }

        private void AddDismissArea(string panel)
        {
            var node = Node("SkinsPanel", panel);
            var button = node.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = node.GetComponent<Image>();
            button.onClick.AddListener(CloseShopDialogs);

            var card = node.Find("Panel");
            var blocker = card.GetComponent<Button>() ?? card.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.targetGraphic = card.GetComponent<Image>();
        }

        private void OpenShop(string returnScreen)
        {
            shopReturnScreen = returnScreen;
            RefreshShop();
            ShowScreen("SkinsPanel");
        }

        private void SetShopTab(bool backgrounds)
        {
            Node("SkinsPanel", "CharactersContent").gameObject.SetActive(!backgrounds);
            Node("SkinsPanel", "BGContent").gameObject.SetActive(backgrounds);
            Node("SkinsPanel", "BackgroundsBtn").gameObject.SetActive(!backgrounds);
            Node("SkinsPanel", "CharactersBtn").gameObject.SetActive(backgrounds);
        }

        private void ChooseShopItem(ShopItem item)
        {
            if (ShopCatalog.IsLocked(data, item)) { gameAudio?.PlayUi(EggSound.Error); return; }
            if (ShopCatalog.IsOwned(data, item))
            {
                ShopCatalog.TrySelect(data, item.Category, item.Id);
                GameDataStore.Save(data, profileSlot);
                RefreshProfile();
                gameAudio?.PlayUi(item.Category == ShopCategory.Character ? EggSound.SkinSelect : EggSound.BackgroundSelect);
                return;
            }
            pendingPurchase = item;
            SetText("SkinsPanel", "BuyPanel/Panel/Tittle", "Buy for " + GameDataStore.FormatNumber(item.Price) + " eggs?");
            var title = Get<Text>("SkinsPanel", "BuyPanel/Panel/Tittle");
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 34;
            title.resizeTextMaxSize = 53;
            var icon = Get<Image>("SkinsPanel", "BuyPanel/Panel/Icon");
            icon.sprite = shopCards[item].Thumbnail != null ? shopCards[item].Thumbnail : shopCards[item].Art;
            icon.preserveAspect = true;
            Node("SkinsPanel", "BuyPanel").gameObject.SetActive(true);
            Node("SkinsPanel", "BuyPanel").SetAsLastSibling();
        }

        private void ConfirmPurchase()
        {
            var item = pendingPurchase;
            if (item == null) return;
            CloseShopDialogs();
            var result = ShopCatalog.TryPurchase(data, item.Category, item.Id);
            if (result == PurchaseResult.NotEnoughEggs)
            {
                Node("SkinsPanel", "Not enough eggsPanel").gameObject.SetActive(true);
                Node("SkinsPanel", "Not enough eggsPanel").SetAsLastSibling();
                gameAudio?.PlayUi(EggSound.Error);
                return;
            }
            if (result == PurchaseResult.Purchased)
            {
                GameDataStore.Save(data, profileSlot);
                RefreshProfile();
                gameAudio?.PlayUi(EggSound.Purchase);
            }
        }

        private void CloseShopDialogs()
        {
            pendingPurchase = null;
            Node("SkinsPanel", "BuyPanel").gameObject.SetActive(false);
            Node("SkinsPanel", "Not enough eggsPanel").gameObject.SetActive(false);
        }

        private void ReleaseShopThumbnails()
        {
            foreach (var card in shopCards.Values)
                if (card.Thumbnail != null) Destroy(card.Thumbnail);
        }

        private void RefreshShop()
        {
            if (shopCards.Count == 0) return;
            foreach (var pair in shopCards)
            {
                var item = pair.Key;
                var card = pair.Value;
                bool locked = ShopCatalog.IsLocked(data, item);
                bool owned = ShopCatalog.IsOwned(data, item);
                bool selected = item.Id == (item.Category == ShopCategory.Character ? data.CurrentSkinId : data.BackgroundId);
                card.Card.sprite = selected ? selectedCardSprite : card.UnselectedCard;
                card.Select.gameObject.SetActive(owned);
                card.Select.interactable = owned;
                card.Action.sprite = selected ? selectedActionSprite : card.SelectAction;
                if (card.Buy != null) card.Buy.SetActive(!locked && !owned);
                if (card.Lock != null) card.Lock.SetActive(locked);
                if (card.PreviewLock != null) card.PreviewLock.SetActive(locked);
                if (item.Category == ShopCategory.Background)
                    card.Root.Find("bg/Icon").GetComponent<Image>().sprite = SelectedCharacterSprite;
            }
            SetText("SkinsPanel", "TopPanel/MoneyPanel/NickName", GameDataStore.FormatNumber(data.Coins));
            SetText("SkinsPanel", "Prewiew/SkinPanel/Tittle", ShopCatalog.Find(ShopCategory.Character, data.CurrentSkinId).Name + " · " +
                ShopCatalog.Find(ShopCategory.Background, data.BackgroundId).Name);
            var previewName = Get<Text>("SkinsPanel", "Prewiew/SkinPanel/Tittle");
            previewName.resizeTextForBestFit = true;
            previewName.resizeTextMinSize = 22;
            previewName.resizeTextMaxSize = 35;
            var preview = Get<Image>("SkinsPanel", "Prewiew/IconPlayer");
            preview.sprite = SelectedCharacterSprite;
            preview.preserveAspect = true;
            var bg = Get<Image>("SkinsPanel", "Prewiew/BG");
            bg.sprite = SelectedBackgroundSprite;
            PositionBackdrop(bg);
        }

        private static void PositionBackdrop(Image image)
        {

            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(0, rect.sizeDelta.y * .22f);
        }

        private void RefreshCurrentLook()
        {
            if (shopCards.Count == 0) return;
            var character = ShopCatalog.Find(ShopCategory.Character, data.CurrentSkinId);
            var background = ShopCatalog.Find(ShopCategory.Background, data.BackgroundId);
            var menuCharacter = Get<Image>("MainMenu", "CharSkin");
            menuCharacter.sprite = SelectedCharacterSprite;
            menuCharacter.preserveAspect = true;
            Get<Image>("ProfilePanel", "CurrentLook/Prewiew/IconPlayer").sprite = SelectedCharacterSprite;
            var bg = Get<Image>("ProfilePanel", "CurrentLook/Prewiew/BG");
            bg.sprite = SelectedBackgroundSprite;
            PositionBackdrop(bg);
            SetText("ProfilePanel", "CurrentLook/Prewiew/SkinPanel/Tittle", "Skin: " + character.Name);
            SetText("ProfilePanel", "CurrentLook/Prewiew/BGPanel/Tittle", "Background: " + background.Name);
            foreach (var path in new[] { "SkinPanel/Tittle", "BGPanel/Tittle" })
            {
                var label = Get<Text>("ProfilePanel", "CurrentLook/Prewiew/" + path);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 26;
            }
            RefreshShop();
        }

        private void BindSettingsAndHelp()
        {
            toggleOnSprite = Get<Image>("SettingPanel", "Music/Toggle").sprite;
            Bind("SettingPanel", "TopPanel/Back", () => ShowScreen("MainMenu"));
            Bind("SettingPanel", "Music/Toggle", () => { data.MusicEnabled = !data.MusicEnabled; SaveSettings(); });
            Bind("SettingPanel", "Sounds/Toggle", () => { data.SoundEnabled = !data.SoundEnabled; SaveSettings(); });
            Bind("SettingPanel", "Vibration/Toggle", () => { data.HapticsEnabled = !data.HapticsEnabled; SaveSettings(); });
            Bind("SettingPanel", "How to play", () => ShowScreen("How to playPanel"));
            Bind("How to playPanel", "TopPanel/Back", () => ShowScreen("SettingPanel"));
            var helpRoot = Node("How to playPanel", "Content").parent;
            var done = MakeButton("GotIt", helpRoot, "More tips", new Vector2(0, -835), new Vector2(780, 170), ShowAdvancedHelp);
            done.transform.SetAsLastSibling();
            var settingsRoot = Node("SettingPanel", "Music").parent;
            bool hasPolicyButton = settingsRoot.Find("Politic") != null;
            var chick = new GameObject("Chick", typeof(RectTransform), typeof(Image));
            chick.transform.SetParent(settingsRoot, false);
            var rect = (RectTransform)chick.transform;
            rect.anchoredPosition = new Vector2(0, hasPolicyButton ? -540 : -580);
            rect.sizeDelta = Vector2.one * (hasPolicyButton ? 400 : 470);
            var image = chick.GetComponent<Image>();
            image.sprite = shopCards[ShopCatalog.Find(ShopCategory.Character, "classic")].Art;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var version = MakeLabel("Version", settingsRoot, "Version " + Application.version,
                new Vector2(0, hasPolicyButton ? -944 : -865), new Vector2(550, hasPolicyButton ? 24 : 55), hasPolicyButton ? 22 : 29);
            version.color = Color.white;
            xpText = MakeLabel("Experience", Node("ProfilePanel", "Panel/LevelProgressPanel"), "", Vector2.zero, new Vector2(490, 60), 33);
            xpText.color = Color.white;
            var shadow = xpText.gameObject.AddComponent<Outline>();
            shadow.effectColor = Ink;
            shadow.effectDistance = new Vector2(1, -1);
        }

        private void ShowAdvancedHelp()
        {
            var card = CreateModal("Farm challenges", 1040);
            MakeLabel("Rules", card,
                "EGG RUSH\nCatch 20 foods without a miss to trigger 6 seconds of double points and faster egg drops.\n\n" +
                "SHIELD\nA heart at full health blocks the next hit.\n\n" +
                "ROUND MISSIONS\nCollect 20 eggs, 5 corn and 3 worms to earn up to 45 bonus eggs. Rewards arrive at game over; restarting forfeits them.",
                new Vector2(0, -5), new Vector2(800, 670), 35);
            MakeButton("Done", card, "Got it!", new Vector2(0, -415), new Vector2(740, 135),
                () => { CloseModal(); ShowScreen("SettingPanel"); });
        }

        private void SaveSettings()
        {
            GameDataStore.Save(data, profileSlot);
            RefreshSettings();
            SettingsChanged?.Invoke();
            gameAudio?.PlayUi(EggSound.Toggle);
        }

        private void RefreshSettings()
        {
            Get<Image>("SettingPanel", "Music/Toggle").sprite = data.MusicEnabled ? toggleOnSprite : toggleOffSprite;
            Get<Image>("SettingPanel", "Sounds/Toggle").sprite = data.SoundEnabled ? toggleOnSprite : toggleOffSprite;
            Get<Image>("SettingPanel", "Vibration/Toggle").sprite = data.HapticsEnabled ? toggleOnSprite : toggleOffSprite;
            xpText.text = data.Level >= EggCatcherData.MaximumLevel ? "MAX" : GameDataStore.FormatNumber(data.LevelXp) + " / " + GameDataStore.FormatNumber(data.NextLevelXp) + " XP";
            Get<Image>("ProfilePanel", "Panel/LevelProgressPanel/Fill").fillAmount = data.Level >= EggCatcherData.MaximumLevel ? 1 : (float)data.LevelXp / data.NextLevelXp;
        }
    }
}
