using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppsFlyerSDK;
using Firebase.Messaging;
using JetBrains.Annotations;
using Unity.Advertisement.IosSupport;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class Loader : MonoBehaviour, IAppsFlyerConversionData
{
    private const int DEFAULT_WEBREQUEST_TIMEOUT = 30;
    private enum LaunchMode
    {
        None,
        Basic,
        Promotional,
        PromotionalPush
    }
    
    private enum LaunchStage
    {
        WaitForConversion,
        WaitForConversionVerify,
        WaitForPromotional,
        WaitForPushDecision,
        Launch
    }
    
    private string DictToJson(Dictionary<string, object> dictionary) => 
        "{" + string.Join(",", dictionary.Select(kvp => kvp.Value != null ? 
            $"\"{kvp.Key}\":{(kvp.Value is string ? $"\"{kvp.Value}\"" : kvp.Value.ToString().ToLower())}" : $"\"{kvp.Key}\":null")) + "}";
    private static Rect FlipRectY(Rect rect) => new(rect.x, Screen.height - rect.yMax, rect.width, rect.height);
    
    
    [SerializeField] private string devKey;
    [SerializeField] [Tooltip("Only numbers allowed")] private string appleID;
    [SerializeField] private float maxAwaitTime = 10f;
    [SerializeField] [Tooltip("https://example.com")] private string analyticsURL = "";

    [SerializeField] private GameObject loadingScreenObject;
    [SerializeField] private GameObject pushDecisionScreenObject;
    [SerializeField] private GameObject networkInfoScreenObject;

    private LaunchMode _launchMode
    {
        get
        {
            __launchModeValue ??= (LaunchMode)PlayerPrefs.GetInt("_launchMode", 0);
            return __launchModeValue.Value;
        }
        set
        {
            __launchModeValue = value;
            if (value != LaunchMode.Basic) return;
            PlayerPrefs.SetInt("_launchMode", (int)value);
            PlayerPrefs.Save();
        }
    }
    private LaunchMode? __launchModeValue = null;

    private LaunchStage _launchStage = LaunchStage.WaitForConversion;
    
    private string _conversionString
    {
        get => PlayerPrefs.GetString("_conversionString", "");
        set
        {
            PlayerPrefs.SetString("_conversionString", value);
            PlayerPrefs.Save();
        }
    }
    private Dictionary<string, object> _conversionDictionary = new Dictionary<string, object>();

    private string _promotionalURL
    {
        get => PlayerPrefs.GetString("_promotionalURL", "");
        set
        {
            PlayerPrefs.SetString("_promotionalURL", value);
            PlayerPrefs.Save();
        }
    }
    private string _oneTimePromotionalURL = null;
    private DateTime _lastPushDecisionDateTime
    {
        get => DateTime.FromBinary(long.Parse(PlayerPrefs.GetString("_lastPushDecisionDateTime", "0")));
        set
        {
            PlayerPrefs.SetString("_lastPushDecisionDateTime", value.ToBinary().ToString());
            PlayerPrefs.Save();
        }
    }
    
    
    private bool _appHaveNotificationsPermission
    {
        get => bool.Parse(PlayerPrefs.GetString("_appHaveNotificationsPermission", false.ToString()));
        set
        {
            if (!_appHaveNotificationsPermission && value)
            {
                PlayerPrefs.SetString("_appHaveNotificationsPermission", true.ToString());
                PlayerPrefs.Save();
                AppsFlyer.sendEvent("app_notifications_attempt",
                    new Dictionary<string, string>() { { "result", _appHaveNotificationsPermission.ToString() } });
            }
        }
    }

    private Coroutine _launchViewRoutine;
    private string _firebaseToken;
    
    private void Awake()
    {
        UniWebView.SetAllowAutoPlay(true);
        UniWebView.SetAllowInlinePlay(true);
        UniWebView.SetEnableKeyboardAvoidance(false);
        UniWebView.SetJavaScriptEnabled(true);
        UniWebView.SetForwardWebConsoleToNativeOutput(true);
        if (_appHaveNotificationsPermission)
        {
            FirebaseMessaging.MessageReceived += OnMessageReceived;
        }
    }
    
    private IEnumerator Start()
    {
        if (Application.internetReachability is NetworkReachability.NotReachable)
        {
            networkInfoScreenObject.SetActive(true);
            yield break;
        }
        if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
            ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            ATTrackingStatusBinding.RequestAuthorizationTracking();
        if (_launchMode is LaunchMode.None)
        {
            yield return LoadAndVerifyAppsFlyer();
            if (_launchStage is LaunchStage.WaitForPromotional)
                yield return TryGetPromotional();
            _launchMode = !string.IsNullOrEmpty(_promotionalURL) 
                ? LaunchMode.Promotional 
                : LaunchMode.Basic;
            _launchStage = DateTime.Now - _lastPushDecisionDateTime > TimeSpan.FromDays(3) &&
                           !_appHaveNotificationsPermission &&
                           _launchMode is LaunchMode.Promotional
                ? LaunchStage.WaitForPushDecision
                : LaunchStage.Launch;
            pushDecisionScreenObject.SetActive(_launchStage is LaunchStage.WaitForPushDecision);
            yield return new WaitWhile(() => _launchStage is LaunchStage.WaitForPushDecision);
        }
        _oneTimePromotionalURL = string.IsNullOrEmpty(_oneTimePromotionalURL) 
            ? _promotionalURL 
            : _oneTimePromotionalURL;
        switch (_launchMode)
        {
            case LaunchMode.PromotionalPush:
                FirebaseMessaging.TokenReceived += OnTokenReceived;
                FirebaseMessaging.MessageReceived += OnMessageReceived;
                yield return InstantiateWebviewAndRun(_oneTimePromotionalURL);
                break;
            case LaunchMode.Promotional:
                yield return InstantiateWebviewAndRun(_oneTimePromotionalURL);
                break;
            case LaunchMode.Basic or LaunchMode.None:
                SceneManager.LoadScene(1);
                AppsFlyer.sendEvent("app_loading_game_scene", new Dictionary<string, string>());
                break;
        }
    }

    private IEnumerator TryUpdateFirebaseTokenAndGetPromotional()
    {
        if (!string.IsNullOrEmpty(_firebaseToken)) _conversionDictionary.TryAdd("push_token", _firebaseToken);
        if (!string.IsNullOrEmpty(_firebaseToken)) _conversionDictionary.TryAdd("firebase_project_id", Firebase.FirebaseApp.DefaultInstance.Options.ProjectId);
        yield return TryGetPromotional();
    }

    private IEnumerator TryGetPromotional()
    {
        _conversionDictionary.TryAdd("bundle_id", Application.identifier);
        _conversionDictionary.TryAdd("store_id", appleID);
        _conversionDictionary.TryAdd("af_id", AppsFlyer.getAppsFlyerId());
        _conversionDictionary.TryAdd("locale", CultureInfo.CurrentCulture.Name);
        _conversionDictionary.TryAdd("os", "iOS");
        yield return PromotionalURLRequest(DictToJson(_conversionDictionary));
    }
    
    private IEnumerator PromotionalURLRequest(string json)
    {
        using var webRequest = UnityWebRequest.Post($"{analyticsURL.Trim().TrimEnd('/')}/config.php", json, "application/json");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.timeout = DEFAULT_WEBREQUEST_TIMEOUT;
        yield return webRequest.SendWebRequest();
        if (webRequest.responseCode == 200 &&
            AppsFlyer.CallbackStringToDictionary(webRequest.downloadHandler.text).TryGetValue("ok", out var okValue) &&
            (bool)okValue)
        {
            _promotionalURL =
                AppsFlyer.CallbackStringToDictionary(webRequest.downloadHandler.text).TryGetValue("url", out var value)
                    ? (string)value
                    : _promotionalURL;
        }
    }
    
    private IEnumerator TryGetOutOfViewList(Action<List<string>> callback)
    {
        using var webRequest = UnityWebRequest.Get($"{analyticsURL.Trim().TrimEnd('/')}/sites");       
        webRequest.timeout = DEFAULT_WEBREQUEST_TIMEOUT;
        yield return webRequest.SendWebRequest();
        var result = new List<string>();
        if (webRequest.responseCode == 200 && !string.IsNullOrEmpty(webRequest.downloadHandler.text))
        {
            result.AddRange(webRequest.downloadHandler.text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(line => line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries),
                    (_, site) => site.Trim()).Where(trimmed => !string.IsNullOrEmpty(trimmed)));
        }
        callback?.Invoke(result);
    }
    
    private IEnumerator SetUserAgent(UniWebView view)
    {
        yield return new WaitWhile(() => view is null);
        view.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1");
    }
    
    private IEnumerator InstantiateWebviewAndRun(string launchURL)
    {
        var view = gameObject.AddComponent<UniWebView>();
        var outOfViewList = new List<string>();
        yield return TryGetOutOfViewList(result => outOfViewList = result);
        StartCoroutine( SetUserAgent(view));
        view.EmbeddedToolbar.Hide();
        view.RegisterOnRequestMediaCapturePermission(permission => UniWebViewMediaCapturePermissionDecision.Grant); 
        view.SetSupportMultipleWindows(false, true);
        view.SetAllowFileAccess(true);
        view.SetCalloutEnabled(true);
        view.SetBackButtonEnabled(true);
        view.SetAllowBackForwardNavigationGestures(true);
        view.SetAcceptThirdPartyCookies(true);
        
        view.OnShouldClose += webView => false;
        view.OnPageFinished += (webView, code, url) =>
        {
            loadingScreenObject.SetActive(false);
            view.Frame = FlipRectY(Screen.safeArea);
            view.Show();
        };
        view.OnMessageReceived += (v, message) => {
            var url = message.RawMessage;
            Application.OpenURL(url);
        };
        view.OnPageStarted += (webView, url) =>
        {
            if (outOfViewList.Count != 0 && outOfViewList.Any(url.Contains))
            {
                Application.OpenURL(url);
                webView.GoBack();
            }
        };
        view.OnLoadingErrorReceived += (webView, code, message, payload) =>
        {
            //prevent crash on ssl failure
            if (code is -10 &&
                payload.Extra != null &&
                payload.Extra.TryGetValue(UniWebViewNativeResultPayload.ExtraFailingURLKey, out var value1))
            {
                webView.GoBack();
                Application.OpenURL((string)value1);
            }
            //prevent freeze on redirect limit
            if (code is -1007 or -9 or 0 &&
                payload.Extra != null &&
                payload.Extra.TryGetValue(UniWebViewNativeResultPayload.ExtraFailingURLKey, out var value))
            {
                webView.Load((string)value);
            }
        };
        view.OnOrientationChanged += (webView, orientation) =>
        {
            webView.Frame = FlipRectY(Screen.safeArea);
        };
        view.OnMultipleWindowOpened += (webView, id) =>
        {
            webView.Frame = FlipRectY(Screen.safeArea);
            webView.ScrollTo(0, 0, false);
        };
        view.Load(launchURL);
    }
    
    private void OnTokenReceived(object sender, TokenReceivedEventArgs token) 
    {
        _firebaseToken = token.Token;
        StartCoroutine(TryUpdateFirebaseTokenAndGetPromotional());
    }
    
    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        if (e.Message.NotificationOpened && e.Message.Data.TryGetValue("url", out var messageUrl))
        {
            if (_launchStage is LaunchStage.Launch && TryGetComponent(typeof(UniWebView), out var view))
            {
                loadingScreenObject.SetActive(true);
                if (_launchViewRoutine != null) StopCoroutine(_launchViewRoutine);
                Destroy(view);
                _launchViewRoutine = StartCoroutine(InstantiateWebviewAndRun(messageUrl));
            }
            _launchMode = LaunchMode.PromotionalPush;
            _launchStage = LaunchStage.Launch;
            _oneTimePromotionalURL = messageUrl;
            if (!string.IsNullOrEmpty(_oneTimePromotionalURL)) AppsFlyer.sendEvent("app_opened_via_push",
                new Dictionary<string, string>() { { "url", _oneTimePromotionalURL } });
        }
    }

    private void OnDeepLink(object sender, EventArgs eventArgs)
    {
        var deepLinkEventArgs = eventArgs as DeepLinkEventsArgs;
        switch (deepLinkEventArgs!.status)
        {
            case DeepLinkStatus.FOUND:
                Debug.Log("Found deeplink");
                AppsFlyer.AFLog("OnDeepLink",
                    deepLinkEventArgs.isDeferred() ? "This is a deferred deep link" : "This is a direct deep link");

                // deepLinkParamsDictionary contains all the deep link parameters as keys
                var deepLinkParamsDictionary = new Dictionary<string, object>();
#if UNITY_IOS && !UNITY_EDITOR
              if (deepLinkEventArgs.deepLink.ContainsKey("click_event") && deepLinkEventArgs.deepLink["click_event"] != null)
              {
                  deepLinkParamsDictionary = deepLinkEventArgs.deepLink["click_event"] as Dictionary<string, object>;
              }
#elif UNITY_ANDROID && !UNITY_EDITOR
                  Debug.Log("Load deeplink values");
                  deepLinkParamsDictionary = deepLinkEventArgs.deepLink;
#endif                
                _conversionDictionary = 
                    _conversionDictionary
                        .Concat(deepLinkParamsDictionary)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                break;
            case DeepLinkStatus.NOT_FOUND:
                AppsFlyer.AFLog("OnDeepLink", "Deep link not found");
                break;
            case DeepLinkStatus.ERROR:
            default:
                AppsFlyer.AFLog("OnDeepLink", "Deep link error");
                break;
        }
    } 

    private IEnumerator LoadAndVerifyAppsFlyer()
    {
        var launchTimestamp = Time.time;
        var isFirstLaunch = string.IsNullOrEmpty(_conversionString);
        AppsFlyer.initSDK(devKey, appleID, this);        
        AppsFlyer.OnDeepLinkReceived += OnDeepLink;        
        AppsFlyer.startSDK();
        yield return new WaitForSeconds(1f); //deep link callback delay
        yield return new WaitWhile(() => string.IsNullOrEmpty(_conversionString) && Time.time - launchTimestamp < maxAwaitTime);                
        _conversionDictionary = string.IsNullOrEmpty(_conversionString)
            ? _conversionDictionary
            : _conversionDictionary
                .Concat(AppsFlyer.CallbackStringToDictionary(_conversionString))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);                                
        if (_conversionDictionary!.Count == 0)
        {
            _launchMode = LaunchMode.Basic;
            _launchStage = LaunchStage.Launch;
            yield break;
        }                
        _launchStage = LaunchStage.WaitForConversionVerify;
        if (_conversionDictionary.ContainsKey("af_status") &&
            _conversionDictionary["af_status"].ToString().ToLower() == "organic" && isFirstLaunch)
        {
            yield return new WaitForSeconds(5f);            
            using var webRequest =
                UnityWebRequest.Get(
                    $"https://gcdsdk.appsflyer.com/install_data/v4.0/id{appleID}?devkey={devKey}&device_id={AppsFlyer.getAppsFlyerId()}");
            webRequest.timeout = DEFAULT_WEBREQUEST_TIMEOUT;            
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                _conversionString = webRequest.downloadHandler.text;
                var newDict = AppsFlyer.CallbackStringToDictionary(_conversionString);
                foreach (var kvp in newDict)
                {
                    _conversionDictionary[kvp.Key] = kvp.Value;
                }
            }
        }
        _launchStage = LaunchStage.WaitForPromotional;
    }
    
    public void EnablePush(bool decision)
    {
        _lastPushDecisionDateTime = DateTime.Now;
        _launchMode = decision ? LaunchMode.PromotionalPush : LaunchMode.Promotional;
        if (!_appHaveNotificationsPermission && _launchMode is LaunchMode.PromotionalPush)
            _appHaveNotificationsPermission = true;
        _launchStage = LaunchStage.Launch;
        pushDecisionScreenObject.SetActive(false);
    }
    
    public void onConversionDataSuccess(string conversionData)
    {
        if (string.IsNullOrEmpty(_conversionString))
            _conversionString = conversionData;
    }

    public void onConversionDataFail(string error)
    {
        _launchStage = LaunchStage.Launch;
    }

    public void onAppOpenAttribution(string attributionData)
    {
        throw new NotImplementedException();
    }

    public void onAppOpenAttributionFailure(string error)
    {
        throw new NotImplementedException();
    }
}
