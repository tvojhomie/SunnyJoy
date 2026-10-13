using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppsFlyerSDK;
using UnityEngine;

[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]

namespace AlienProject.WebWrapper.Integrations.AppsFlyer
{
    [RequireComponent(typeof(AppsFlyerSDK.AppsFlyer))]
    public sealed class AppsFlyerAttributionAdapter : MonoBehaviour, IAttributionProvider
    {
        private readonly object _payloadLock = new object();
        private readonly List<AttributionPayload> _payloads = new List<AttributionPayload>();
        private readonly TaskCompletionSource<bool> _conversionSignal = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> _deepLinkSignal = new TaskCompletionSource<bool>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private WebWrapperSettings _settings;
        private Task _initialization;
        private string _appsFlyerId = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBeforeSceneLoad()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);
            if (settings == null || settings.UseMockServices || string.IsNullOrWhiteSpace(settings.AppsFlyerDevKey))
            {
                return;
            }

            if (FindFirstObjectByType<AppsFlyerAttributionAdapter>() != null)
            {
                return;
            }

            var host = new GameObject("[WebWrapper.AppsFlyer]");
            DontDestroyOnLoad(host);
            host.AddComponent<AppsFlyerAttributionAdapter>();
#endif
        }

        private void Awake()
        {
            _settings = Resources.Load<WebWrapperSettings>(WebWrapperSettings.ResourceName);

            if (GetComponent<AppsFlyerSDK.AppsFlyer>() == null)
            {
                gameObject.AddComponent<AppsFlyerSDK.AppsFlyer>();
            }

            WebWrapperServices.AttributionProvider = this;
            WebWrapperDiagnostics.Record("AppsFlyer", "Production adapter registered.");
            _initialization = InitializeAsync();
        }

        private void OnDestroy()
        {
            _lifetime.Cancel();
            if (ReferenceEquals(WebWrapperServices.AttributionProvider, this))
            {
                WebWrapperServices.AttributionProvider = null;
            }
        }

        public async Task<AttributionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken, _lifetime.Token))
            {

                deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(1,
                    _settings != null ? _settings.AppsFlyerTimeoutSeconds : 7)));
                try
                {
                    await SdkTaskWaiter.WaitAsync(_initialization, deadline.Token);
                    await SdkTaskWaiter.WaitAsync(_conversionSignal.Task, deadline.Token);

                    if (_settings != null &&
                        _settings.DeepLinkGraceMilliseconds > 0)
                    {
                        var grace = Task.Delay(_settings.DeepLinkGraceMilliseconds, deadline.Token);
                        await SdkTaskWaiter.WaitAsync(Task.WhenAny(_deepLinkSignal.Task, grace), deadline.Token);
                    }
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
                {

                }
            }

            List<AttributionPayload> snapshot;
            lock (_payloadLock)
            {
                snapshot = new List<AttributionPayload>(_payloads);
            }

            return new AttributionSnapshot(_appsFlyerId, snapshot);
        }

        private async Task InitializeAsync()
        {
            if (_settings == null || string.IsNullOrWhiteSpace(_settings.AppsFlyerDevKey))
            {
                _conversionSignal.TrySetResult(false);
                return;
            }

            try
            {
                await AppsFlyerSDK.AppsFlyer.enableDebug(_settings.AppsFlyerDebugLogging);
                await AppsFlyerSDK.AppsFlyer.setCurrencyCode("USD");
#if UNITY_IOS && !UNITY_EDITOR

                AppsFlyerSDK.AppsFlyer.CallBackObjectName = gameObject.name;
                var devKey = _settings.AppsFlyerDevKey;
                var appId = _settings.AppsFlyerIosAppId;
                await SdkTaskWaiter.WaitAsync(Task.Run(async () =>
                    await AppsFlyerSDK.AppsFlyer.init(devKey, appId)), _lifetime.Token);
#else
                await AppsFlyerSDK.AppsFlyer.init(
                    _settings.AppsFlyerDevKey,
                    _settings.AppsFlyerIosAppId,
                    GetComponent<AppsFlyerSDK.AppsFlyer>());
#endif
                _lifetime.Token.ThrowIfCancellationRequested();
                await AppsFlyerSDK.AppsFlyer.registerConversionListener(
                    OnConversionDataSuccess,
                    OnConversionDataFailure);
                await AppsFlyerSDK.AppsFlyer.registerDeepLinkListener(OnDeepLinkReceived);
                await AppsFlyerSDK.AppsFlyer.start();
                _appsFlyerId = await AppsFlyerSDK.AppsFlyer.getAppsFlyerUID();
                WebWrapperDiagnostics.Record("AppsFlyer", "SDK started; UID received=" + !string.IsNullOrEmpty(_appsFlyerId));
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                _conversionSignal.TrySetResult(false);
            }
            catch (Exception exception)
            {
                Debug.LogError("[WebWrapper.AppsFlyer] Initialization failed: " + exception.Message);
                _conversionSignal.TrySetResult(false);
            }
        }

        private void OnConversionDataSuccess(string json)
        {
            try
            {
                AddPayload(AppsFlyerSDK.AppsFlyer.CallbackStringToDictionary(json));
                WebWrapperDiagnostics.Record("AppsFlyer", "Conversion callback received successfully.");
                _conversionSignal.TrySetResult(true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WebWrapper.AppsFlyer] Invalid conversion payload: " + exception.Message);
                _conversionSignal.TrySetResult(false);
            }
        }

        private void OnConversionDataFailure(string error)
        {
            Debug.LogWarning("[WebWrapper.AppsFlyer] Conversion data failed: " + error);
            _conversionSignal.TrySetResult(false);
        }

        private void OnDeepLinkReceived(DeepLinkEventsArgs args)
        {
            if (args != null && args.status == DeepLinkStatus.FOUND)
            {
                AddPayload(args.getDeepLinkDictionary());
            }

            _deepLinkSignal.TrySetResult(true);
        }

        private void AddPayload(Dictionary<string, object> values)
        {
            if (values == null || _lifetime.IsCancellationRequested)
            {
                return;
            }

            lock (_payloadLock)
            {
                _payloads.Add(new AttributionPayload(new Dictionary<string, object>(values)));
            }
        }
    }
}
