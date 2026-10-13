using System.Reflection;
using UnityEngine;

public class UniWebViewStaticListener {
    
    public static string InvokeStaticMethod(string method, string parameters)
    {
        MethodInfo methodInfo = typeof(UniWebViewStaticListener)
            .GetMethod(method, BindingFlags.Static | BindingFlags.Public);
        if (methodInfo == null) {
            UniWebViewLogger.Instance.Critical(
                "Cannot find the static method to invoke: " + method + ". Ignoring."
            );
            return null;
        }
        var result = methodInfo.Invoke(null, new object[] { parameters });
        return result as string;
    }
    
    public static void DebugLog(string value) {
        var payload = JsonUtility.FromJson<UniWebViewNativeResultPayload>(value);
        var description = UniWebViewLogger.DescribePayload(payload.data);
        switch (payload.resultCode) {
            case "0":
                Debug.Log(description);
                break;
            case "1":
                Debug.Log(description);
                break;
            case "2":
                Debug.LogWarning(description);
                break;
            case "3":
                Debug.LogError(description);
                break;
            case "4":
                Debug.LogError(description);
                break;
            default:
                Debug.Log(description);
                break;
        }
    }

    public static void CookieOperation(string value) {
        var payload = JsonUtility.FromJson<UniWebViewNativeResultPayload>(value);
        UniWebView.InternalCookieOperation(payload);
    }
}