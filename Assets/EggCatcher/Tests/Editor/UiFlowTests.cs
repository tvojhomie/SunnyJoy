using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EggCatcher.Tests
{
    public sealed class UiFlowTests
    {
        private const string Slot = "UiFlowTest";
        private static int playRequests;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator AuthoredUi_ProfileValidationNavigationPersistencePhotosAndReset()
        {
            EditorSceneManager.OpenScene(EggCatcher.Editor.EggCatcherUiSetup.ScenePath);
            Assert.That(Object.FindFirstObjectByType<EggCatcherApp>(), Is.Not.Null, "Connect UI Template first.");
            yield return new EnterPlayMode();
            yield return new WaitForSecondsRealtime(1);
            var app = Object.FindFirstObjectByType<EggCatcherApp>();
            GameDataStore.Reset(Slot);
            typeof(EggCatcherApp).GetField("profileSlot", Private).SetValue(app, Slot);
            Call(app, "LoadProfile");
            Call(app, "ShowScreen", "CreateProfilePanel");
            var input = Find<InputField>(app, "CreateProfilePanel", "Panel/InputField (Legacy)");
            var start = Find<Button>(app, "CreateProfilePanel", "StartBtn");
            Assert.That(start.interactable, Is.False);
            input.text = "S";
            Assert.That(start.interactable, Is.False);
            Assert.That(Find<Text>(app, "CreateProfilePanel", "txt").text, Does.Contain("2–12"));
            var cardRect = (RectTransform)Node(app, "CreateProfilePanel", "Panel");
            var hintRect = Find<Text>(app, "CreateProfilePanel", "txt").rectTransform;
            Assert.That(hintRect.anchoredPosition.y + hintRect.rect.yMax,
                Is.LessThan(cardRect.anchoredPosition.y + cardRect.rect.yMin), "Validation must not be hidden behind the avatar card.");
            input.text = "Speckles";
            Assert.That(start.interactable, Is.False, "An avatar must be selected.");
            Click(app, "CreateProfilePanel", "Panel/Avatars/2");
            Assert.That(start.interactable, Is.True);
            Assert.That(Find<Button>(app, "CreateProfilePanel", "Panel/Avatars/7").interactable, Is.False);
            start.onClick.Invoke();
            AssertScreen(app, "MainMenu");
            Assert.That(GameDataStore.Load(Slot).Name, Is.EqualTo("Speckles"));
            Assert.That(GameDataStore.Load(Slot).AvatarIndex, Is.EqualTo(1));
            Assert.That(Find<Text>(app, "MainMenu", "TopPanel/MoneyPanel/NickName").text, Is.EqualTo("0"));
            Click(app, "MainMenu", "Stats");
            AssertScreen(app, "StatsPanel");
            Assert.That(Node(app, "StatsPanel", "EmptyResult").gameObject.activeSelf, Is.True);
            Assert.That(Node(app, "StatsPanel", "Result").gameObject.activeSelf, Is.False);
            Click(app, "StatsPanel", "TopPanel/Back");
            Click(app, "MainMenu", "TopPanel/ProfilePanel");
            Click(app, "ProfilePanel", "EditProfile");
            Find<InputField>(app, "EditProfilePanel", "Panel/InputField (Legacy)").text = "Cancelled";
            Click(app, "EditProfilePanel", "Panel/Avatars/3");
            Click(app, "EditProfilePanel", "Cancel");
            Assert.That(GameDataStore.Load(Slot).Name, Is.EqualTo("Speckles"));
            Click(app, "ProfilePanel", "EditProfile");
            Find<InputField>(app, "EditProfilePanel", "Panel/InputField (Legacy)").text = "Sunny";
            Click(app, "EditProfilePanel", "Panel/Avatars/4");

            var photoPath = Path.Combine(Application.temporaryCachePath, "egg-ui-test.jpg");
            var photo = new Texture2D(32, 16);
            File.WriteAllBytes(photoPath, photo.EncodeToJPG());
            Object.Destroy(photo);
            typeof(EggCatcherApp).GetField("pendingPhotoPath", Private).SetValue(app, photoPath);
            app.OnPhotoPicked(JsonUtility.ToJson(new EggCatcherPhotoPicker.Result { path = photoPath }));
            Assert.That(Node(app, "EditProfilePanel", "TakeAPhotoPanel").gameObject.activeSelf, Is.True);
            Find<Slider>(app, "EditProfilePanel", "TakeAPhotoPanel/Zoom/Slider").value = 2;
            Click(app, "EditProfilePanel", "TakeAPhotoPanel/Done");
            Assert.That(GameDataStore.Load(Slot).CustomAvatarFileName, Is.Empty, "Crop Done must not commit the draft.");
            Click(app, "EditProfilePanel", "Save");
            AssertScreen(app, "ProfilePanel");
            Assert.That(GameDataStore.Load(Slot).Name, Is.EqualTo("Sunny"));
            Assert.That(GameDataStore.Load(Slot).AvatarIndex, Is.EqualTo(3));
            Assert.That(File.Exists(Path.Combine(Application.persistentDataPath, GameDataStore.Load(Slot).CustomAvatarFileName)), Is.True);
            Call(app, "LoadProfile");
            Assert.That(app.Profile.Name, Is.EqualTo("Sunny"));
            Assert.That(Find<Image>(app, "ProfilePanel", "Panel/Icon").sprite.texture.width, Is.EqualTo(256));
            Assert.That(Find<Image>(app, "ProfilePanel", "Panel/Icon").sprite.texture.GetPixel(0, 0).a, Is.Zero,
                "Custom avatar corners must be transparent, matching the circular crop guide.");
            Click(app, "ProfilePanel", "TopPanel/Back");
            Click(app, "MainMenu", "Settings");
            AssertScreen(app, "SettingPanel");
            Click(app, "SettingPanel", "Sounds/Toggle");
            Assert.That(GameDataStore.Load(Slot).SoundEnabled, Is.False);
            Click(app, "SettingPanel", "Music/Toggle");
            Click(app, "SettingPanel", "Vibration/Toggle");
            Assert.That(GameDataStore.Load(Slot).MusicEnabled, Is.False);
            Assert.That(GameDataStore.Load(Slot).HapticsEnabled, Is.True);
            Click(app, "SettingPanel", "How to play");
            AssertScreen(app, "How to playPanel");
            Click(app, "How to playPanel", "GotIt");
            Assert.That(app.transform.Find("EggCatcherDialog/SafeArea/Card/Rules").GetComponent<Text>().text, Does.Contain("EGG RUSH"));
            app.transform.Find("EggCatcherDialog/SafeArea/Card/Done").GetComponent<Button>().onClick.Invoke();
            AssertScreen(app, "SettingPanel");
            Click(app, "SettingPanel", "TopPanel/Back");
            yield return null;
            Assert.That(app, Is.Not.Null, "App survives the frame after closing Settings.");
            Click(app, "MainMenu", "Play");
            var game = Object.FindFirstObjectByType<EggCatcherGame>();
            Assert.That(game, Is.Not.Null, "Play opens the authored game.");
            Assert.That(game.Round.Phase, Is.EqualTo(RoundPhase.Countdown));
            game.ReturnToMenu();
            playRequests = 0;
            app.PlayRequested += RecordPlayRequest;
            Click(app, "MainMenu", "Play");
            Assert.That(playRequests, Is.EqualTo(1));
            game.ReturnToMenu();
            Click(app, "MainMenu", "Skins");
            AssertScreen(app, "SkinsPanel");
            Assert.That(Node(app, "SkinsPanel", "CharactersContent/Golden/Lock").gameObject.activeSelf, Is.True);
            var progress = GameDataStore.Load(Slot);
            progress.Coins = 500;
            GameDataStore.Save(progress, Slot);
            Call(app, "LoadProfile");
            Click(app, "SkinsPanel", "CharactersContent/Ninja/Buy");
            Assert.That(Node(app, "SkinsPanel", "BuyPanel").gameObject.activeSelf, Is.True);
            Click(app, "SkinsPanel", "BuyPanel/Panel/Cancel");
            Assert.That(GameDataStore.Load(Slot).Coins, Is.EqualTo(500));
            Click(app, "SkinsPanel", "CharactersContent/Ninja/Buy");
            Click(app, "SkinsPanel", "BuyPanel/Panel/Reset");
            Call(app, "ConfirmPurchase");
            Assert.That(GameDataStore.Load(Slot).Coins, Is.Zero);
            Assert.That(GameDataStore.Load(Slot).TotalEggsSpent, Is.EqualTo(500));
            Assert.That(app.Profile.CurrentSkinId, Is.EqualTo("ninja"));
            Call(app, "LoadProfile");
            Assert.That(Node(app, "SkinsPanel", "CharactersContent/Ninja/Buy").gameObject.activeSelf, Is.False);
            Assert.That(Find<Image>(app, "SkinsPanel", "CharactersContent/Ninja/Btn").sprite.name, Is.EqualTo("Button_Selected"));
            Click(app, "SkinsPanel", "BackgroundsTab");
            Assert.That(Node(app, "SkinsPanel", "BGContent").gameObject.activeSelf, Is.True);
            Click(app, "SkinsPanel", "BGContent/Winter/Buy");
            var thumbnail = Find<Image>(app, "SkinsPanel", "BuyPanel/Panel/Icon").sprite;
            Assert.That(thumbnail.rect.width, Is.EqualTo(thumbnail.rect.height), "Background purchase uses a square thumbnail.");
            Click(app, "SkinsPanel", "BuyPanel/Panel/Reset");
            Assert.That(Node(app, "SkinsPanel", "Not enough eggsPanel").gameObject.activeSelf, Is.True);
            Click(app, "SkinsPanel", "Not enough eggsPanel");
            Click(app, "SkinsPanel", "BGContent/Sunset/Btn");
            Assert.That(GameDataStore.Load(Slot).BackgroundId, Is.EqualTo("sunset"));
            progress = GameDataStore.Load(Slot);
            progress.Level = 12;
            progress.BestScore = 2000;
            progress.GamesPlayed = 2;
            progress.TotalScore = 3000;
            GameDataStore.Save(progress, Slot);
            Call(app, "LoadProfile");
            Assert.That(Node(app, "SkinsPanel", "BGContent/Night Farm/Lock").gameObject.activeSelf, Is.False);
            Click(app, "SkinsPanel", "BGContent/Night Farm/Btn");
            Click(app, "SkinsPanel", "CharactersTab");
            Click(app, "SkinsPanel", "CharactersContent/Golden/Btn");
            Assert.That(GameDataStore.Load(Slot).CurrentSkinId, Is.EqualTo("gold"));
            Click(app, "SkinsPanel", "TopPanel/Back");
            AssertScreen(app, "MainMenu");
            Click(app, "MainMenu", "Stats");
            Assert.That(Node(app, "StatsPanel", "Result").gameObject.activeSelf, Is.True);
            Assert.That(Find<Text>(app, "StatsPanel", "Result/Average score/Value").text, Is.EqualTo("1,500"));
            Click(app, "StatsPanel", "TopPanel/Back");
            Click(app, "MainMenu", "TopPanel/ProfilePanel");
            Click(app, "ProfilePanel", "CurrentLook");
            AssertScreen(app, "SkinsPanel");
            Click(app, "SkinsPanel", "TopPanel/Back");
            AssertScreen(app, "ProfilePanel");
            Click(app, "ProfilePanel", "ResetProgress");
            Click(app, "ProfilePanel", "ResetPanel/Panel/Cancel");
            Assert.That(GameDataStore.Load(Slot).HasProfile, Is.True);
            Click(app, "ProfilePanel", "ResetProgress");
            Click(app, "ProfilePanel", "ResetPanel/Panel/Reset");
            AssertScreen(app, "CreateProfilePanel");
            Assert.That(GameDataStore.Load(Slot).HasProfile, Is.False);
            Assert.That(GameDataStore.Load(Slot).PurchasedSkins, Is.Empty);
            Assert.That(start.interactable, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var file = GameDataStore.Load(Slot).CustomAvatarFileName;
            if (file.StartsWith("egg-profile-") && Path.GetFileName(file) == file)
            {
                var path = Path.Combine(Application.persistentDataPath, file);
                if (File.Exists(path)) File.Delete(path);
            }
            GameDataStore.Reset(Slot);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static Transform Node(EggCatcherApp app, string screen, string path) =>
            app.transform.Find(screen + "/SafeAreaContent/" + path);
        private static T Find<T>(EggCatcherApp app, string screen, string path) where T : Component => Node(app, screen, path).GetComponent<T>();
        private static void Click(EggCatcherApp app, string screen, string path)
        {
            var button = Find<Button>(app, screen, path);
            Assert.That(button.interactable && button.gameObject.activeInHierarchy, Is.True, screen + "/" + path);
            button.onClick.Invoke();
        }
        private static void Call(EggCatcherApp app, string method, params object[] args) => typeof(EggCatcherApp).GetMethod(method, Private).Invoke(app, args);
        private static void RecordPlayRequest() => playRequests++;
        private static void AssertScreen(EggCatcherApp app, string screen)
        {
            foreach (Transform child in app.transform)
                if (child.GetComponent<Image>() != null)
                    Assert.That(child.gameObject.activeSelf, Is.EqualTo(child.name == screen), child.name);
        }
    }
}
