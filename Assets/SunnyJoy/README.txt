SunnyJoy — playable prototype

Start: open Assets/Scenes/MainMenu.unity and press Play.
The first launch shows the loading screen and profile form. Enter a name
(2–12 characters) and choose an unlocked avatar. Start opens the main menu.
Later launches load the saved profile directly into the menu.

Connected screens
- Loading, create profile, main menu, player profile, edit profile and stats.
- Save/Cancel editing; local photo import, circular crop, drag, zoom and removal.
- Reset progress requires confirmation and affects only SunnyJoy data.
- The authored Settings and How to play screens are connected, including
  back navigation and Got it. Settings saves Sound/Music/Haptics switches.
  New profiles start with Music and Sounds on, Vibration off. The music,
  effects and damage vibration use these settings immediately, including in pause.
- Skins opens the authored shop from the menu or Profile > Current look.
  Both tabs, selection highlights, previews and currency are connected.
  Classic/Chicka/Pirate and Farm by Day/Sunset are initially available.
  Ninja costs 500 eggs, Astronaut 1,200, Winter 800. These are local earned
  eggs, not real-money purchases. Confirmation, cancellation, insufficient
  eggs, persistence and protection from double charging are implemented.
  Golden unlocks at best score 2,000; Night Farm unlocks at level 12.
  Avatar choice and equipped game character are independent.
- Play starts the authored game, with countdown, pause, restart/quit confirmation,
  game over and new-best results. PlayRequested remains a notification event.
- Stats starts empty and updates after completed rounds.
- The two locked avatars require level 10 / 15.

Testing
Tools > SunnyJoy > Preview UI (Separate Test Profile) uses an isolated
UiPreview save. Ordinary Play uses the real local profile. Stop Play Mode
to leave preview. It does not change the normal player profile.
Tools > SunnyJoy > Preview Shop (2480 Test Eggs) creates a separate
ShopPreview save with sample statistics and 2,480 eggs for testing purchases.
Every invocation resets only that preview slot; normal player data is unchanged.
Tools > SunnyJoy > Preview Game (Separate Test Profile) starts a round
immediately in the separate GameplayPreview profile. Its earned progress
persists between previews; the normal player profile is untouched.
Tools > SunnyJoy > Run UI and Data Tests checks data validation,
persistence, shop prices/locks, and a full UI flow including photo crop/save,
settings, shop purchases/cancellation/insufficient eggs and progress reset.
Game checks cover collision, pooling, countdown, all item values, pause/resume,
magnet duration, damage immunity, restart/quit, results and single-award saves.
Audio checks cover all generated effects, actual UI/game event routing,
dynamic dialog buttons, mute, result sequencing and fast-restart cancellation.
HUD checks cover mixed full/empty hearts and restoring a lost heart.
Save the current scene and stop Play Mode before running tests.
Result: Library/SunnyJoyTestResults.txt.

Game controls and initial balance
- Touch/hold/drag on the playfield to move toward that position. In Editor,
  A/D or Left/Right also work. Pause uses the upper-right button or Escape.
- 3 lives. Egg: +1 score and +1 spendable egg; corn: +5 score; worm: +10.
- Heart restores one life (maximum 3). At full health it grants a non-stacking
  shield which absorbs one hazard without losing life or the catch streak.
  Magnet attracts eggs for 8 seconds.
- Catch 20 foods without a miss to start Egg Rush: 6 seconds of double points,
  faster egg drops and no new crow hazards. Existing hazards remain dangerous.
  Taking damage ends the rush; pause/countdown freeze its timer.
- Each round has three missions: 20 eggs (+10 eggs), 5 corn (+15 eggs),
  3 worms (+20 eggs). Missions progress concurrently and pay once at game over.
  Restart and Quit forfeit uncommitted rewards. Actual egg statistics exclude bonuses.
  How to play > More tips explains the additional rules.
- Rock, rotten apple and bomb remove one life. Damage has 1.1 seconds of
  immunity so overlapping hazards cannot consume all lives in a single frame.
- Every 10 consecutive food catches increases the score multiplier, up to x5.
  Missing food or taking damage resets the streak/multiplier. Dodging hazards
  does not break it. These are initial balancing choices, not final tuning.
- Food falls from the moving truck; the crow drops hazards; the balloon drops
  hearts/magnets. Speed and spawn rate increase gradually over 150 seconds.
