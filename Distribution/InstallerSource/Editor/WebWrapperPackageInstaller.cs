using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AlienProject.WebWrapperInstaller
{
    // This assembly intentionally depends only on Unity Editor APIs. Importing the
    // installer does not require Firebase, AppsFlyer, Mobile Notifications, or the core.
    [InitializeOnLoad]
    public static class WebWrapperPackageInstaller
    {
        private const string MenuPath = "Tools/Alien Project/Install Web Wrapper Package";
        private const string SetupMenuPath = "Tools/Alien Project/Web Wrapper Setup";
        private const string PendingKey = "AlienProject.WebWrapperInstaller.0.6.0.Pending";
        private const string DeadlineKey = "AlienProject.WebWrapperInstaller.0.6.0.Deadline";
        private const string VendorImportKey = "AlienProject.WebWrapperInstaller.0.6.0.VendorImport";
        private const string VendorArchive = "UniWebView-6.10.0.unitypackage";
        // The distribution builder substitutes hashes of the approved local payloads.
        private const string VendorSha256 = "@@SHA_UniWebView@@";
        private const string CoreName = "com.alienproject.webwrapper";
        private const string SetupTypeName = "AlienProject.WebWrapper.Editor.WebWrapperSetupWindow";

        private static readonly Dependency[] Dependencies =
        {
            new Dependency(CoreName, "0.6.0", "com.alienproject.webwrapper-0.6.0.tgz", "@@SHA_com.alienproject.webwrapper@@"),
            new Dependency("com.google.firebase.app", "13.17.0", "com.google.firebase.app-13.17.0.tgz", "@@SHA_com.google.firebase.app@@"),
            new Dependency("com.google.firebase.messaging", "13.17.0", "com.google.firebase.messaging-13.17.0.tgz", "@@SHA_com.google.firebase.messaging@@"),
            new Dependency("appsflyer-unity-plugin", "7.0.3", "appsflyer-unity-plugin-7.0.3.tgz", "@@SHA_appsflyer-unity-plugin@@"),
            new Dependency("com.google.external-dependency-manager", "1.2.189", "com.google.external-dependency-manager-1.2.189.tgz", "@@SHA_com.google.external-dependency-manager@@"),
            new Dependency("com.unity.mobile.notifications", "2.4.3", "com.unity.mobile.notifications-2.4.3.tgz", "@@SHA_com.unity.mobile.notifications@@"),
            new Dependency("com.unity.ugui", "2.0.0", "com.unity.ugui-2.0.0.tgz", "@@SHA_com.unity.ugui@@"),
            new Dependency("com.unity.inputsystem", "1.18.0", "com.unity.inputsystem-1.18.0.tgz", "@@SHA_com.unity.inputsystem@@")
        };

        private static readonly string[] BuiltInModules = { "androidjni", "imgui", "jsonserialize",
            "unitywebrequest", "unitywebrequesttexture", "imageconversion", "audio", "ui", "uielements" };

        private static readonly Dictionary<string, string[]> RawSdkTypes = new Dictionary<string, string[]>
        {
            ["appsflyer-unity-plugin"] = new[] { "AppsFlyerSDK.AppsFlyer" },
            ["com.google.firebase.app"] = new[] { "Firebase.FirebaseApp" },
            ["com.google.firebase.messaging"] = new[] { "Firebase.Messaging.FirebaseMessaging" },
            ["com.google.external-dependency-manager"] = new[]
                { "GooglePlayServices.PlayServicesResolver", "Google.IOSResolver", "Google.JarResolver.Dependency" },
            ["com.unity.mobile.notifications"] = new[] { "Unity.Notifications.NotificationCenter" }
        };

        private enum Phase { Idle, Inspect, Add, WaitForEditor, Verify, ImportVendor, OpenSetup }
        private static Phase _phase;
        private static ListRequest _list;
        private static AddAndRemoveRequest _add;
        private static DateTime _deadline;
        private static DateTime _retryAfter;

        static WebWrapperPackageInstaller()
        {
            AssemblyReloadEvents.beforeAssemblyReload += EditorUtility.ClearProgressBar;
            EditorApplication.delayCall += ResumeAfterReload;
        }

        [MenuItem(MenuPath)]
        public static void Install()
        {
            if (_phase != Phase.Idle || SessionState.GetBool(PendingKey, false))
            {
                EditorUtility.DisplayDialog("Web Wrapper", "Installation is already being monitored. Check the Console and Package Manager.", "OK");
                return;
            }
            if (!int.TryParse(Application.unityVersion.Split('.')[0], out var major) || major < 6000)
            {
                EditorUtility.DisplayDialog("Unity version", "Web Wrapper 0.6.0 requires Unity 6000.0 or later.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Install Web Wrapper 0.6.0 — Full",
                    "Add the Web Wrapper UPM package and missing dependencies:\n\n" +
                    "Firebase App/Messaging 13.17.0\nAppsFlyer 7.0.3\nEDM4U 1.2.189\nMobile Notifications 2.4.3\n\n" +
                    "UniWebView 6.10.0, uGUI and Input System are included too. All SDK archives are local. " +
                    "Internet may still be needed for native Android/iOS build dependencies. Existing dependencies are retained; " +
                    "conflicting versions stop the installer. App name, bundle ID, scenes, and host settings are not changed. " +
                    "No keys or Firebase app configuration files are supplied. UniWebView is your licensed copy, for your own projects.",
                    "Install", "Cancel")) return;
            BeginInstallation();
        }

        // Used only by the distribution smoke project. It follows the exact same install path.
        public static void InstallForBatchValidation()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch validation only.");
            BeginInstallation();
        }

        private static void BeginInstallation()
        {
            try
            {
                File.WriteAllText("Library/WebWrapperInstaller-result.txt", "INSTALLING\n");
                _deadline = DateTime.UtcNow.AddMinutes(2);
                _phase = Phase.Inspect;
                _list = Client.List(true, true);
                StartPump();
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanInstall() => _phase == Phase.Idle &&
            !SessionState.GetBool(PendingKey, false) && !EditorApplication.isCompiling && !EditorApplication.isPlaying;

        private static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(PendingKey, false) || _phase != Phase.Idle) return;
            if (!long.TryParse(SessionState.GetString(DeadlineKey, ""), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var ticks))
            {
                Fail("The installation monitor could not restore its deadline. Inspect Window > Package Manager before retrying.");
                return;
            }
            _deadline = new DateTime(ticks, DateTimeKind.Utc);
            _phase = Phase.WaitForEditor;
            StartPump();
        }

        private static void StartPump()
        {
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        private static void Pump()
        {
            try
            {
                if (DateTime.UtcNow > _deadline)
                {
                    Fail("Stopped waiting for Package Manager or compilation. The underlying Unity package operation " +
                         "may still finish; inspect Window > Package Manager and the Console before retrying.");
                    return;
                }
                switch (_phase)
                {
                    case Phase.Inspect:
                        EditorUtility.DisplayProgressBar("Web Wrapper", "Checking existing package versions...", 0.1f);
                        if (!_list.IsCompleted) return;
                        if (_list.Status == StatusCode.Failure) { Fail(_list.Error?.message); return; }
                        PrepareInstallation(_list.Result.ToArray());
                        break;
                    case Phase.Add:
                        EditorUtility.DisplayProgressBar("Web Wrapper", "Installing dependencies through Unity Package Manager...", 0.5f);
                        if (!_add.IsCompleted) return;
                        if (_add.Status == StatusCode.Failure) { Fail(_add.Error?.message); return; }
                        _phase = Phase.WaitForEditor;
                        break;
                    case Phase.WaitForEditor:
                        if (EditorApplication.isCompiling || EditorApplication.isUpdating || DateTime.UtcNow < _retryAfter) return;
                        _list = Client.List(true, true);
                        _phase = Phase.Verify;
                        break;
                    case Phase.Verify:
                        if (!_list.IsCompleted) return;
                        if (_list.Status == StatusCode.Success && AllDependenciesResolved(_list.Result.ToArray()))
                        {
                            _phase = Phase.ImportVendor;
                        }
                        else
                        {
                            // A domain reload can finish while UPM is still applying the original
                            // operation. Verify it again; never start a second add operation.
                            _retryAfter = DateTime.UtcNow.AddSeconds(3);
                            _phase = Phase.WaitForEditor;
                        }
                        break;
                    case Phase.ImportVendor:
                        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                        if (VendorPresent()) { _phase = Phase.OpenSetup; break; }
                        if (SessionState.GetBool(VendorImportKey, false)) return;
                        ValidateVendorDestination();
                        SessionState.SetBool(VendorImportKey, true);
                        AssetDatabase.importPackageFailed += OnVendorImportFailed;
                        AssetDatabase.importPackageCancelled += OnVendorImportCancelled;
                        Debug.Log("[WebWrapper Installer] Importing bundled UniWebView 6.10.0 (no demo scenes).");
                        AssetDatabase.ImportPackage(Path.Combine(ProjectRoot, "WebWrapperPackages", VendorArchive), false);
                        break;
                    case Phase.OpenSetup:
                        EditorUtility.DisplayProgressBar("Web Wrapper", "Waiting for package compilation...", 0.9f);
                        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                        if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(SetupTypeName, false) != null)) return;
                        ValidateVendorDestination();
                        foreach (var sdk in new[] { "AppsFlyerSDK.AppsFlyer", "Firebase.Messaging.FirebaseMessaging", "Unity.Notifications.NotificationCenter" })
                            if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(sdk, false) != null))
                                throw new InvalidOperationException("Installed SDK did not compile: " + sdk);
                        Finish();
                        File.WriteAllText("Library/WebWrapperInstaller-result.txt", "SUCCESS: WebWrapper 0.6.0, all SDK packages and UniWebView are installed.\n");
                        Debug.Log("[WebWrapper Installer] Installed Web Wrapper 0.6.0 and all bundled SDKs. " +
                                  "Add Web Wrapper App to your saved first scene, fill this app's identifiers and key, " +
                                  "then add its Firebase files. No app-specific data was imported.");
                        if (!Application.isBatchMode && !EditorApplication.ExecuteMenuItem(SetupMenuPath))
                            Debug.LogWarning("[WebWrapper Installer] Open Tools > Alien Project > Web Wrapper Setup manually.");
                        break;
                }
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        private static void PrepareInstallation(PackageInfo[] installed)
        {
            var existingCore = installed.FirstOrDefault(package => package.name == CoreName);
            if (existingCore == null && AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                    assembly.GetType("AlienProject.WebWrapper.WebWrapperController", false) != null))
                throw new InvalidOperationException("Web Wrapper source is already compiled outside the UPM package. " +
                    "Review the existing Assets installation before adding UPM; installing both would duplicate assemblies.");

            ValidateVendorDestination();

            var missing = new List<Dependency>();
            foreach (var dependency in Dependencies)
            {
                var existing = installed.FirstOrDefault(package => package.name == dependency.Name);
                if (existing == null)
                {
                    if (RawSdkTypes.TryGetValue(dependency.Name, out var types) && types.Any(type =>
                            AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(type, false) != null)))
                        throw new InvalidOperationException(dependency.Name + " is already compiled outside UPM, " +
                            "typically from an SDK imported under Assets. No dependencies were changed. " +
                            "Back up the project, then migrate/remove that legacy Assets SDK using its vendor's UPM migration " +
                            "instructions before retrying. Do not install both the Assets and UPM copies.");
                    missing.Add(dependency);
                }
                else if (!string.Equals(existing.version, dependency.Version, StringComparison.Ordinal))
                    throw new InvalidOperationException(dependency.Name + " is already resolved as " + existing.version +
                        "; this installer expects " + dependency.Version + ". No dependencies were changed. " +
                        "Review that version in Package Manager before installing.");
            }

            var payloadRoot = PayloadRoot();
            var destinationRoot = Path.Combine(ProjectRoot, "WebWrapperPackages");
            var archives = Dependencies.Concat(new[] { new Dependency("UniWebView", "6.10.0", VendorArchive, VendorSha256) }).ToArray();

            // Validate every payload and destination before copying anything. Existing
            // archives are reusable only when identical; never overwrite a different file.
            foreach (var dependency in archives)
            {
                var source = Path.Combine(payloadRoot, dependency.Archive + ".bytes");
                if (!File.Exists(source)) throw new FileNotFoundException("Installer payload is missing: " + dependency.Archive);
                var sourceHash = HashFile(source);
                if (sourceHash != dependency.ExpectedSha256)
                    throw new InvalidDataException(dependency.Archive + " failed its SHA-256 check. Re-import the original installer.");
                var destination = Path.Combine(destinationRoot, dependency.Archive);
                if (File.Exists(destination) && HashFile(destination) != sourceHash)
                    throw new IOException("A different archive already exists at " + destination +
                        ". It was not overwritten. Move or review it manually before retrying.");
            }
            Directory.CreateDirectory(destinationRoot);
            foreach (var dependency in archives)
            {
                var destination = Path.Combine(destinationRoot, dependency.Archive);
                if (!File.Exists(destination)) File.Copy(Path.Combine(payloadRoot, dependency.Archive + ".bytes"), destination, false);
            }

            var additions = missing.Select(dependency =>
                // UPM local paths are relative to Packages/manifest.json. Keep the
                // completed project portable when moved or checked out on another machine.
                "file:../WebWrapperPackages/" + dependency.Archive)
                .Concat(BuiltInModules.Select(name => "com.unity.modules." + name)
                    .Where(name => !installed.Any(package => package.name == name)).Select(name => name + "@1.0.0")).ToArray();
            _deadline = DateTime.UtcNow.AddMinutes(10);
            SessionState.SetString(DeadlineKey, _deadline.Ticks.ToString(CultureInfo.InvariantCulture));
            SessionState.SetBool(PendingKey, true);
            Debug.Log("[WebWrapper Installer] Installing " + string.Join(", ", missing.Select(item => item.Name)) +
                      ". Existing dependencies and app settings are retained.");
            if (additions.Length > 0)
            {
                _add = Client.AddAndRemove(additions, Array.Empty<string>());
                _phase = Phase.Add;
            }
            else _phase = Phase.WaitForEditor;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static string PayloadRoot()
        {
            var paths = AssetDatabase.FindAssets("WebWrapperPackageInstaller t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith("/Editor/WebWrapperPackageInstaller.cs", StringComparison.Ordinal)).ToArray();
            if (paths.Length != 1) throw new InvalidOperationException("Keep one WebWrapperInstaller folder in Assets.");
            return Path.Combine(Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(paths[0]))).FullName, "Payload");
        }

        private static bool VendorPresent() => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("UniWebView", false) != null);

        private static void ValidateVendorDestination()
        {
            if (VendorPresent())
            {
                // Never replace an existing licensed/native plugin. A matching installed SDK is reused.
                var notes = "Assets/UniWebView/CHANGELOG.md";
                if (!File.Exists(notes) || !File.ReadLines(notes).Take(4).Any(line => line.Contains("6.10.0")))
                    throw new InvalidOperationException("UniWebView is already installed, but 6.10.0 could not be verified. " +
                        "No native files were replaced. Review the existing SDK version first.");
                foreach (var asset in new[] { "Assets/Plugins/Android/UniWebView.aar", "Assets/Plugins/iOS/UniWebView.xcframework" })
                    if (!File.Exists(asset) && !Directory.Exists(asset))
                        throw new InvalidOperationException("Existing UniWebView is incomplete: " + asset + ". Restore that SDK before retrying.");
                return;
            }
            foreach (var asset in new[] { "Assets/UniWebView", "Assets/Plugins/Android/UniWebView.aar",
                         "Assets/Plugins/UniWebView.bundle", "Assets/Plugins/iOS/UniWebView.xcframework" })
                if (File.Exists(asset) || Directory.Exists(asset))
                    throw new InvalidOperationException("Uncompiled or partial UniWebView files exist at " + asset +
                        ". No files were overwritten. Fix the existing SDK or use a clean project.");
        }

        private static void OnVendorImportFailed(string name, string error) => Fail("UniWebView import: " + error);
        private static void OnVendorImportCancelled(string name) => Fail("UniWebView import cancelled.");

        private static bool AllDependenciesResolved(PackageInfo[] packages) => Dependencies.All(expected =>
            packages.Any(package => package.name == expected.Name && package.version == expected.Version));

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void Fail(string message)
        {
            Finish();
            var detail = string.IsNullOrWhiteSpace(message) ? "Unity Package Manager did not provide an error message." : message;
            Debug.LogError("[WebWrapper Installer] " + detail);
            File.WriteAllText("Library/WebWrapperInstaller-result.txt", "FAILED: " + detail + "\n");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Web Wrapper installer stopped", detail, "OK");
        }

        private static void Finish()
        {
            EditorApplication.update -= Pump;
            EditorUtility.ClearProgressBar();
            SessionState.EraseBool(PendingKey);
            SessionState.EraseString(DeadlineKey);
            SessionState.EraseBool(VendorImportKey);
            AssetDatabase.importPackageFailed -= OnVendorImportFailed;
            AssetDatabase.importPackageCancelled -= OnVendorImportCancelled;
            _phase = Phase.Idle;
            _list = null;
            _add = null;
            _retryAfter = DateTime.MinValue;
        }

        private sealed class Dependency
        {
            public readonly string Name;
            public readonly string Version;
            public readonly string Archive;
            public readonly string ExpectedSha256;

            public Dependency(string name, string version, string archive, string expectedSha256)
            {
                Name = name; Version = version; Archive = archive;
                ExpectedSha256 = expectedSha256;
            }
        }
    }
}
