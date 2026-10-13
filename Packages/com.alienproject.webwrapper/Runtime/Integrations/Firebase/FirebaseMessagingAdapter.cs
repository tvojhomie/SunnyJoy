using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using Firebase.Messaging;
using Unity.Notifications;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]

namespace AlienProject.WebWrapper.Integrations.Firebase
{
    public sealed class FirebaseMessagingAdapter : MonoBehaviour,
        IPushRegistrationProvider,
        IPushRegistrationChangeSource,
        INotificationPermissionProvider,
        IPushOpenSource
    {
        private readonly Queue<PushOpenMessage> _bufferedOpens = new Queue<PushOpenMessage>();
        private readonly TaskCompletionSource<bool> _registrationSignal = new TaskCompletionSource<bool>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();

        private WebWrapperSettings _settings;
        private Task _initialization;
        private Task _tokenRefresh;
        private Action<PushOpenMessage> _opened;
        private string _registrationId = string.Empty;
        private string _firebaseProjectId = string.Empty;
        private bool _eventsSubscribed;
        private bool _firebaseReady;
        private volatile bool _destroyed;
        private int _mainThreadId;

        public event Action<PushRegistration> RegistrationChanged;

        public event Action<PushOpenMessage> Opened
        {
            add
            {
                _opened += value;
                FlushBufferedOpens();
            }
            remove => _opened -= value;
        }

        public NotificationPermissionStatus Status => ReadPermissionStatus();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBeforeSceneLoad()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (settings == null || settings.UseMockServices)
            {
                return;
            }

            if (FindFirstObjectByType<FirebaseMessagingAdapter>() != null)
            {
                return;
            }

            var host = new GameObject("[WebWrapper.Firebase]");
            DontDestroyOnLoad(host);
            host.AddComponent<FirebaseMessagingAdapter>();
#endif
        }