- Game Over credits actual eggs plus mission rewards, score, time and best streak once. Restart
  and Quit discard the unfinished round, as the confirmation dialog says.
- Score also gives XP: 500 XP for level 1, then +50 XP required per level.
  This unlocks the existing level/score rewards and feeds profile statistics.
- Tune values on Game > SunnyJoyGame > Tuning in the Inspector.
- Runs use the equipped skin/background. All six skins have stand, run-left,
  run-right and hurt poses, with bob/squash/blink animation. Falling items,
  drifting clouds, floating catch labels, particles and damage flashes animate.
- SunnyJoyAudio synthesizes an original eight-bar toy-instrument loop and
  short effects locally. No audio downloads, network calls or third-party
  music licenses are needed. Music and effects have independent switches.
  Distinct feedback covers egg/corn/worm/heart/magnet pickup, damage, bombs,
  countdown/start, combo upgrades/loss, magnet expiry, pause/resume, buttons,
  avatar/skin/background selection, purchase, insufficient eggs, save and toggles.
  The final impact is followed by the loss sting, then a new-record fanfare
  when earned. Restart/menu/mute cancels pending result sounds. Semantic UI
  sounds replace the generic click; dynamic dialogs also receive feedback.
  Sounds are louder than the background music and use separate UI/game/result
  sources. Respect saved Sounds settings; enable Sounds in Settings to hear them.
- HUD hearts preserve artwork proportions with fixed spacing. The tight gray
  sprite is compensated for the red sprite's glow/padding, so losing a life
  does not enlarge or stretch a heart. Source gameplay artwork is unchanged.
- Pause freezes simulation and power-up timers. Resume gives a new countdown.
  iOS backgrounding/loss of focus pauses automatically; it never auto-resumes.

iOS
The game fits foreground UI uniformly inside the device safe area in both
orientations. Menus and the playfield share a centered viewport. Tablets use
a wider playfield, while wide screens retain a central portrait playfield.
Tools > SunnyJoy > Run Screen Adaptation Tests checks the authored controls
on small phones, notched phones, tablets and landscape displays.
Capture Screen Adaptation Preview saves layout renders to Library/ScreenAdaptation.
The focused name field moves above the software keyboard when needed.
Keyboard movement and crop dragging include the safe-area content scale.
The native camera/photo picker is in Assets/Plugins/iOS. Permission prompts
are requested only when the player selects a photo source. Photos remain
in the app's private local storage. The postbuild step adds missing camera
and photo descriptions, AVFoundation and the Objective-C ARC flag.
Tools > SunnyJoy > Export iOS Xcode Project writes Builds/iOS/SunnyJoy.
Open Unity-iPhone.xcodeproj in Xcode to sign a device or App Store build.

Integration notes
The controller uses the existing MenuCanvas and Game/GameCanvas hierarchies
and original artwork. Only MenuCanvas owns SunnyJoyApp; Game owns gameplay.
If rebuilding the scene wiring: Tools > SunnyJoy > Connect UI Template.
Use the Preview UI/Game/Shop menu items for isolated local profiles.
The game prepares the local profile, audio and object pool, then opens the
profile creation screen or main menu. Existing installs keep their save key.
SunnyJoyRound contains deterministic rules and one-time progress commit;
SunnyJoyGame handles world/input/animation and the authored gameplay UI.
The game avoids global Time.timeScale changes and reuses falling items.
Do not rename bound scene elements without updating the controller paths.
ASSETS AND MOBILE BUILD SIZE
Images under Assets/0_UI and Assets/0_Sprites have role-based names.
Players are grouped by skin; animation frames use Idle/RunLeft/RunRight/Hurt.
Reports/asset-renames.tsv records the old/new paths and preserved GUIDs.
Android and iOS use ASTC 4x4 for UI/characters, ASTC 6x6 for scenery, no mipmaps
or CPU-readable copy, and a 512-pixel cap for the 400-pixel character art.
Full-screen and baked-text artwork keeps up to 2048 pixels.
Mobile builds use Medium managed stripping and IL2CPP size-oriented generation.
The native photo callback is preserved.
Tools > SunnyJoy > Build Optimized Android APK writes a non-development
ARM64 APK with LZ4HC data compression to Builds/Android/SunnyJoy-optimized.apk.
Compare the actual APK size, and check text clarity and photo
callbacks on devices before release. Editor texture memory is not APK size.
Backups/before-game-expansion-20261013.tar.gz retains the prior game/code/art settings.
