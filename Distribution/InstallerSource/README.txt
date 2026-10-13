WEB WRAPPER FULL 0.6.0 — ПЕРЕНОС В ДРУГУЮ ИГРУ

Для Unity 6. Проверено на 6000.3.8f1. Сохраните копию целевого проекта перед импортом.

1. Assets > Import Package > Custom Package:
   AlienProject-WebWrapper-Full-0.6.0.unitypackage. Импортируйте все файлы.
2. Tools > Alien Project > Install Web Wrapper Package > Install.
   Дождитесь сообщения Installed Web Wrapper 0.6.0 and all bundled SDKs.
   Установщик проверит версии и SHA-256, подключит локальные пакеты и UniWebView.
   Сцены, имя, bundle ID и настройки вашей игры не заменяются.
   При конфликте SDK установка остановится, не удаляя существующие SDK.
3. Сохраните первую сцену приложения и добавьте её первой в Build Profiles.
   Tools > Alien Project > Add Web Wrapper App to Scene.
   Либо добавьте компонент Web Wrapper App на отдельный активный объект сцены.
4. Заполните компонент данными НОВОГО приложения:
   Application Name, Bundle Identifier, Apple App ID (для iOS),
   Firebase Database URL, Firebase Config Path, AppsFlyer Dev Key.
   При использовании OneLink: One Link Domain и iOS URL Scheme.
   Privacy Policy URL — адрес политики вашего приложения.
   Имя и bundle ID синхронизируются в Player Settings после заполнения компонента.
5. Положите в Assets google-services.json для Android и/или GoogleService-Info.plist
   для iOS именно этого приложения. Bundle ID должен совпадать.
   Редактировать автоматически созданный Resources/WebWrapperSettings не нужно.
6. Для разделения игры и WebView укажите Game Root в компоненте:
   соберите стартовое меню, звук и игровую логику под отдельным объектом;
   СОХРАНИТЕ ЕГО ВЫКЛЮЧЕННЫМ. Он включится только при выборе игрового режима.
   WebWrapper App не должен находиться внутри Game Root.
   Loading Root необязателен: отдельный соседний объект с вашим загрузочным экраном.
   Для другой архитектуры используйте On Game Ready / WebWrapperLaunchState.
7. В Firebase создайте свой узел с полями enabled (boolean) и url (HTTPS string).
   enabled=true открывает этот URL; enabled=false, ошибка или таймаут запускают игру.
   Данные читаются при каждом холодном запуске. config.php не используется.
   Разрешите клиенту читать только этот узел. Публичную запись не разрешайте.
8. Для Android выполните Resolve Android SDK Dependencies после переключения
   платформы, затем Prepare Android Non-Development APK или обычную сборку Unity.
   Для iOS проверьте deployment target и подпись в Xcode; push capabilities и
   notification service extension добавляются пакетом при экспорте.

ЧТО ВХОДИТ

Web Wrapper 0.6.0; UniWebView 6.10.0 (Android/iOS/macOS Editor, без Demo);
AppsFlyer 7.0.3; Firebase App и Messaging 13.17.0; EDM4U 1.2.189;
Unity Mobile Notifications 2.4.3; Input System 1.18.0; uGUI 2.0.0.
Все перечисленные SDK лежат внутри архива, Git для их установки не нужен.
Firebase Realtime Database читается через REST-клиент Web Wrapper;
отдельная библиотека Firebase.Database не нужна и не включена.
Input System/uGUI нужны UniWebView. Настройки ввода игры не переключаются.
Лицензии и уведомления поставщиков сохранены внутри пакетов.

ЧТО НЕ ВХОДИТ

Игра Egg Catcher, сцены, спрайты, звуки, фото профиля, пользовательские сохранения,
ключи Sunny Joy, её имя/bundle ID, Firebase json/plist, адреса её базы/страницы,
иконка, Android keystore, ProjectSettings, Apple сертификаты и provisioning profiles.
Это комплект для ваших проектов с вашей лицензией UniWebView; не публичный SDK-репак.

ПОСЛЕ УСТАНОВКИ

WebWrapperPackages в корне проекта содержит архивы UPM, на которые ссылается manifest.
Сохраняйте эту папку вместе с проектом/в системе контроля версий. Пути относительные.
Assets/AlienProject/WebWrapperInstaller — только Editor-установщик и его payload;
они не добавляются в APK/IPA как игровые ресурсы. Не класть payload в Resources.
UniWebView находится в Assets/UniWebView и Assets/Plugins с нативными библиотеками.
Не ставьте одновременно ещё одну Assets-копию Firebase/AppsFlyer поверх UPM-версии.

ВНЕ UNITY ВСЁ ЕЩЁ НУЖНЫ

Зарегистрированное приложение Firebase и AppsFlyer, корректные правила базы,
OneLink и Apple Team ID для Universal Links, APNs key в Firebase для iOS push.
Для сборок нужен Android Build Support или Xcode/iOS Build Support соответственно.
Gradle/Maven и CocoaPods/SPM могут скачивать нативные зависимости при первой сборке:
SDK внутри unitypackage не означают полностью офлайн-сборку APK/IPA.
Установщик не меняет облачные проекты, правила базы, подписные ключи или чужие SDK.

ПРОВЕРИТЬ В КАЖДОЙ НОВОЙ ИГРЕ

Чистый запуск -> загрузка -> ответ Firebase -> WebView или игра.
Отключённая сеть/недоступная база -> игра без служебного экрана ошибки.
В WebView не должны играть музыка игры или появляться её PushPanel.
Полный экран, поворот, возврат из фона, клавиатура, back, cookies и загрузка файлов.
В игре: разрешение push только после открытия игрового интерфейса.
На устройствах: AppsFlyer, OneLink, push в фоне/при закрытом приложении и APNs.
Проверка импорта и компиляции не заменяет проверки на Android/iPhone.
