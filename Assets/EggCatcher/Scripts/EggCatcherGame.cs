using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EggCatcher
{
    [Serializable]
    public sealed class EggCatcherSkinFrames
    {
        public string Id;
        public Sprite Stand, Left, Right, Hurt;
    }

    [DisallowMultipleComponent]
    public sealed partial class EggCatcherGame : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas gameCanvas;
        [SerializeField] private SpriteRenderer backdrop, player;
        [SerializeField] private Sprite[] itemSprites;
        [SerializeField] private EggCatcherSkinFrames[] skins;
        [SerializeField] private Sprite truckSprite, crowSprite, balloonSprite, cloudSprite;
        [SerializeField] private Sprite emptyHeart, fullHeart, activeMagnet, inactiveMagnet, newBestRibbon;
        [SerializeField] private Sprite toggleOn, toggleOff;
        [SerializeField] private EggCatcherTuning tuning = new EggCatcherTuning();
        private EggCatcherApp app;
        private EggCatcherAudio audioPlayer;
        private EggCatcherSkinFrames skin;
        private EggCatcherGameInput input;
        private SpriteRenderer truck, crow, balloon;
        private readonly List<FallingItem> items = new List<FallingItem>(48);
        private readonly Stack<FallingItem> pool = new Stack<FallingItem>(48);
        private readonly List<Burst> bursts = new List<Burst>(32);
        private readonly List<FloatingText> floaters = new List<FloatingText>(16);
        private readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();
        private Transform itemRoot;
        private float halfWidth, groundY, fallStartY, truckY, crowY, balloonY;
        private float targetX, spawnTimer, hazardTimer, powerTimer, animationClock, damageFlash, catchBounce;
        private int lastCount = -1, previousBest;
        private bool initialized, resultShown;
        private Vector2Int lastViewportSize;
        private Rect lastSafeArea;
        private Vector3 playerScale;
        public EggCatcherRound Round { get; private set; }
        public int ActiveItemCount => items.Count;
        public Vector3 PlayerPosition => player.transform.position;
        public EggCatcherTuning Tuning => tuning;

        public void Initialize(EggCatcherApp owner, EggCatcherAudio audio)
        {
            if (initialized) return;
            app = owner;
            audioPlayer = audio;
            itemRoot = new GameObject("Falling Items").transform;
            itemRoot.SetParent(transform, false);
            backdrop.sortingOrder = -100;
            player.sortingOrder = 20;
            truck = MakeSprite("Truck", truckSprite, 5, 1.8f);
            crow = MakeSprite("Crow", crowSprite, 7, .85f);
            balloon = MakeSprite("Balloon", balloonSprite, 6, .7f);
            for (int i = 0; i < 3; i++) clouds.Add(MakeSprite("Drifting Cloud", cloudSprite, -90, .9f + i * .2f));
            BuildGameUi();
            EggCatcherViewport.Ensure(gameCanvas);
            for (int i = 0; i < 24; i++)
            {
                var renderer = MakeSprite("Falling Item", itemSprites[0], 10, .45f);
                renderer.gameObject.SetActive(false);
                pool.Push(new FallingItem { Renderer = renderer });
            }
            app.SettingsChanged += RefreshPauseSettings;
            initialized = true;
            gameObject.SetActive(false);
        }

        public void StartRound()
        {
            if (!initialized) throw new InvalidOperationException("Initialize gameplay from the menu before starting.");
            audioPlayer.CancelResult();
            ClearWorld();
            gameObject.SetActive(true);
            itemRoot.gameObject.SetActive(true);
            player.enabled = true;
            Ui("TopPanel").gameObject.SetActive(true);
            var profile = app.Profile;
            previousBest = profile.BestScore;
            skin = Array.Find(skins, candidate => candidate.Id == profile.CurrentSkinId) ?? skins[0];
            player.sprite = skin.Stand;
            backdrop.sprite = app.SelectedBackgroundSprite;
            FitWorld();
            targetX = 0;
            player.transform.position = new Vector3(0, groundY, 0);
            player.color = Color.white;
            playerScale = Vector3.one * (1.75f / Math.Max(.1f, skin.Stand.bounds.size.y));
            player.transform.localScale = playerScale;
            Round = new EggCatcherRound(tuning);
            spawnTimer = .2f;
            hazardTimer = 2.4f;
            powerTimer = 7;
            animationClock = damageFlash = catchBounce = 0;
            lastCount = -1;
            resultShown = false;
            input.Release();
            HideOverlays();
            countdownPanel.SetActive(true);
            audioPlayer.Duck(false);
            UpdateHud();
            AnimateWorld(0, false);
        }

        private void FitWorld()
        {
            lastViewportSize = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;

            worldCamera.orthographic = true;
            worldCamera.rect = EggCatcherViewport.CalculateCameraRect(lastViewportSize);
            var cameraPixels = worldCamera.pixelRect;
            if (cameraPixels.height > 0) worldCamera.aspect = cameraPixels.width / cameraPixels.height;
            worldCamera.orthographicSize = 5;
            worldCamera.transform.position = new Vector3(0, 0, -10);
            halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float scale = Mathf.Max(halfWidth * 2 / backdrop.sprite.bounds.size.x, 10 / backdrop.sprite.bounds.size.y);
            backdrop.transform.localScale = Vector3.one * scale;
            backdrop.transform.position = Vector3.zero;
            float backgroundHeight = backdrop.sprite.bounds.size.y * scale;
            truckY = backgroundHeight * .161f + .48f;
            fallStartY = backgroundHeight * .135f;
            crowY = Mathf.Min(3.2f, truckY + 1);
            balloonY = crowY;
            float safeBottom = worldCamera.ScreenToWorldPoint(new Vector3(0, Screen.safeArea.yMin, 10)).y;
            groundY = Mathf.Max(-3.6f, safeBottom + 1);
        }

        private void Update()
        {
            if (Round == null) return;
            if (lastViewportSize != new Vector2Int(Screen.width, Screen.height) || lastSafeArea != Screen.safeArea)
            {
                FitWorld();
                float limit = Mathf.Max(.1f, halfWidth - .7f);
                targetX = Mathf.Clamp(targetX, -limit, limit);
                var position = player.transform.position;
                position.x = Mathf.Clamp(position.x, -limit, limit);
                position.y = groundY;
                player.transform.position = position;
                AnimateWorld(0, false);
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Round.Phase == RoundPhase.Paused && (restartConfirm.activeSelf || quitConfirm.activeSelf)) ClearConfirmation();
                else if (Round.Phase == RoundPhase.Paused) ResumeRound(); else PauseRound();
            }
            var before = Round.Phase;
            float previousMagnet = Round.MagnetRemaining;
            bool wasFrenzy = Round.IsFrenzy;
            Round.Tick(dt);
            if (wasFrenzy && !Round.IsFrenzy) audioPlayer.Play(EggSound.FrenzyEnd);
            if (previousMagnet > 0 && Round.MagnetRemaining <= 0) audioPlayer.Play(EggSound.MagnetEnd);
            if (Round.Phase == RoundPhase.Countdown)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(Round.CountdownRemaining));
                countdownText.text = count.ToString();
                if (count != lastCount) { lastCount = count; audioPlayer.Play(EggSound.Tick); }
                float pulse = 1 + Mathf.Repeat(Round.CountdownRemaining, 1) * .14f;
                countdownText.transform.localScale = Vector3.one * pulse;
            }
            if (before == RoundPhase.Countdown && Round.Phase == RoundPhase.Running)
            {
                countdownPanel.SetActive(false);
                audioPlayer.Play(EggSound.Go);
                Pop("GO!", new Vector3(0, 0, 0), new Color32(255, 211, 49, 255));
            }
            if (Round.Phase == RoundPhase.Running)
            {
                animationClock += dt;
                float oldX = player.transform.position.x;
                ReadMovement(dt);
                AnimateWorld(dt, Mathf.Abs(oldX - player.transform.position.x) > .003f);
                SpawnAndMove(dt);
                if (Round.Phase == RoundPhase.GameOver) EndRound();
            }
            if (Round.Phase != RoundPhase.Paused)
            {
                TickEffects(dt);
                damageFlash = Mathf.Max(0, damageFlash - dt);
                damageOverlay.color = new Color(1, .04f, .02f, damageFlash * .55f);
            }
            UpdateHud();
        }

        private void ReadMovement(float dt)
        {
            float axis = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null)
                axis = (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0) -
                       (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0);
            if (input.Held) targetX = worldCamera.ScreenToWorldPoint(new Vector3(input.ScreenPosition.x, 0, 10)).x;
            if (axis != 0) targetX = player.transform.position.x + axis * 2;
            float limit = Mathf.Max(.1f, halfWidth - .7f);
            targetX = Mathf.Clamp(targetX, -limit, limit);
            var position = player.transform.position;
            float next = Mathf.MoveTowards(position.x, targetX, Mathf.Max(1, tuning.PlayerSpeed) * dt);
            if (Mathf.Abs(next - position.x) > .003f) player.sprite = next < position.x ? skin.Left : skin.Right;
            else player.sprite = skin.Stand;
            position.x = next;
            position.y = groundY;
            player.transform.position = position;
        }

        private void AnimateWorld(float dt, bool moving)
        {
            float t = animationClock;
            truck.transform.position = new Vector3(Mathf.Sin(t * .8f) * Mathf.Max(.2f, halfWidth - 1), truckY + Mathf.Sin(t * 12) * .012f, 0);
            crow.transform.position = new Vector3(Mathf.Sin(t * .63f + 1.4f) * (halfWidth - .48f), crowY + Mathf.Sin(t * 3) * .09f, 0);
            crow.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 6) * 7);
            crow.flipX = Mathf.Cos(t * .63f + 1.4f) < 0;
            balloon.transform.position = new Vector3(Mathf.Sin(t * .32f - 1) * (halfWidth - .45f), balloonY + Mathf.Sin(t * 1.7f) * .1f, 0);
            balloon.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.2f) * 6);
            for (int i = 0; i < clouds.Count; i++)
                clouds[i].transform.position = new Vector3(Mathf.Repeat(t * .13f + i * 1.7f, halfWidth * 2 + 2) - halfWidth - 1, .5f - i * .7f, 0);
            catchBounce = Mathf.Max(0, catchBounce - dt * 5);
            float bounce = moving ? Mathf.Abs(Mathf.Sin(t * 17)) * .07f : Mathf.Sin(t * 2.6f) * .015f;
            var position = player.transform.position;
            position.y = groundY + bounce;
            player.transform.position = position;
            player.transform.localScale = Vector3.Scale(playerScale, new Vector3(1 + catchBounce * .1f - bounce * .15f, 1 - catchBounce * .08f + bounce * .25f, 1));
            player.transform.localRotation = Quaternion.Euler(0, 0, moving ? Mathf.Sin(t * 17) * 3 : 0);
            if (Round.InvulnerableRemaining > 0)
            {
                player.sprite = skin.Hurt;
                player.color = new Color(1, 1, 1, Mathf.Sin(t * 38) > 0 ? .42f : 1);
            }
            else player.color = Color.white;
        }

        private void SpawnAndMove(float dt)
        {
            float difficulty = Mathf.Clamp01((float)Round.Elapsed / Mathf.Max(1, tuning.DifficultyRampSeconds));
            spawnTimer -= dt;
            hazardTimer -= dt;
            powerTimer -= dt;
            if (spawnTimer <= 0)
            {
                float choice = UnityEngine.Random.value;
                SpawnItem(Round.IsFrenzy || choice < .73f ? EggItemKind.Egg : choice < .91f ? EggItemKind.Corn : EggItemKind.Worm,
                    new Vector2(truck.transform.position.x + UnityEngine.Random.Range(-.45f, .45f), fallStartY));
                spawnTimer = Round.IsFrenzy ? .2f : Mathf.Max(.15f, Mathf.Lerp(tuning.FirstSpawnInterval, tuning.FastestSpawnInterval, difficulty));
            }
            if (hazardTimer <= 0 && !Round.IsFrenzy)
            {
                var hazard = (EggItemKind)UnityEngine.Random.Range((int)EggItemKind.Rock, (int)EggItemKind.Bomb + 1);
                SpawnItem(hazard, crow.transform.position - Vector3.up * .3f);
                hazardTimer = Mathf.Lerp(2.4f, .95f, difficulty) * UnityEngine.Random.Range(.8f, 1.2f);
            }
            if (powerTimer <= 0)
            {
                SpawnItem(UnityEngine.Random.value < (Round.Lives < 3 ? .6f : .35f) ? EggItemKind.Heart : EggItemKind.Magnet,
                    balloon.transform.position - Vector3.up * .4f);
                powerTimer = UnityEngine.Random.Range(8, 12);
            }
            float speed = Mathf.Max(.5f, Mathf.Lerp(tuning.FirstFallSpeed, tuning.FastestFallSpeed, difficulty));
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                Vector3 previous = item.Renderer.transform.position;
                var position = previous;
                position.y -= speed * item.SpeedFactor * dt;
                if (item.Kind == EggItemKind.Egg && Round.MagnetRemaining > 0 && position.y < groundY + 3.5f)
                    position.x = Mathf.MoveTowards(position.x, player.transform.position.x, 5 * dt);
                item.Renderer.transform.position = position;
                item.Renderer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(animationClock * 3 + item.Phase) * 12);

                float catchY = groundY + .35f;
                if (position.y <= catchY + .26f && previous.y >= catchY - .25f &&
                    Mathf.Abs(position.x - player.transform.position.x) < .57f)
                {
                    HandleCatch(item.Kind, position);
                    Recycle(i);
                    if (Round.Phase == RoundPhase.GameOver) break;
                }
                else if (position.y < groundY - .9f)
                {
                    int previousMultiplier = Round.Multiplier;
                    Round.Miss(item.Kind);
                    if (previousMultiplier > 1 && Round.Multiplier == 1) audioPlayer.Play(EggSound.StreakLost);
                    Recycle(i);
                }
            }
        }

        private void SpawnItem(EggItemKind kind, Vector2 position)
        {
            if (items.Count >= 48) return;
            FallingItem item = pool.Count > 0 ? pool.Pop() : new FallingItem { Renderer = MakeSprite("Falling Item", itemSprites[(int)kind], 10, .45f) };
            item.Kind = kind;
            item.Phase = UnityEngine.Random.Range(0, 6.28f);
            item.SpeedFactor = UnityEngine.Random.Range(.9f, 1.1f);
            item.Renderer.sprite = itemSprites[(int)kind];
            float height = kind == EggItemKind.Heart || kind == EggItemKind.Magnet ? .54f : .43f;
            item.Renderer.transform.localScale = Vector3.one * (height / item.Renderer.sprite.bounds.size.y);
            position.x = Mathf.Clamp(position.x, -halfWidth + .2f, halfWidth - .2f);
            item.Renderer.transform.position = position;
            item.Renderer.gameObject.SetActive(true);
            items.Add(item);
        }

        private void HandleCatch(EggItemKind kind, Vector3 position)
        {
            int previousMultiplier = Round.Multiplier;
            var result = Round.Catch(kind);
            if (!result.Accepted) return;
            bool hurt = result.LifeChange < 0;
            var colour = hurt ? new Color32(255, 70, 65, 255) : new Color32(255, 210, 37, 255);
            string message = result.ShieldBlocked ? "Blocked!" : result.ShieldGained ? "Shield!" :
                result.Points > 0 ? "+" + result.Points : hurt ? "−1" : kind == EggItemKind.Heart ?
                (result.LifeChange > 0 ? "+1 life" : "Shield ready") : "Magnet!";
            Pop(message, position, colour);
            EmitBurst(position, kind, hurt);
            if (hurt)
            {
                damageFlash = .5f;
                if (app.HapticsEnabled && Application.isMobilePlatform) Handheld.Vibrate();
            }
            else catchBounce = 1;
            EggSound sound = result.ShieldBlocked ? EggSound.ShieldBlock : result.ShieldGained ? EggSound.Shield :
                hurt ? (kind == EggItemKind.Bomb ? EggSound.Bomb : EggSound.Hurt) :
                kind == EggItemKind.Corn ? EggSound.Corn : kind == EggItemKind.Worm ? EggSound.Worm :
                kind == EggItemKind.Heart ? EggSound.Heart : kind == EggItemKind.Magnet ? EggSound.Magnet : EggSound.Catch;
            audioPlayer.Play(sound);
            if (Round.Multiplier > previousMultiplier) audioPlayer.Play(EggSound.Combo);
            if (result.FrenzyStarted)
            {
                audioPlayer.Play(EggSound.Frenzy);
                Pop("EGG RUSH!  ×2", new Vector3(0, .1f, 0), new Color32(255, 218, 46, 255));
                spawnTimer = 0;
            }
            if (result.MissionReward > 0)
            {
                audioPlayer.Play(EggSound.Mission);
                Pop("Mission! +" + result.MissionReward + " eggs", new Vector3(0, 1, 0), new Color32(139, 255, 94, 255));
            }
        }

        public void PauseRound()
        {
            if (Round == null || !Round.Pause()) return;
            input.Release();
            targetX = player.transform.position.x;
            countdownPanel.SetActive(false);
            pausePanel.SetActive(true);
            RefreshPauseSettings();
            audioPlayer.Duck(true);
            audioPlayer.PlayUi(EggSound.Pause);
        }

        public void ResumeRound()
        {
            if (Round == null || !Round.Resume()) return;
            HideOverlays();
            countdownPanel.SetActive(true);
            lastCount = -1;
            input.Release();
            audioPlayer.Duck(false);
            audioPlayer.PlayUi(EggSound.Resume);
        }

        public void ReturnToMenu()
        {
            Round?.Abandon();
            audioPlayer.CancelResult();
            ClearWorld();
            input.Release();
            gameObject.SetActive(false);
            audioPlayer.Duck(false);
            app.ReturnFromGame();
            audioPlayer.PlayUi(EggSound.Back);
        }

        private void EndRound()
        {
            if (resultShown) return;
            resultShown = true;
            player.sprite = skin.Hurt;
            player.color = Color.white;
            player.transform.localRotation = Quaternion.identity;
            input.Release();
            app.SaveRound(Round);
            Ui("TopPanel").gameObject.SetActive(false);
            itemRoot.gameObject.SetActive(false);
            player.enabled = false;
            ShowResult(Round.Score > previousBest);
            audioPlayer.PlayResult(Round.Score > previousBest);
            audioPlayer.Duck(true);
        }

        private void Recycle(int index)
        {
            var item = items[index];
            item.Renderer.gameObject.SetActive(false);
            pool.Push(item);
            items.RemoveAt(index);
        }
        private void ClearWorld()
        {
            for (int i = items.Count - 1; i >= 0; i--) Recycle(i);
            foreach (var burst in bursts) if (burst.Renderer != null) Destroy(burst.Renderer.gameObject);
            bursts.Clear();
            foreach (var text in floaters) if (text.Label != null) Destroy(text.Label.gameObject);
            floaters.Clear();
        }
        private SpriteRenderer MakeSprite(string objectName, Sprite sprite, int order, float width)
        {
            var go = new GameObject(objectName, typeof(SpriteRenderer));
            go.transform.SetParent(itemRoot != null ? itemRoot : transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = player.sharedMaterial;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            go.transform.localScale = Vector3.one * (width / Mathf.Max(.01f, sprite.bounds.size.x));
            return renderer;
        }
        private void OnApplicationPause(bool paused) { if (paused) PauseRound(); }
        private void OnDisable()
        {
            if (worldCamera == null) return;
            worldCamera.rect = new Rect(0, 0, 1, 1);
            worldCamera.ResetAspect();
        }
        private void OnApplicationFocus(bool focused)
        {
#if !UNITY_EDITOR
            if (!focused) PauseRound();
#endif
        }
        private void OnDestroy() { if (app != null) app.SettingsChanged -= RefreshPauseSettings; }
        private sealed class FallingItem { public EggItemKind Kind; public SpriteRenderer Renderer; public float SpeedFactor, Phase; }
    }

    public sealed class EggCatcherGameInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Pointed;
        private int pointerId = int.MinValue;
        public bool Held { get; private set; }
        public Vector2 ScreenPosition { get; private set; }
        public void OnPointerDown(PointerEventData data)
        {
            if (Held) return;
            pointerId = data.pointerId; Held = true; ScreenPosition = data.position;
            Pointed?.Invoke(ScreenPosition);
        }
        public void OnDrag(PointerEventData data)
        {
            if (!Held || pointerId != data.pointerId) return;
            ScreenPosition = data.position; Pointed?.Invoke(ScreenPosition);
        }
        public void OnPointerUp(PointerEventData data) { if (pointerId == data.pointerId) Release(); }
        public void Release() { Held = false; pointerId = int.MinValue; }
        private void OnDisable() => Release();
    }
}
