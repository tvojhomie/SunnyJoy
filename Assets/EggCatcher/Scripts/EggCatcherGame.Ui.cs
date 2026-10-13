using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EggCatcher
{
    public sealed partial class EggCatcherGame
    {
        private GameObject countdownPanel, pausePanel, resultPanel, restartConfirm, quitConfirm;
        private Text countdownText, scoreText, comboText;
        private Text rushText;
        private Image rushFill;
        private int hudCharge = -1, hudRushSeconds = -1;
        private bool hudShield;
        private Image magnetIcon, magnetFill, damageOverlay;
        private Image[] hearts;
        private Font uiFont;
        private Sprite normalRibbon;
        private RectTransform effectsRoot;
        private int hudScore = -1, hudLives = -1, hudStreak = -1;

        private void BuildGameUi()
        {
            countdownPanel = Ui("PreRun").gameObject;
            pausePanel = Ui("PausedPanel").gameObject;
            resultPanel = Ui("GameOver").gameObject;
            restartConfirm = Ui("PausedPanel/SafeAreaContent/RestartGame?").gameObject;
            quitConfirm = Ui("PausedPanel/SafeAreaContent/QuitMenu?").gameObject;
            countdownText = Ui<Text>("PreRun/SafeAreaContent/Count");
            countdownText.fontSize = 480;
            FitText(countdownText, 240, 480);
            countdownText.color = new Color32(255, 205, 48, 255);
            var countOutline = countdownText.GetComponent<Outline>();
            if (countOutline != null)
            {
                countOutline.effectColor = new Color32(117, 66, 4, 255);
                countOutline.effectDistance = new Vector2(10, -10);
            }
            scoreText = Ui<Text>("TopPanel/ScorePanel/Score");
            uiFont = scoreText.font;
            FitText(scoreText, 32, 90);
            hearts = Ui("TopPanel/Panel/Health").GetComponentsInChildren<Image>(true);
            EggCatcherHeartLayout.Configure(Ui("TopPanel/Panel/Health"));
            magnetIcon = Ui<Image>("TopPanel/Panel/MagnetIcon");
            magnetFill = Ui<Image>("TopPanel/Panel/MagnetIcon/Fill");
            magnetFill.type = Image.Type.Filled;
            magnetFill.fillMethod = Image.FillMethod.Radial360;
            magnetFill.fillOrigin = 2;
            magnetFill.fillClockwise = false;
            normalRibbon = Ui<Image>("GameOver/SafeAreaContent/Panel/Tittle").sprite;

            var inputObject = new GameObject("Playfield Touch", typeof(RectTransform), typeof(Image), typeof(EggCatcherGameInput));
            inputObject.transform.SetParent(gameCanvas.transform, false);
            Stretch((RectTransform)inputObject.transform);
            inputObject.transform.SetAsFirstSibling();
            inputObject.GetComponent<Image>().color = Color.clear;
            input = inputObject.GetComponent<EggCatcherGameInput>();
            input.Pointed = position =>
            {
                if (Round != null && Round.Phase == RoundPhase.Running)
                    targetX = worldCamera.ScreenToWorldPoint(new Vector3(position.x, 0, 10)).x;
            };
            var fx = new GameObject("Gameplay Feedback", typeof(RectTransform));
            fx.transform.SetParent(gameCanvas.transform, false);
            effectsRoot = (RectTransform)fx.transform;
            Stretch(effectsRoot);
            fx.transform.SetSiblingIndex(1);
            var flash = new GameObject("Damage Flash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(effectsRoot, false);
            Stretch((RectTransform)flash.transform);
            damageOverlay = flash.GetComponent<Image>();
            damageOverlay.color = Color.clear;
            damageOverlay.raycastTarget = false;
            BuildRoundStatusUi();

            BindUiButton("TopPanel/Panel/SettingBtn", PauseRound);
            BindUiButton("PausedPanel/SafeAreaContent/Panel/Resume", ResumeRound);
            BindUiButton("PausedPanel/SafeAreaContent/Panel/Restart", () => SetConfirmation(true));
            BindUiButton("PausedPanel/SafeAreaContent/Panel/MainMenu", () => SetConfirmation(false));
            const string restart = "PausedPanel/SafeAreaContent/RestartGame?/SafeAreaContent/Panel/";
            const string quit = "PausedPanel/SafeAreaContent/QuitMenu?/SafeAreaContent/Panel/";
            BindUiButton(restart + "Back", ClearConfirmation);
            BindUiButton(quit + "Back", ClearConfirmation);
            BindUiButton(restart + "Restart", () => { Round.Abandon(); StartRound(); });
            BindUiButton(quit + "Quit", ReturnToMenu);
            Ui(quit + "Quit/Tittle").gameObject.SetActive(true);
            BindUiButton("GameOver/SafeAreaContent/Panel/Again", StartRound);
            BindUiButton("GameOver/SafeAreaContent/Menu", ReturnToMenu);
            BindUiButton("PausedPanel/SafeAreaContent/Panel/Music/Toggle", () => app.ToggleGameSetting(0));
            BindUiButton("PausedPanel/SafeAreaContent/Panel/Sound/Toggle", () => app.ToggleGameSetting(1));
            BindUiButton("PausedPanel/SafeAreaContent/Panel/Vibration/Toggle", () => app.ToggleGameSetting(2));

            foreach (var path in new[] { "Score", "BestScore", "Best streak", "Eggs caught", "Round time", "PlusEggs" })
            {
                var text = Ui<Text>("GameOver/SafeAreaContent/Panel/" + path);
                FitText(text, path == "Score" ? 70 : 24, text.resizeTextForBestFit ? text.resizeTextMaxSize : text.fontSize);
            }
            countdownPanel.transform.SetAsLastSibling();
            pausePanel.transform.SetAsLastSibling();
            resultPanel.transform.SetAsLastSibling();
            HideOverlays();
        }

        private void BuildRoundStatusUi()
        {
            var top = (RectTransform)Ui("TopPanel");
            top.GetComponentInParent<EggCatcherSafeArea>(true)?.AlignToTop();
            var score = (RectTransform)Ui("TopPanel/ScorePanel");
            var controls = (RectTransform)Ui("TopPanel/Panel");
            top.anchorMin = top.anchorMax = new Vector2(.5f, 1);
            top.pivot = new Vector2(.5f, 1);
            top.anchoredPosition = new Vector2(0, -24);
            top.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(score.rect.height, controls.rect.height));
            score.anchoredPosition = new Vector2(score.anchoredPosition.x, 0);
            controls.anchoredPosition = new Vector2(controls.anchoredPosition.x, 0);
            var statusObject = new GameObject("Round Status", typeof(RectTransform));
            var status = (RectTransform)statusObject.transform;
            status.SetParent(top, false);
            status.anchorMin = new Vector2(0, 0);
            status.anchorMax = new Vector2(1, 0);
            status.pivot = new Vector2(.5f, 1);
            status.anchoredPosition = new Vector2(0, -20);
            status.sizeDelta = new Vector2(-56, 60);

            comboText = Label("Combo", status, Vector2.zero, new Vector2(208, 38), 29);
            PinToTop(comboText.rectTransform, 0, new Vector2(104, -19));
            comboText.color = new Color32(255, 213, 55, 255);
            FitText(comboText, 24, 29);
            rushText = Label("Rush Status", status, Vector2.zero, new Vector2(360, 38), 26);
            PinToTop(rushText.rectTransform, .5f, new Vector2(0, -19));
            rushText.color = Color.white;
            FitText(rushText, 22, 26);

            var progress = new GameObject("Rush Progress", typeof(RectTransform), typeof(Image));
            progress.transform.SetParent(status, false);
            var progressRect = (RectTransform)progress.transform;
            progressRect.sizeDelta = new Vector2(508, 12);
            PinToTop(progressRect, .5f, new Vector2(0, -50));
            progress.GetComponent<Image>().color = new Color32(30, 76, 126, 220);
            progress.GetComponent<Image>().raycastTarget = false;
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(progress.transform, false);
            Stretch((RectTransform)fillObject.transform);
            rushFill = fillObject.GetComponent<Image>();
            rushFill.type = Image.Type.Simple;
            rushFill.color = new Color32(255, 216, 46, 255);
            rushFill.raycastTarget = false;

        }

        private static void PinToTop(RectTransform rect, float horizontalAnchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(horizontalAnchor, 1);
            rect.anchoredPosition = position;
        }

        private Transform Ui(string path)
        {

            var found = gameCanvas.transform.Find(path);
            if (found == null && path.StartsWith("TopPanel")) found = gameCanvas.transform.Find("HudSafeArea/" + path);
            if (found == null) throw new System.InvalidOperationException("Missing gameplay UI: " + path);
            return found;
        }
        private T Ui<T>(string path) where T : Component => Ui(path).GetComponent<T>();
        private void BindUiButton(string path, UnityAction action)
        {
            var button = Ui<Button>(path);
            button.onClick.AddListener(() => { action(); audioPlayer.PlayUi(); });

            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private void SetConfirmation(bool restart)
        {
            if (Round.Phase != RoundPhase.Paused) return;
            restartConfirm.SetActive(restart);
            quitConfirm.SetActive(!restart);
        }
        private void ClearConfirmation()
        {
            restartConfirm.SetActive(false); quitConfirm.SetActive(false);
        }
        private void HideOverlays()
        {
            countdownPanel.SetActive(false); pausePanel.SetActive(false); resultPanel.SetActive(false);
            ClearConfirmation();
        }
        private void RefreshPauseSettings()
        {
            Ui<Image>("PausedPanel/SafeAreaContent/Panel/Music/Toggle").sprite = app.MusicEnabled ? toggleOn : toggleOff;
            Ui<Image>("PausedPanel/SafeAreaContent/Panel/Sound/Toggle").sprite = app.SoundsEnabled ? toggleOn : toggleOff;
            Ui<Image>("PausedPanel/SafeAreaContent/Panel/Vibration/Toggle").sprite = app.HapticsEnabled ? toggleOn : toggleOff;
        }
        private void UpdateHud()
        {
            if (hudScore != Round.Score) { hudScore = Round.Score; scoreText.text = GameDataStore.FormatNumber(hudScore); }
            if (hudLives != Round.Lives)
            {
                hudLives = Round.Lives;
                for (int i = 0; i < hearts.Length; i++) EggCatcherHeartLayout.Set(hearts[i], i < hudLives, fullHeart, emptyHeart);
            }
            magnetIcon.sprite = Round.MagnetRemaining > 0 ? activeMagnet : inactiveMagnet;
            magnetFill.fillAmount = Round.MagnetRemaining / Mathf.Max(.1f, tuning.MagnetSeconds);
            float progress = Mathf.Clamp01(Round.FrenzyProgress);
            if (!Mathf.Approximately(rushFill.rectTransform.anchorMax.x, progress))
            {
                rushFill.rectTransform.anchorMax = new Vector2(progress, 1);
                rushFill.rectTransform.offsetMax = Vector2.zero;
            }
            int seconds = Mathf.CeilToInt(Round.FrenzyRemaining);
            if (hudCharge != Round.FrenzyCharge || hudRushSeconds != seconds || hudShield != Round.HasShield)
            {
                hudCharge = Round.FrenzyCharge; hudRushSeconds = seconds; hudShield = Round.HasShield;
                rushText.text = (hudShield ? "SHIELD  •  " : "") + (Round.IsFrenzy ?
                    "EGG RUSH ×2  " + seconds + "s" : "Egg rush  " + hudCharge + "/" + Mathf.Max(1, tuning.FrenzyCatches));
                rushText.color = Round.IsFrenzy ? new Color32(255, 228, 74, 255) : Color.white;
            }
            if (hudStreak != Round.Streak)
            {
                hudStreak = Round.Streak;
                comboText.text = hudStreak > 0 ? "×" + Round.Multiplier + " · " + hudStreak : string.Empty;
            }
        }
        private void ShowResult(bool record)
        {
            HideOverlays();
            resultPanel.SetActive(true);
            const string panel = "GameOver/SafeAreaContent/Panel/";
            Ui<Text>(panel + "Score").text = GameDataStore.FormatNumber(Round.Score);
            Ui<Text>(panel + "BestScore").text = (record ? "Previous best: " : "Best: ") + GameDataStore.FormatNumber(previousBest);
            Ui<Text>(panel + "Eggs caught").text = GameDataStore.FormatNumber(Round.EggsCaught);
            Ui<Text>(panel + "Best streak").text = "×" + Round.BestMultiplier + " · " + Round.BestStreak;
            int seconds = Mathf.FloorToInt((float)Round.Elapsed);
            Ui<Text>(panel + "Round time").text = seconds / 60 + ":" + (seconds % 60).ToString("00");
            Ui<Text>(panel + "PlusEggs").text = "+" + GameDataStore.FormatNumber(Round.EarnedEggs) + " eggs" +
                (Round.MissionBonus > 0 ? " (" + Round.MissionBonus + " bonus)" : "");
            Ui<Image>(panel + "Tittle").sprite = record ? newBestRibbon : normalRibbon;
            var icon = Ui<Image>(panel + "Icon");
            icon.sprite = skin.Hurt;
            icon.preserveAspect = true;
        }

        private void Pop(string message, Vector3 position, Color colour)
        {
            if (floaters.Count >= 16) return;
            Vector2 screen = worldCamera.WorldToScreenPoint(position + Vector3.up * .3f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(effectsRoot, screen, null, out var local);
            var label = Label("Catch Feedback", effectsRoot, local, new Vector2(420, 100), 65);
            label.text = message;
            label.color = colour;
            floaters.Add(new FloatingText { Label = label, Origin = local, Life = .75f });
        }
        private Text Label(string labelName, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(labelName, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = uiFont;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color32(100, 58, 5, 255);
            outline.effectDistance = new Vector2(2, -2);
            return label;
        }
        private void EmitBurst(Vector3 position, EggItemKind kind, bool hurt)
        {
            for (int i = 0; i < 5 && bursts.Count < 40; i++)
            {
                var renderer = MakeSprite("Catch Spark", itemSprites[(int)(hurt ? EggItemKind.Rock : EggItemKind.Egg)], 25, .08f);
                renderer.transform.position = position;
                renderer.color = hurt ? new Color(1, .5f, .3f) : new Color(1, .92f, .5f);
                bursts.Add(new Burst { Renderer = renderer, Velocity = new Vector2(UnityEngine.Random.Range(-1.8f, 1.8f), UnityEngine.Random.Range(.6f, 2)), Life = .45f });
            }
        }
        private void TickEffects(float dt)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var floating = floaters[i];
                floating.Life -= dt;
                if (floating.Life <= 0) { Destroy(floating.Label.gameObject); floaters.RemoveAt(i); continue; }
                float t = 1 - floating.Life / .75f;
                floating.Label.rectTransform.anchoredPosition = floating.Origin + Vector2.up * (100 * t);
                floating.Label.transform.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .12f);
                var colour = floating.Label.color; colour.a = Mathf.Min(1, floating.Life * 4); floating.Label.color = colour;
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var burst = bursts[i]; burst.Life -= dt;
                if (burst.Life <= 0) { Destroy(burst.Renderer.gameObject); bursts.RemoveAt(i); continue; }
                burst.Velocity += Vector2.down * (5 * dt);
                burst.Renderer.transform.position += (Vector3)(burst.Velocity * dt);
                var colour = burst.Renderer.color; colour.a = burst.Life / .45f; burst.Renderer.color = colour;
            }
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void FitText(Text text, int min, int max)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = Mathf.Max(min, max);
        }
        private sealed class Burst { public SpriteRenderer Renderer; public Vector2 Velocity; public float Life; }
        private sealed class FloatingText { public Text Label; public Vector2 Origin; public float Life; }
    }
}
