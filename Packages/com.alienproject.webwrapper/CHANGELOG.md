# Changelog

## [0.6.0] - 2026-10-13

- Added WebWrapperApp: one scene component for per-app identifiers and service configuration.
- Generate clean runtime settings before Play/Build, preserving early native SDK registration.
- Firebase DB is the new default; no sample endpoints/offer URLs or mock mode in app setup.
- Added scene/root validation, optional game/loading roots and missing-SDK/Firebase build guards.
- Removed fixed host scene, app name and signing overrides from reusable Android build helpers.
- Android WebView now fills the viewport without an extra Unity safe-area crop.
- Added portable notification Activity metadata for Firebase's Android Activity.
- Existing explicit HttpConfig installations remain compatible; scene-based setup uses Firebase DB.

## [0.5.0] - 2026-10-09

- Fixed AppsFlyer 7 native RPC event delivery and main-thread handling of SDK callbacks.
- Restored actual FCM registration tokens for the backend contract instead of sending FIDs.
- Added late/rotated token registration refresh with durable acknowledgement and background retry;
  saved game installations continue to avoid new config calls.
- Serialized callback delivery and merged installation-state changes without discarding unrelated
  push queues, permission choices, or URL data.
- Made push navigation independent of callback delivery and prioritized clicked URLs during startup.
- Bounded SDK, startup, and page waits, with cached/offline/retry behavior and reconnection handling.
- Distinguished network-interface disconnection from backend DNS/TLS/timeout failures.
- Fixed Android Back/root behavior; enabled iOS gestures, redirect-limit recovery, protected-media
  handling, and User-Agent cleanup while preserving real platform versions.
- Added persistent Privacy Policy actions to the game and native WebView toolbar; empty URLs remain
  supported for local tests.
- Deferred iOS notification authorization until the custom flow and disabled automatic FCM startup.
- Added automatic iOS push/background capabilities and an embedded native image notification
  extension; APNs keys, Apple team, signing/provisioning, and device delivery remain external checks.
- Replaced substring Firebase preflight with exact, per-platform bundle-ID validation.
- Expanded regression coverage for SDK callback mapping, cancellation, Firebase configuration,
  push/token races, navigation, and generated iOS plist settings. Device build/performance checks
  are separate and are not implied by passing Edit Mode tests.

## [0.4.0] - 2026-10-08

- Added optional real UniWebView preview for mock URLs in the Unity Editor.
- Added deterministic macOS Editor WebView teardown across repeated Play Mode sessions.
- Validated application identifiers against both Firebase configuration files in preflight.
- Constrained UniWebView to the device Safe Area and refresh its frame after rotation/resume.
- Enabled inline/autoplay media, JavaScript, keyboard avoidance, downloads, and persistent sessions.
- Restricted the custom notification explanation to WebView mode and persisted native denial.
- Added a privacy-policy action to the notification prompt and production status screen.
- Added configurable responsive loading/offline/error UI with an animated loading indicator.
- Added Android build-time FCM default icon, accent color, channel, and FID metadata.
- Added iOS 18 setup, App Store ID normalization, and camera/photo purpose-string export.
- Expanded Edit Mode coverage to 22 tests, including generated Android FCM resources.

## [0.3.0] - 2026-10-08

- Added Firebase App and Messaging 13.17.0 integration using Firebase Installation IDs.
- Added Unity Mobile Notifications 2.4.3 permission handling for Android and iOS.
- Added a configurable custom notification-permission prompt with icon and image support.
- Added cold-start push buffering, payload mapping, one-time URL routing, and callback retries.
- Added Android and iOS FID build metadata automation.
- Added an optional reflection-based UniWebView adapter with full-screen resize and back handling.
- Updated EDM4U to 1.2.189 and resolved Firebase Android dependencies.
- Expanded Edit Mode coverage to 17 tests, including pending-push deduplication and optional WebView safety.

## [0.2.0] - 2026-10-08

- Added AppsFlyer Unity Plugin 7.0.3 integration using its asynchronous API.
- Added first-arrival-ordered conversion and Unified Deep Link payload collection.
- Added configurable AppsFlyer startup timeout and deep-link grace window.
- Added AppsFlyer and EDM4U production setup documentation and preflight checks.
- Made Android setup create and enable the Gradle templates required by EDM4U.
- Added a stable package menu command for EDM4U Android Force Resolve.

## [0.1.0] - 2026-10-08

- Added reusable UPM package structure.
- Added launch state machine and persistent installation mode.
- Added first-wins attribution payload merge.
- Added mock attribution, push, connectivity, and config services.
- Added config and notification interaction HTTP clients.
- Added notification prompt cooldown policy.
- Added Editor setup and preflight window.
- Added Edit Mode tests for the critical startup rules.
- Added production adapter registration and real config-client wiring.
- Added WebView routing, wrapper-scene routing, retry UI, and Android back handling.
- Added durable push-open callback queue and duplicate protection.
- Added notification permission-flow orchestration.
- Added documented Android Player Settings validation and setup command.
