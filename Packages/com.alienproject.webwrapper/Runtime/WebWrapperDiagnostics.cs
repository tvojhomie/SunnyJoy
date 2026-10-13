using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AlienProject.WebWrapper
{

    public static class WebWrapperDiagnostics
    {
        public const int Capacity = 250;
        private static readonly object Gate = new object();
        private static readonly Queue<string> Entries = new Queue<string>();
        private static string _devKey;
        public static bool Enabled { get; private set; }
        public static bool ConsoleOpen { get; set; }
        public static int Revision { get; private set; }
        public static float ToolbarPixels => Enabled ? 56f * Mathf.Max(1f, Screen.width / 420f) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Application.logMessageReceivedThreaded -= Capture;
            Enabled = false;
            ConsoleOpen = false;
            _devKey = null;
            Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize() => EnsureInitialized();

        public static void EnsureInitialized()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (Enabled) return;
            _devKey = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName)?.AppsFlyerDevKey;
            Enabled = true;
            Application.logMessageReceivedThreaded += Capture;
            Record("Startup", "Android DEVELOPMENT APK; real SDKs/server, sensitive values redacted.");
            Record("App", Application.identifier + " v" + Application.version + "; Unity " + Application.unityVersion);
#endif
        }

        public static void Record(string area, string message)
        {
            if (!Enabled) return;
            Append("INFO", "[" + area + "] " + message);
        }

        private static void Capture(string message, string stackTrace, LogType type)
        {
            if (!Enabled) return;
            Append(type.ToString(), message +
                (type == LogType.Exception || type == LogType.Error || type == LogType.Assert ? "\n" + stackTrace : ""));
        }

        private static void Append(string level, string message)
        {
            var safe = Sanitize(message, _devKey);
            if (safe.Length > 6000) safe = safe.Substring(0, 6000) + " [truncated]";
            lock (Gate)
            {
                while (Entries.Count >= Capacity) Entries.Dequeue();
                Entries.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + " " + level + " " + safe);
                Revision++;
            }
        }

        public static string Snapshot()
        {
            lock (Gate) return string.Join("\n\n", Entries);
        }

        public static void Clear()
        {
            lock (Gate) { Entries.Clear(); Revision++; }
        }

        public static string Sanitize(string text, string secret = null)
        {
            text = text ?? string.Empty;
            if (!string.IsNullOrEmpty(secret)) text = text.Replace(secret, "[redacted]");

            text = Regex.Replace(text, @"https?://[^\s""<>]+", match => SafeUrl(match.Value));
            text = Regex.Replace(text,
                @"(?i)([""']?(?:[\w-]*(?:token|secret|password|api_key|devkey|dev_key|authorization|email|idfa|advertising_id|af_id|appsflyer_id)[\w-]*)[""']?\s*[:=]\s*)(""(?:\\.|[^""\\])*""|'[^']*'|[^\s,;}]+)",
                "$1[redacted]");

            return Regex.Replace(text, @"[A-Za-z0-9_:+/=-]{40,}", "[opaque value redacted]");
        }

        public static string SafeUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https")) return "[invalid URL]";
            return uri.GetLeftPart(UriPartial.Authority).Replace(uri.UserInfo + "@", "") + uri.AbsolutePath +
                (string.IsNullOrEmpty(uri.Query) ? "" : "?[redacted]") +
                (string.IsNullOrEmpty(uri.Fragment) ? "" : "#[redacted]");
        }

        public static string DescribeRequest(IReadOnlyDictionary<string, object> values)
        {
            var safe = new Dictionary<string, object>();
            foreach (var pair in values)
            {
                switch (pair.Key)
                {
                    case "bundle_id": case "os": case "store_id": case "locale":
                    case "af_status": case "is_first_launch": case "is_unity_editor":
                        safe[pair.Key] = Sanitize(Convert.ToString(pair.Value)); break;
                    default:
                        safe[pair.Key] = pair.Value == null ? null : "[redacted]"; break;
                }
            }
            return JsonObjectWriter.Serialize(safe);
        }
    }
}
