# Install in another game

1. Install `com.alienproject.webwrapper-0.6.0.tgz` through Unity Package Manager:
   **+ > Install package from tarball**. Or copy this embedded package folder into `Packages`.
2. Install AppsFlyer Unity 7.0.3, Firebase App/Messaging 13.17.0, EDM4U 1.2.189 and your
   licensed UniWebView 6.10.0. Mobile Notifications is a package dependency. See README
   for pinned sources. Third-party SDK downloads/paid assets are not bundled here.
3. Download **this app's** `google-services.json` (Android) / `GoogleService-Info.plist`
   (iOS) from Firebase into Assets. Keep exactly one applicable configuration file.
4. Save the startup scene, add it first in Build Profiles, then add one active
   **Alien Project > Web Wrapper App** on a separate root object.
5. Set app name, bundle ID, Firebase Database root/node, AppsFlyer Dev Key and numeric
   Apple App ID (iOS). Optional: OneLink host, iOS URI scheme and Privacy Policy URL.
   No URL for the actual web page is stored on the component.
6. In Firebase Console, configure the node with `enabled` (boolean) and `url` (HTTPS).
   Allow client reads of that node, but deny public writes.
7. Under **Scene bindings**, assign an initially inactive Game Root containing game
   UI, logic and audio. Keep WebWrapperApp outside it. Optionally assign a separate
   loading panel. Game Root activates only on a game/fallback decision.
   Hosts already integrated with `WebWrapperLaunchState` can leave these fields empty.
8. When installing SDKs into a new Android project, run **Apply Documented Android
   Settings** and **Resolve Android SDK Dependencies** once. Resolve import errors.
   Firebase uses MessagingUnityPlayerActivity (not GameActivity). For iOS run
   **Apply Documented iOS Settings** and configure Apple Team/APNs/Universal Links.
9. Press Play or build. Runtime settings are regenerated before SDK registration.
   Do not edit the generated Resources asset. Build validation reports missing SDKs/configs.

## Automatic setup

- Name and bundle ID synchronize to Player Settings.
- Real Firebase lookup on each launch, game fallback and WebView navigation.
- SDK registration receives current keys before the first scene; no mock/old endpoint.
- Android fullscreen/frame pacing and Firebase Activity selection during preparation.
- iOS notification extension, camera/photo descriptions and configured URL scheme/domains
  during export, using installed platform build support.

## External setup remains necessary

Firebase registrations/rules, AppsFlyer application/OneLink, Apple Team, APNs credentials,
signing and any service sending pushes must exist. Unity keys cannot create those resources.
Direct Firebase URL lookup does not provide server-side FCM token storage or click analytics;
those require a separate backend integration.

## Migrate this project

**Tools > Alien Project > Add Web Wrapper App to Scene** transfers current connection values
from the old settings asset. Save the scene. Old PHP and mock URLs are cleared from generated
settings; its GUID is retained. Existing game-specific loading/push UI remains.
Keep one setup component in the first scene; do not duplicate it in subsequent game scenes.

## Reuse boundary

The transferable artifact is the package, not only WebWrapperApp.cs: the component depends
on runtime/editor support and native SDKs. Copying one C# file cannot install native libraries
or perform Firebase/Apple dashboard setup. The distribution contains no Sunny Joy scene,
Firebase config files, saved settings, branded artwork or fixed offer URL.

## Verify

Check the Firebase connection, both enabled/disabled routes, offline fallback, separate game
audio, fullscreen/rotation, cookies, keyboard, file/camera uploads and real push delivery.
The macOS Editor opens a separate native WebView window; mobile builds overlay the app.
AppsFlyer attribution, permissions and APNs require target-device tests.
