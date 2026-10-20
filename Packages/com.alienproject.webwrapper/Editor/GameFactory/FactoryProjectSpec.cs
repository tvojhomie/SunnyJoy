using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AlienProject.WebWrapper;
using AlienProject.WebWrapper.Editor;

namespace GameFactory.Editor
{
    [Serializable]
    public sealed class FactoryProjectSpec
    {
        public int schemaVersion = 1;
        public string projectName = "";
        public string bundleId = "";
        public bool android = true;
        public bool ios;
        public bool connectServices;
        public bool webViewEnabled;
        public string appleId = "";
        public string appsFlyerKey = "";
        public string databaseUrl = "";
        public string databasePath = "";
        public string oneLinkDomain = "";
        public string urlScheme = "";
        public string privacyUrl = "";
        public string androidConfig = "";
        public string iosConfig = "";
        public string iconFile = "";

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (schemaVersion != 1) errors.Add("Неизвестная версия конфигурации GameFactory.");
            if (!Regex.IsMatch(projectName ?? "", @"\A[\p{L}\p{N}][\p{L}\p{N} _-]{0,63}\z"))
                errors.Add("Название: 1–64 символа, буквы, цифры, пробел, дефис или подчёркивание.");
            if (!WebWrapperAppSetup.ValidBundle(bundleId)) errors.Add("Укажи Bundle ID в формате com.company.game.");
            if (!android && !ios) errors.Add("Выбери Android или iOS.");
            if (!string.IsNullOrEmpty(privacyUrl) && !Https(privacyUrl)) errors.Add("Privacy Policy должна быть HTTPS-ссылкой.");
            if (!string.IsNullOrEmpty(appleId) && !StoreIdNormalizer.IsValidIos(appleId)) errors.Add("Apple ID должен быть числовым.");
            if (!string.IsNullOrEmpty(oneLinkDomain) && !Regex.IsMatch(oneLinkDomain, @"\A[a-zA-Z0-9-]+\.onelink\.me\z"))
                errors.Add("OneLink: только домен вида name.onelink.me.");
            if (!string.IsNullOrEmpty(urlScheme) && !Regex.IsMatch(urlScheme, @"\A[a-zA-Z][a-zA-Z0-9+.-]*\z"))
                errors.Add("URL Scheme указывается без ://.");
            if (connectServices)
            {
                if (string.IsNullOrWhiteSpace(appsFlyerKey)) errors.Add("Для подключения сервисов нужен AppsFlyer Dev Key.");
                if (ios && !StoreIdNormalizer.IsValidIos(appleId)) errors.Add("Для iOS-сервисов нужен Apple ID.");
                if (!FirebaseDatabaseConfigClient.TryBuildEndpoint(databaseUrl, databasePath, out _))
                    errors.Add("Для WebWrapper укажи Firebase Database URL и путь узла.");
                if (android && string.IsNullOrEmpty(androidConfig)) errors.Add("Добавь google-services.json нового приложения.");
                if (ios && string.IsNullOrEmpty(iosConfig)) errors.Add("Добавь GoogleService-Info.plist нового приложения.");
            }
            ValidateConfig(androidConfig, true, errors);
            ValidateConfig(iosConfig, false, errors);
            if (!string.IsNullOrEmpty(iconFile) && (!File.Exists(iconFile) ||
                !string.Equals(Path.GetExtension(iconFile), ".png", StringComparison.OrdinalIgnoreCase)))
                errors.Add("Иконка должна быть существующим PNG-файлом.");
            return errors;
        }

        private void ValidateConfig(string path, bool isAndroid, List<string> errors)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024 ||
                    !(isAndroid ? FirebaseConfigurationValidator.AndroidMatches(File.ReadAllText(path), bundleId) :
                        FirebaseConfigurationValidator.IosMatches(File.ReadAllText(path), bundleId)))
                    errors.Add((isAndroid ? "google-services.json" : "GoogleService-Info.plist") + " отсутствует или не соответствует Bundle ID.");
            }
            catch (IOException) { errors.Add("Не удалось прочитать файл Firebase."); }
            catch (UnauthorizedAccessException) { errors.Add("Нет доступа к файлу Firebase."); }
        }

        private static bool Https(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    }
}
