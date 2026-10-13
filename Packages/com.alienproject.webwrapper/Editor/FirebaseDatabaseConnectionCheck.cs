using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    public static class FirebaseDatabaseConnectionCheck
    {
        [MenuItem("Tools/Alien Project/Check Firebase DB Connection")]
        public static async void Run() => await CheckAsync();

        public static async void RunBatch()
        {
            var success = await CheckAsync();
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        private static async Task<bool> CheckAsync()
        {
            const string reportPath = "Library/FirebaseDatabaseConnectionTest.txt";
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            try
            {
                if (settings == null || !settings.UsesFirebaseDatabase)
                    throw new InvalidOperationException("Select FirebaseRealtimeDatabase in WebWrapperSettings first.");
                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var client = new FirebaseDatabaseConfigClient(settings.FirebaseDatabaseUrl,
                        settings.FirebaseConfigPath, settings.RequestTimeoutSeconds);
                    var result = await client.FetchAsync(null, cancellation.Token);
                    var report = "Firebase DB connection check (real GET, no writes)\n" +
                        "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                        "Endpoint: " + settings.ActiveConfigAddress + "\n" +
                        "Result: " + result.Kind + "\n" +
                        "URL: " + WebWrapperDiagnostics.SafeUrl(result.Url) + "\n" + result.Message;
                    File.WriteAllText(reportPath, report);
                    Debug.Log("[WebWrapper] " + report);
                    return result.Kind == ConfigResultKind.Success;
                }
            }
            catch (Exception exception)
            {
                File.WriteAllText(reportPath, "Failed: " + exception.GetType().Name + "\n" + exception.Message);
                Debug.LogWarning("[WebWrapper] Firebase DB check failed: " + exception.Message);
                return false;
            }
        }
    }
}
