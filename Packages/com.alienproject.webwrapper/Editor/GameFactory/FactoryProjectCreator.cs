using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameFactory.Editor
{
    public static class FactoryProjectCreator
    {
        public const string PackagePath = "Packages/com.alienproject.webwrapper";
        public const string SetupFile = "GameFactory.setup.json";
        public static readonly string[] TemplateDirectories =
        {
            PackagePath, "Assets/UniWebView", "Assets/Editor/UniWebView",
            "Assets/Plugins/UniWebView.bundle", "Assets/Plugins/iOS/UniWebView.xcframework"
        };
        public static readonly string[] TemplateFiles =
        {
            "ProjectSettings/ProjectVersion.txt",
            "Assets/Plugins/Android/UniWebView.aar", "Assets/Plugins/Android/UniWebView.aar.meta",
            "Assets/Plugins/UniWebView.bundle.meta", "Assets/Plugins/iOS/UniWebView.xcframework.meta"
        };
        public const string IgnoreContents =
            "/[Ll]ibrary/\n/[Tt]emp/\n/[Oo]bj/\n/[Ll]ogs/\n/[Uu]ser[Ss]ettings/\n/[Bb]uilds/\n/[Bb]uild/\n/Backups/\n" +
            "/Assets/_Recovery/\n/Assets/_Recovery.meta\n/.vs/\n/.vscode/\n/.idea/\n/.utmp/\n" +
            "*.csproj\n*.sln\n*.slnx\n*.user\n*.userprefs\n*.suo\n*.apk\n*.aab\n*.ipa\n*.unitypackage\n" +
            "*_BurstDebugInformation_DoNotShip/\n*_BackUpThisFolder_ButDontShipItWithYourGame/\nmono_crash.*\n.DS_Store\n._*\n" +
            "*.keystore\n*.jks\n*.p12\n*.pfx\n*.mobileprovision\nAuthKey_*.p8\n.env\n.env.*\n*.pem\n*.key\n" +
            "*firebase-adminsdk*.json\n*service-account*.json\n*service_account*.json\n/" + SetupFile + "\n";

        public static string ValidateDestination(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathRooted(destination)) return "Выбери абсолютный путь нового проекта.";
            source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
            destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            if (Within(destination, source) || Within(source, destination))
                return "Новый проект должен находиться вне папки исходного проекта, не внутри неё и не выше неё.";
            if (File.Exists(destination) || Directory.Exists(destination)) return "Выбери новую, ещё не существующую папку. Существующие файлы не перезаписываются.";
            for (var parent = new DirectoryInfo(Path.GetDirectoryName(destination)); parent != null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    return "Папка назначения не должна проходить через символическую ссылку.";
            return null;
        }

        private static bool Within(string child, string parent) => string.Equals(child, parent, StringComparison.OrdinalIgnoreCase) ||
            child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        public static void Create(string source, string destination, FactoryProjectSpec spec, Action<float, string> progress = null)
        {
            var errors = spec.Validate();
            var destinationError = ValidateDestination(source, destination);
            if (destinationError != null) errors.Add(destinationError);
            foreach (var path in TemplateDirectories)
                if (!Directory.Exists(Path.Combine(source, path))) errors.Add("В шаблоне отсутствует папка: " + path);
            foreach (var path in TemplateFiles)
                if (!File.Exists(Path.Combine(source, path))) errors.Add("В шаблоне отсутствует файл: " + path);
            var archives = Directory.Exists(Path.Combine(source, "GooglePackages"))
                ? Directory.GetFiles(Path.Combine(source, "GooglePackages"), "com.google.firebase.*.tgz") : Array.Empty<string>();
            foreach (var name in new[] { "com.google.firebase.app", "com.google.firebase.messaging" })
                if (archives.Count(p => Path.GetFileName(p).StartsWith(name + "-", StringComparison.Ordinal)) != 1)
                    errors.Add("Нужен ровно один локальный архив " + name + ".");
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

            var copies = new List<KeyValuePair<string, string>>();
            foreach (var directory in TemplateDirectories) Collect(source, directory, copies);
            foreach (var file in TemplateFiles) copies.Add(new KeyValuePair<string, string>(Path.Combine(source, file), file));
            foreach (var archive in archives) copies.Add(new KeyValuePair<string, string>(archive, "GooglePackages/" + Path.GetFileName(archive)));
            foreach (var copy in copies)
                if ((File.GetAttributes(copy.Key) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Символические ссылки не копируются: " + copy.Value);
            Directory.CreateDirectory(destination);
            Write(destination, SetupFile, "{\"schemaVersion\":0}");
            for (int i = 0; i < copies.Count; i++)
            {
                progress?.Invoke((float)i / copies.Count, copies[i].Value);
                Copy(copies[i].Key, Path.Combine(destination, copies[i].Value));
            }
            var clean = JsonUtility.FromJson<FactoryProjectSpec>(JsonUtility.ToJson(spec));
            clean.androidConfig = CopySelected(spec.androidConfig, destination, "Assets/google-services.json");
            clean.iosConfig = CopySelected(spec.iosConfig, destination, "Assets/GoogleService-Info.plist");
            clean.iconFile = CopySelected(spec.iconFile, destination, "Assets/App/Branding/AppIcon.png");
            Write(destination, "Packages/manifest.json", Manifest(archives));
            Write(destination, ".gitignore", IgnoreContents);
            Write(destination, ".gitattributes", "* text=auto\n*.cs text eol=lf\n*.meta text eol=lf\n*.unity text eol=lf\n*.png binary\n*.tgz binary\n*.aar binary\n");
            Write(destination, "GAMEFACTORY-START.txt", "GameFactory 0.1\n\nOpen this folder in Unity " +
                File.ReadAllText(Path.Combine(source, "ProjectSettings/ProjectVersion.txt")).Split('\n')[0].Replace("m_EditorVersion: ", "") +
                ".\nWait for Package Manager and compilation. Internet may be needed for SDK dependencies.\n" +
                "Open Tools > GameFactory > Open. Click Finish setup for this new project.\n" +
                "Replace the Game Content root with your own game. The template contains no SunnyJoy gameplay or art.\n" +
                "Run project checks and resolve Android SDK dependencies before a device build.\n" +
                "No build, Git commit, push, or remote database changes are performed automatically.\n" +
                "Use a private repository: the SDK bundle includes your licensed UniWebView copy.\n");
            Write(destination, SetupFile, JsonUtility.ToJson(clean, true));
            progress?.Invoke(1, "Готово");
        }

        private static string CopySelected(string source, string root, string relative)
        {
            if (string.IsNullOrEmpty(source)) return "";
            Copy(source, Path.Combine(root, relative));
            return relative;
        }

        private static void Collect(string root, string relative, List<KeyValuePair<string, string>> files)
        {
            var absolute = Path.Combine(root, relative);
            if ((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Символические ссылки не копируются: " + relative);
            foreach (var file in Directory.GetFiles(absolute))
            {
                if (Path.GetFileName(file) == ".DS_Store" || Path.GetFileName(file).StartsWith("._", StringComparison.Ordinal)) continue;
                files.Add(new KeyValuePair<string, string>(file, relative + "/" + Path.GetFileName(file)));
            }
            foreach (var child in Directory.GetDirectories(absolute))
                Collect(root, relative + "/" + Path.GetFileName(child), files);
        }

        private static string Manifest(string[] archives)
        {
            var entries = new SortedDictionary<string, string>
            {
                ["appsflyer-unity-plugin"] = "https://github.com/AppsFlyerSDK/appsflyer-unity-plugin.git#v7.0.3",
                ["com.google.external-dependency-manager"] = "https://github.com/googlesamples/unity-jar-resolver.git?path=upm#v1.2.189",
                ["com.unity.mobile.notifications"] = "2.4.3",
                ["com.unity.ugui"] = "2.0.0",
                ["com.unity.test-framework"] = "1.6.0",
                ["com.unity.modules.audio"] = "1.0.0",
                ["com.unity.modules.imageconversion"] = "1.0.0",
                ["com.unity.modules.jsonserialize"] = "1.0.0",
                ["com.unity.modules.unitywebrequest"] = "1.0.0",
                ["com.unity.modules.unitywebrequesttexture"] = "1.0.0"
            };
            foreach (var name in new[] { "com.google.firebase.app", "com.google.firebase.messaging" })
                entries[name] = "file:../GooglePackages/" + Path.GetFileName(archives.Single(p => Path.GetFileName(p).StartsWith(name + "-", StringComparison.Ordinal)));
            return "{\n  \"dependencies\": {\n" + string.Join(",\n", entries.Select(pair =>
                "    \"" + pair.Key + "\": \"" + pair.Value + "\"")) + "\n  },\n  \"testables\": [\"com.alienproject.webwrapper\"]\n}\n";
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, false);
        }
        private static void Write(string root, string relative, string contents)
        {
            var target = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, contents, new UTF8Encoding(false));
        }

        public static void InitializeGit(string project)
        {
            if (!File.Exists(Path.Combine(project, "Packages/manifest.json")) ||
                !File.Exists(Path.Combine(project, SetupFile))) throw new InvalidOperationException("Выбери созданный GameFactory проект.");
            if (Directory.Exists(Path.Combine(project, ".git"))) throw new InvalidOperationException("Git уже инициализирован.");
            using (var process = Process.Start(new ProcessStartInfo("git", "init -b main")
            { WorkingDirectory = project, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            {
                if (!process.WaitForExit(10000)) throw new InvalidOperationException("Git не завершился за 10 секунд. Проверь папку вручную.");
                if (process.ExitCode != 0) throw new InvalidOperationException("Не удалось выполнить git init: " + process.StandardError.ReadToEnd());
            }
        }
    }
}
