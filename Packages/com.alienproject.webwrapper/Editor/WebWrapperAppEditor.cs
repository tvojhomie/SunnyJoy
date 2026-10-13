using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    [CustomEditor(typeof(WebWrapperApp))]
    public sealed class WebWrapperAppEditor : UnityEditor.Editor
    {
        private bool _sceneBindings;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Один компонент на первой сцене. Запуск автоматический. Ошибка или отсутствие сети открывает игру.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("webViewEnabled"), new GUIContent("WebView Enabled", "Выключено — всегда игра. Включено — решение по enabled/url в Firebase DB. Применяется при следующем запуске."));
            EditorGUILayout.HelpBox(serializedObject.FindProperty("webViewEnabled").boolValue
                ? "WebView разрешён: Firebase enabled=true и корректная HTTPS-ссылка открывают страницу; enabled=false открывает игру."
                : "Только игра: запрос конфигурации WebView не отправляется, ссылки из пушей не переключают приложение в WebView.", MessageType.Info);
            Section("Приложение", "applicationName", "bundleIdentifier", "appleAppId");
            Section("Firebase Realtime Database", "firebaseDatabaseUrl", "firebaseConfigPath");
            Section("AppsFlyer", "appsFlyerDevKey");
            Section("OneLink / iOS — если используется", "oneLinkDomain", "iosUrlScheme");
            Section("Privacy Policy", "privacyPolicyUrl");
            _sceneBindings = EditorGUILayout.Foldout(_sceneBindings, "Подключение сцены — один раз", true);
            if (_sceneBindings)
            {
                EditorGUILayout.HelpBox("Для новой игры назначь Game Root и оставь его выключенным в сохранённой сцене. Он включится только после решения открыть игру. Компонент размести отдельно. Если игра уже использует WebWrapperLaunchState, Game Root не нужен.", MessageType.Info);
                Field("gameRoot"); Field("loadingRoot"); Field("onGameReady");
            }
            if (serializedObject.ApplyModifiedProperties())
                WebWrapperAppSetup.Synchronize((WebWrapperApp)target);
            var app = (WebWrapperApp)target;
            foreach (var issue in WebWrapperAppSetup.Validate(app, EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS))
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            if (app.GameRoot != null && app.GameRoot.activeSelf)
                EditorGUILayout.HelpBox("Сохрани Game Root неактивным: это гарантирует, что его Awake и звуки не запустятся до решения сервера.", MessageType.Warning);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Firebase SDK использует файлы этого приложения: google-services.json (Android) и GoogleService-Info.plist (iOS). Автоматически созданный Resources-asset редактировать не нужно. UniWebView, AppsFlyer и Firebase SDK должны быть установлены.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Применить настройки приложения")) WebWrapperAppSetup.Synchronize(app);
        }
        private void Section(string title, params string[] fields)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var field in fields) Field(field);
        }
        private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
    }

    [CustomEditor(typeof(WebWrapperSettings))]
    public sealed class WebWrapperSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (WebWrapperSettings)target;
            if (string.IsNullOrEmpty(settings.SourceSceneGuid))
            {
                EditorGUILayout.HelpBox("Перенеси настройки в компонент WebWrapperApp на первой сцене. Текущие рабочие ключи будут сохранены.", MessageType.Info);
                if (GUILayout.Button("Создать / выбрать компонент сцены")) WebWrapperAppSetup.AddToScene();
                return;
            }
            EditorGUILayout.HelpBox("Создаётся автоматически из WebWrapperApp перед запуском и сборкой. Настройки изменяются только на компоненте сцены.", MessageType.Info);
            EditorGUILayout.LabelField("Источник", AssetDatabase.GUIDToAssetPath(settings.SourceSceneGuid));
            if (GUILayout.Button("Открыть настройки приложения")) WebWrapperSetupWindow.Open();
        }
    }
}