        private void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (_settings == null)
            {
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            try { InitializeNotificationCenter(); }
            catch (Exception exception)
            {

                Debug.LogWarning("[WebWrapper.Firebase] Notification center unavailable: " + exception.Message);
            }
            WebWrapperServices.PushRegistrationProvider = this;
            WebWrapperServices.NotificationPermissionProvider = this;
            WebWrapperServices.PushOpenSource = this;
            _initialization = InitializeFirebaseAsync();
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _lifetime.Cancel();
            UnsubscribeFromFirebaseEvents();
            lock (_mainThreadActions)
            {
                _mainThreadActions.Clear();
            }

            _bufferedOpens.Clear();
            _opened = null;
            RegistrationChanged = null;
            if (ReferenceEquals(WebWrapperServices.PushRegistrationProvider, this))
            {
                WebWrapperServices.PushRegistrationProvider = null;
            }

            if (ReferenceEquals(WebWrapperServices.NotificationPermissionProvider, this))
            {
                WebWrapperServices.NotificationPermissionProvider = null;
            }

            if (ReferenceEquals(WebWrapperServices.PushOpenSource, this))
            {
                WebWrapperServices.PushOpenSource = null;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused || _destroyed || !_firebaseReady || !CanInitializeMessaging)
            {
                return;
            }

            try
            {

                EnableMessaging();
                _ = EnsureTokenRefresh();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WebWrapper.Firebase] Push setup is pending: " + exception.Message);
            }
        }

        public async Task<PushRegistration> GetRegistrationAsync(CancellationToken cancellationToken)
        {
            using (var deadline = CreateDeadline(cancellationToken))
            {
                try
                {

                    await SdkTaskWaiter.WaitAsync(_initialization, deadline.Token);
                    if (_firebaseReady && CanInitializeMessaging)
                    {
                        EnableMessaging();
                        if (string.IsNullOrWhiteSpace(_registrationId))
                        {
                            await SdkTaskWaiter.WaitAsync(
                                Task.WhenAny(EnsureTokenRefresh(), _registrationSignal.Task), deadline.Token);
                        }

                        if (string.IsNullOrWhiteSpace(_registrationId))
                        {
                            await SdkTaskWaiter.WaitAsync(_registrationSignal.Task, deadline.Token);
                        }
                    }
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
                {

                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    Debug.LogWarning("[WebWrapper.Firebase] Registration unavailable: " + exception.Message);
                }
            }

            return CurrentRegistration;
        }

        public async Task<NotificationPermissionStatus> RequestAsync(
            CancellationToken cancellationToken)
        {
            var request = NotificationCenter.RequestPermission();
            while (request.Status == NotificationsPermissionStatus.RequestPending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.Status != NotificationsPermissionStatus.Granted)
            {
                return ReadPermissionStatus();
            }

            cancellationToken.ThrowIfCancellationRequested();

            _ = FinishRegistrationAfterPermissionAsync();
            return NotificationPermissionStatus.Granted;
        }

        private async Task FinishRegistrationAfterPermissionAsync()
        {
            try
            {
                await Task.Yield();
                await SdkTaskWaiter.WaitAsync(_initialization, _lifetime.Token);
                if (_firebaseReady && CanInitializeMessaging)
                {

                    EnableMessaging();
                    await EnsureTokenRefresh();
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WebWrapper.Firebase] Push setup is pending: " + exception.Message);
            }
        }

        private async Task InitializeFirebaseAsync()
        {
            try
            {
                var dependencyStatus = await SdkTaskWaiter.WaitAsync(
                    FirebaseApp.CheckAndFixDependenciesAsync(), _lifetime.Token);
                if (dependencyStatus != DependencyStatus.Available)
                {
                    Debug.LogError(
                        "[WebWrapper.Firebase] Dependencies are unavailable: " + dependencyStatus);
                    return;
                }

                var app = FirebaseApp.DefaultInstance;
                _firebaseProjectId = app.Options?.ProjectId ?? string.Empty;
                _firebaseReady = true;
                WebWrapperDiagnostics.Record("Firebase", "Dependencies available; app initialized. Project=" + _firebaseProjectId);
                if (CanInitializeMessaging)
                {
                    EnableMessaging();

                    _ = EnsureTokenRefresh();
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError("[WebWrapper.Firebase] Initialization failed: " + exception.Message);
            }
        }

        private async Task RefreshRegistrationAsync()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR

                await SdkTaskWaiter.WaitAsync(
                    FirebaseMessaging.RequestPermissionAsync(), _lifetime.Token);
#endif

#pragma warning disable CS0618
                var token = await SdkTaskWaiter.WaitAsync(
                    FirebaseMessaging.GetTokenAsync(), _lifetime.Token);
#pragma warning restore CS0618
                DispatchOnMainThread(() => SetRegistrationId(token));
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WebWrapper.Firebase] FCM registration is not available: " + exception.Message);
            }
        }

        private void SubscribeToFirebaseEvents()
        {
            if (_eventsSubscribed)
            {
                return;
            }

#pragma warning disable CS0618
            FirebaseMessaging.TokenReceived += OnTokenReceived;
#pragma warning restore CS0618
            FirebaseMessaging.MessageReceived += OnMessageReceived;
            _eventsSubscribed = true;
        }

        private void UnsubscribeFromFirebaseEvents()
        {
            if (!_eventsSubscribed)
            {
                return;
            }

#pragma warning disable CS0618
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
#pragma warning restore CS0618
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
            _eventsSubscribed = false;
        }

        private void OnTokenReceived(object sender, TokenReceivedEventArgs eventArgs)
        {
            var token = eventArgs?.Token;
            DispatchOnMainThread(() => SetRegistrationId(token));
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs eventArgs)
        {
            var message = eventArgs?.Message;
            if (message == null)
            {
                return;
            }

            var opened = PushOpenMessageMapper.FromPayload(
                message.NotificationOpened,
                message.MessageId,
                message.Link?.AbsoluteUri,
                message.Data);
            if (opened == null)
            {
                return;
            }

            DispatchOnMainThread(() =>
            {
                if (_opened == null)
                {
                    _bufferedOpens.Enqueue(opened);
                    return;
                }

                _opened.Invoke(opened);
            });
        }

        private void SetRegistrationId(string registrationId)
        {
            if (_destroyed || string.IsNullOrWhiteSpace(registrationId) ||
                string.Equals(_registrationId, registrationId, StringComparison.Ordinal))
            {
                return;
            }

            _registrationId = registrationId;
            WebWrapperDiagnostics.Record("Firebase", "FCM registration token received (value hidden).");
            _registrationSignal.TrySetResult(true);
            RegistrationChanged?.Invoke(CurrentRegistration);
        }

        private void FlushBufferedOpens()
        {
            if (_opened == null)
            {
                return;
            }

            while (_bufferedOpens.Count > 0)
            {
                _opened.Invoke(_bufferedOpens.Dequeue());
            }
        }

        private void InitializeNotificationCenter()
        {
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = string.IsNullOrWhiteSpace(_settings.NotificationChannelId)
                ? "webwrapper_messages"
                : _settings.NotificationChannelId.Trim();
            args.AndroidChannelName = _settings.NotificationChannelName;
            args.AndroidChannelDescription = _settings.NotificationChannelDescription;
            args.PresentationOptions = NotificationPresentation.Alert |
                                       NotificationPresentation.Badge |
                                       NotificationPresentation.Sound;
            NotificationCenter.Initialize(args);
        }

        private static NotificationPermissionStatus ReadPermissionStatus()
        {
#if UNITY_EDITOR
            return NotificationPermissionStatus.Unknown;
#elif UNITY_ANDROID
            switch (AndroidNotificationCenter.UserPermissionToPost)
            {
                case PermissionStatus.Allowed:
                    return NotificationPermissionStatus.Granted;
                case PermissionStatus.NotRequested:
                case PermissionStatus.RequestPending:
                    return NotificationPermissionStatus.Unknown;
                case PermissionStatus.Denied:
                    return AndroidNotificationCenter.ShouldShowPermissionToPostRationale
                        ? NotificationPermissionStatus.DeniedCanAskAgain
                        : NotificationPermissionStatus.DeniedPermanently;
                default:
                    return NotificationPermissionStatus.DeniedPermanently;
            }
#elif UNITY_IOS
            switch (iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus)
            {
                case AuthorizationStatus.Authorized:
                case AuthorizationStatus.Provisional:
                case AuthorizationStatus.Ephemeral:
                    return NotificationPermissionStatus.Granted;
                case AuthorizationStatus.NotDetermined:
                    return NotificationPermissionStatus.Unknown;
                default:
                    return NotificationPermissionStatus.DeniedPermanently;
            }
#else
            return NotificationPermissionStatus.Unknown;
#endif
        }

        private PushRegistration CurrentRegistration =>
            new PushRegistration(_registrationId, _firebaseProjectId);

        private static bool CanInitializeMessaging
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return ReadPermissionStatus() == NotificationPermissionStatus.Granted;
#else
                return true;
