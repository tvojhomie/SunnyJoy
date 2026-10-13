# Alien Project Web Wrapper

## One scene component

Add **Alien Project > Web Wrapper App** to a separate active object in the first build
scene, or use **Tools > Alien Project > Add Web Wrapper App to Scene**. Set the app name,
bundle ID, Firebase Database URL/node, AppsFlyer Dev Key, numeric Apple App ID (iOS),
optional OneLink host/URI scheme and Privacy Policy URL. No offer URL, PHP endpoint,
mock switch or development data is required in the Inspector.

`WebView Enabled` is the local master switch and defaults to on. Off always starts
the game, skips remote launch configuration and blocks push/late-response WebView routing
for that session; AppsFlyer, Firebase Messaging and game-only notification prompts remain
available. On permits Firebase's `enabled`/`url` decision, not a forced WebView route.
Change the switch outside Play Mode, save the scene and rebuild for device changes.
Remote `enabled: false` opens the game on the next cold launch without rebuilding.
The loading screen waits for the configuration response and the initial WebView page load,
even on a slow connection. There is no local startup deadline or automatic game route just
because loading is slow. An explicit game decision, offline state, failed request or native
page-load error still opens the game. Once the game route is committed, late navigation
does not replace its menu.

The Editor automatically generates the runtime Resources settings **before** native SDK
registration on Play/Build. Edit only the scene component, not the generated asset.
App name and bundle ID are applied to Player Settings. Existing SDK configuration files
are never rewritten to fake a different Firebase registration: download the correct files
for each app. Build checks reject missing SDKs, invalid settings, duplicate configuration,
missing bootstrap scene and mismatched Firebase files.

For a new host game, assign an initially inactive Game Root under the scene-binding foldout;
optionally assign a separate loading panel and a game-ready event. Put WebWrapperApp outside
both roots. Existing hosts can continue using WebWrapperLaunchState and their own loading UI.
The game root is only activated for the game route, so its UI and audio do not start behind WebView.

The package does not include app credentials, Firebase config files or licensed UniWebView.
Install the SDK dependencies below. See [Getting started](Documentation~/getting-started.md).

## Firebase Realtime Database configuration

FirebaseRealtimeDatabase is the scene component's fixed source. Supply the HTTPS Database URL and
Config Path (for example `apps/my_game/webview`). The node is a public read-only JSON
object: `{"enabled":true,"url":"https://example.com/"}`. The runtime uses the official
Firebase REST API; no Database SDK or admin credentials are embedded in the game.
Publish rules that allow reading this node but deny client writes; never use public
write access in a release. Use a separate config node/settings asset for each game.

This mode performs a fresh GET each cold launch, without waiting for attribution or
FCM. It ignores legacy PHP routing/cache and leaves game saves untouched. Disabled,
empty, invalid, offline or failed configuration opens the game for that session.
It never posts to `config.php`, including token sync/click callbacks. AppsFlyer and
Firebase Messaging can initialize independently. Push token storage and server-side
click acknowledgements need a separate backend; direct DB configuration is not that
backend. Existing push-open navigation/local deduplication is retained.

`Tools > Alien Project > Check Firebase DB Connection` tests the actual GET without
entering Play Mode or changing database/install state. `Test Real Server in Editor`
also supports the DB flow. Existing explicitly configured `HttpConfig` assets remain compatible;
its attribution-dependent protocol below is unchanged.

Reusable Unity startup orchestration for AppsFlyer attribution, a remote config endpoint,
Firebase Cloud Messaging, and a native WebView.

Version `0.6.0` contains the scene configuration, startup state machine, internal test fixtures,
an Editor setup window, tests, and adapters for AppsFlyer `7.0.3`, Firebase Messaging
`13.17.0`, and licensed UniWebView `6.10.0`. It targets Unity `6000.0` or later.
Third-party SDKs, app-specific settings, and platform configuration files are not included
in this package.

## Verify your connection

Open the startup scene and press Play. Scene-based setup uses the real Firebase URL;
on macOS the native Editor WebView is a separate window, on a device it overlays the app.
An unavailable external-app URL scheme does not dismiss an already loaded web page;
the native SDK handles installed external apps. HTTP/network/TLS failures still use
the normal game fallback. Initial loading without a usable web page is not treated as success.
AppsFlyer/FCM native attribution and permissions still require a device. Internal unit-test
fixtures are separate from app configuration and are not an onboarding requirement.

## Production SDK setup

The consuming project's `Packages/manifest.json` must pin these dependencies. Firebase's
official tarballs can be downloaded from the Google Unity package registry and referenced
from a project-local `GooglePackages` directory:

```json
"appsflyer-unity-plugin": "https://github.com/AppsFlyerSDK/appsflyer-unity-plugin.git#v7.0.3",
"com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=upm#v1.2.189",
"com.google.firebase.app": "file:../GooglePackages/com.google.firebase.app-13.17.0.tgz",
"com.google.firebase.messaging": "file:../GooglePackages/com.google.firebase.messaging-13.17.0.tgz",
"com.unity.mobile.notifications": "2.4.3"
```

Set the AppsFlyer Dev Key and, for iOS, the numeric App Store ID in
`WebWrapperApp`. The package creates and registers the AppsFlyer adapter automatically
on Android and iOS before the first scene starts.

Place `google-services.json` and/or `GoogleService-Info.plist` directly in `Assets`. Never
replace these with credentials from another Firebase project without approval. For Android,
run `Tools > Alien Project > Apply Documented Android Settings` once, then
`Tools > Alien Project > Resolve Android SDK Dependencies`.

