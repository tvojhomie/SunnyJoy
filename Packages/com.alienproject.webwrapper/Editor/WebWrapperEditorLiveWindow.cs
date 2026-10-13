using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienProject.WebWrapper.Editor
{
    public sealed class WebWrapperEditorLiveWindow : EditorWindow
    {
        [SerializeField] private WebWrapperEditorLiveSession.AttributionMode mode;
        private Vector2 scroll;
        private bool permissionWasPending;

        [MenuItem("Tools/Alien Project/Test Real Server in Editor")]
        public static void Open()
        {
            var window = GetWindow<WebWrapperEditorLiveWindow>("Real Server Test");
            window.minSize = new Vector2(480, 550);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) WebWrapperEditorLiveSession.End();

            if (state == PlayModeStateChange.EnteredPlayMode && WebWrapperEditorLiveSession.IsEnabled) Open();
        }

        private void OnInspectorUpdate()
        {
            bool waiting = WebWrapperEditorLiveSession.PermissionPending;
            if (waiting && !permissionWasPending) Focus();
            permissionWasPending = waiting;
            Repaint();
        }

        private void OnGUI()
        {
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            EditorGUILayout.LabelField("Реальный сервер — тест в Unity", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("HTTP-запрос, ответ сервера, кэш и WebView настоящие. AppsFlyer/OneLink на iPhone, FCM/APNs и системные разрешения здесь проверить нельзя. Вместо них — явно помеченные QA-данные и симуляция разрешения. Настройки билда не меняются.", MessageType.Info);
            EditorGUILayout.LabelField("Config URL", settings != null ? settings.ActiveConfigAddress : "Settings not found");
            var databaseMode = settings != null && settings.UsesFirebaseDatabase;
            EditorGUILayout.LabelField("Источник", databaseMode ? "Firebase Realtime Database — GET" : "HTTP config — POST");
            EditorGUILayout.LabelField("Платформа запроса", databaseMode ? "Общий узел Android/iOS; данные устройства не передаются" : "iOS (независимо от Build Target)");
            EditorGUILayout.LabelField("Bundle", WebWrapperEditorLiveSession.BundleId);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (!databaseMode)
                {
                    mode = (WebWrapperEditorLiveSession.AttributionMode)EditorGUILayout.EnumPopup("Тестовая атрибуция", mode);
                    EditorGUILayout.HelpBox("NonOrganicQa/OrganicQa: af_id = unity-editor-…, media_source = unity_editor, campaign = unity_editor_test. NoNativeData: пустой af_id. Решение всегда принимает сервер — web-адрес не подменяется.", MessageType.None);
                }
                else
                    EditorGUILayout.HelpBox("Ссылка читается заново при каждом запуске из Firebase DB. AppsFlyer/FCM не блокируют чтение. enabled=false, пустой узел или ошибка открывают игру. Никаких POST/записей в базу из приложения.", MessageType.None);
                using (new EditorGUI.DisabledScope(settings == null || settings.UseMockServices))
                {
                    if (GUILayout.Button("Новая установка → реальный сервер", GUILayout.Height(34))) StartSession(true);
                    if (GUILayout.Button("Повторный запуск → сохранённое состояние", GUILayout.Height(28))) StartSession(false);
                }
            }
            if (settings != null && settings.UseMockServices)
                EditorGUILayout.HelpBox("Сначала выключите Use Mock Services в WebWrapperSettings. Тест не меняет этот production-asset автоматически.", MessageType.Warning);
            if (EditorApplication.isPlaying && WebWrapperEditorLiveSession.IsEnabled)
            {
                if (GUILayout.Button("Остановить тест")) EditorApplication.ExitPlaymode();
                if (WebWrapperEditorLiveSession.PermissionPending)
                {
                    EditorGUILayout.HelpBox("СИМУЛЯЦИЯ системного запроса iOS. Это не разрешение macOS и не регистрация в APNs.", MessageType.Warning);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Allow (симуляция)", GUILayout.Height(32))) WebWrapperEditorLiveSession.CompletePermission(true);
                    if (GUILayout.Button("Don't Allow (симуляция)", GUILayout.Height(32))) WebWrapperEditorLiveSession.CompletePermission(false);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.HelpBox(databaseMode
                ? "Firebase читается на каждом запуске, независимо от старого кэша. UniWebView в macOS Editor открывается отдельным окном — это поведение плагина."
                : "Новая установка использует отдельное тестовое хранилище. Повторный запуск честно использует кэш/сохранённый выбор и может не обращаться к серверу. UniWebView в macOS Editor открывается отдельным окном — это поведение плагина.", MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!databaseMode)
            {
                EditorGUILayout.LabelField("Отправленный JSON (тестовые данные)", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(WebWrapperEditorLiveSession.RequestJson, EditorStyles.textArea, GUILayout.Height(85));
            }
            EditorGUILayout.LabelField("Этапы запуска", EditorStyles.boldLabel);
            var transcript = WebWrapperEditorLiveSession.Transcript;
            float logHeight = Mathf.Max(230, EditorStyles.textArea.CalcHeight(new GUIContent(transcript), position.width - 30));
            EditorGUILayout.SelectableLabel(transcript, EditorStyles.textArea, GUILayout.Height(logHeight));
            EditorGUILayout.EndScrollView();
        }

        private static void StartSession(bool fresh)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var window = GetWindow<WebWrapperEditorLiveWindow>();
            WebWrapperEditorLiveSession.Prepare(fresh, window.mode);
            EditorApplication.EnterPlaymode();
            GUIUtility.ExitGUI();
        }
    }
}