#endif
            }
        }

        private void EnableMessaging()
        {
            if (_eventsSubscribed)
            {
                return;
            }

            SubscribeToFirebaseEvents();
#pragma warning disable CS0618
            FirebaseMessaging.TokenRegistrationOnInitEnabled = true;
#pragma warning restore CS0618
        }

        private Task EnsureTokenRefresh()
        {
            if (_tokenRefresh == null || _tokenRefresh.IsCompleted)
            {
                _tokenRefresh = RefreshRegistrationAsync();
            }

            return _tokenRefresh;
        }

        private CancellationTokenSource CreateDeadline(CancellationToken cancellationToken)
        {
            var deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(1,
                _settings != null ? _settings.FirebaseTimeoutSeconds : 8)));
            return deadline;
        }

        private void DispatchOnMainThread(Action action)
        {
            if (_destroyed)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                action();
                return;
            }

            lock (_mainThreadActions)
            {
                if (!_destroyed)
                {
                    _mainThreadActions.Enqueue(action);
                }
            }
        }

        private void Update()
        {
            while (!_destroyed)
            {
                Action action;
                lock (_mainThreadActions)
                {
                    if (_mainThreadActions.Count == 0)
                    {
                        return;
                    }

                    action = _mainThreadActions.Dequeue();
                }

                action();
            }
        }
    }
}