Firebase registration sends an actual **FCM registration token** in `push_token`, together
with `firebase_project_id`. It uses the token APIs retained in Firebase `13.17.0`
(`TokenReceived` / `GetTokenAsync`), not a Firebase Installation ID. Build processors disable
FID mode. These token APIs are deprecated upstream; keep the SDK pinned until a future
upgrade is checked against the backend's token contract. A token arriving late or rotating
is sent in a new config request for WebView installations, with retries after connectivity
returns or the app resumes. Saved game/wrapper installations do not issue new config requests.

The package shows its own notification explanation before the system dialog. Prompt text,
buttons, icon, image, Android channel, timeout, and the three-day default retry cooldown are
configured in `WebWrapperSettings`. A push opened by the user is recorded before navigation.
Its interaction callback runs independently and retries durably; callback delivery never
holds up the clicked page. A push URL takes precedence over the ordinary launch URL for
that session and never overwrites the persisted config URL. A successful token-registration
response may refresh the cached config URL for a future launch without replacing the open page.

The custom prompt is only eligible after the host game's menu is visible. Call
`WebWrapperLaunchState.NotifyGameReady()` after showing that UI; do not call it
from the loading screen. WebView launches open directly without a notification
prompt. The game route is published before the prompt so initialization is never
blocked by it; dismissing the prompt does not reload the game. Choosing **Not now** defers it; rejecting
the native system prompt suppresses it permanently for that installation. Android builds receive
a package-provided white notification icon, configurable accent color, default FCM channel, and
FCM-token metadata during Gradle generation. FCM `notification.image` remains available for rich images.

The supplied adapters register before the first scene on supported devices. A custom
implementation can instead register through:

```csharp
WebWrapperServices.WebViewAdapter = uniWebViewAdapter;
```

Firebase providers register automatically on Android and iOS. Manual registration remains
available through `WebWrapperServices` for a custom messaging implementation. The bundled
UniWebView adapter discovers the paid SDK at runtime, enables inline/autoplay media, JavaScript,
keyboard avoidance, cookie/session persistence and downloads, fills Android's viewport
(iOS retains safe-area bounds) after rotation or resume, and handles Android back navigation without closing the
view at the root. It enables iOS back/forward gestures, retries a redirect-limit failure once
at its last URL, and grants protected-media-only requests while retaining native prompts for
camera/microphone requests. Its User-Agent normalization preserves device/OS/engine versions
and does not append the app name or bundle ID. The package still compiles when UniWebView is
absent; install the licensed SDK before a production build. Internal media-permission bridging
is specific to UniWebView 6 and falls back to native prompts if an SDK upgrade changes that API.

The optional Editor preview also disposes the native macOS WebView synchronously when Play Mode
ends, so repeated Play Mode sessions do not leave callbacks attached across Unity domain reloads.

For iOS, run **Apply Documented iOS Settings**. It sets the iOS 18.0 deployment target.
Build processing disables automatic notification authorization, adds Push Notifications and
Remote notifications background mode, and writes camera/photo-library usage descriptions.
The numeric App Store ID is sent to config with the required `id` prefix.

iOS export also creates and embeds `WebWrapperNotificationService`, with bundle ID
`<app bundle ID>.notificationservice`, to download notification images from `fcm_options.image`.
The extension uses native UserNotifications; no extra Firebase pod is required. Image pushes
need `aps.mutable-content = 1` and an HTTPS image URL. Sign **both** the app and extension with
your Apple team; manual signing requires a separate extension provisioning profile. Enabling
Push Notifications for the Apple App ID and uploading an APNs key to Firebase remain external
setup steps. Choose the correct APS development/production environment in Mobile Notifications
settings for your signing profile. Local preflight cannot verify APNs credentials or delivery.

The privacy-policy URL is available for the host game's own settings panel. The wrapper does
not inject Privacy Policy buttons over the game, into notification prompts or into WebView.
The native toolbar is hidden in release builds; development diagnostics retain the Logs action.

Loading/logo/background appearance and notification text are configurable. Production startup
errors, offline conditions and failed WebView loads now return to the game without error/retry UI.
The loading overlay and notification prompt use `Screen.safeArea`. Reset-install-state controls
are only shown by the mock harness.

Startup and page loading have bounded waits with a session-only game fallback. A transport/SDK/
page failure does not permanently assign game mode or erase a cached WebView URL. Only an explicit
negative server decision retains the existing permanent game assignment. Network-interface reachability
distinguishes a disconnected device from an endpoint error; it does not prove that a connected
Wi-Fi network has working internet. Device tests are still required for captive/offline networks.

SDK adapters use `AlwaysLinkAssembly`, and `WebWrapperLinkerProcessor` supplies the package's
native callback/reflection roots to UnityLinker explicitly. Do not rely on link.xml discovery
inside UPM packages. Android IL2CPP builds verify the presence of SDK/adapter assemblies after
managed stripping. Test-console initialization is explicit and idempotent; IMGUI must not cover it.

Scene-generated settings always disable mock services and SDK debug logging. Native attribution/messaging adapters do
not run in the Unity Editor. Editor Play Mode and Edit Mode tests do not validate APNs, attribution,
native permissions, final package size, 16 KB Android compatibility, or measured startup time.

See [Getting started](Documentation~/getting-started.md) for install, export, and device checks.

AppsFlyer project timezone and currency still have to be configured as Moscow (UTC+3) and
USD in the AppsFlyer dashboard; the runtime adapter also sets currency to USD.
