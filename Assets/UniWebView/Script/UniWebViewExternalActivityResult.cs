//
//  UniWebViewExternalActivityResult.cs
//  Created by Wang Wei (@onevcat) on 2025-06-18.
//
//  This file is a part of UniWebView Project (https://uniwebview.com)
//  By purchasing the asset, you are allowed to use this code in as many as projects
//  you want, only if you publish the final products under the name of the same account
//  used for the purchase.
//
//  This asset and all corresponding files (such as source code) are provided on an
//  "as is" basis, without warranty of any kind, express of implied, including but not
//  limited to the warranties of merchantability, fitness for a particular purpose, and
//  noninfringement. In no event shall the authors or copyright holders be liable for any
//  claim, damages or other liability, whether in action of contract, tort or otherwise,
//  arising from, out of or in connection with the software or the use of other dealing in the software.
//

/// <summary>
/// The status of an external activity result on Android.
/// </summary>
public enum UniWebViewExternalActivityResultStatus {
    /// <summary>
    /// The external activity completed successfully and returned a result.
    /// </summary>
    Ok = -1,
    /// <summary>
    /// The external activity was canceled by the user or did not return a result.
    /// </summary>
    Canceled = 0
}

/// <summary>
/// Represents the result returned from an external Android activity that was launched from the web view.
///
/// When a web page in the web view navigates to a custom URL scheme (such as `upi://` for payment apps),
/// UniWebView launches the corresponding external app using Android's `startActivityForResult`. When that
/// app finishes and returns a result, this object carries the result data back to your Unity code.
///
/// This is only available on Android. On iOS and macOS, external app communication uses URL schemes and
/// deep links instead.
/// </summary>
public class UniWebViewExternalActivityResult {
    /// <summary>
    /// The original URL that triggered the external app launch (e.g., `upi://pay?pa=...`).
    /// </summary>
    public string Url { get; private set; }

    /// <summary>
    /// The result status returned by the external activity.
    /// </summary>
    public UniWebViewExternalActivityResultStatus Status { get; private set; }

    /// <summary>
    /// The raw integer result code returned by the external activity. This is the Android `resultCode`
    /// value. In most cases, use the `Status` property instead for a more readable representation.
    /// </summary>
    public int ResultCode { get; private set; }

    /// <summary>
    /// The data returned by the external activity, serialized as a JSON string.
    ///
    /// The JSON object contains all extras from the result Intent. For UPI payment apps, this typically
    /// includes a `"response"` key with a query-string-formatted value containing fields like `txnId`,
    /// `Status`, and `responseCode`.
    ///
    /// If the result Intent also carries a data URI, it is included under the `"__dataUri"` key.
    ///
    /// This value is an empty string if the external activity returned no data.
    /// </summary>
    public string Data { get; private set; }

    internal UniWebViewExternalActivityResult(string url, int resultCode, string data) {
        Url = url;
        ResultCode = resultCode;
        Status = resultCode == -1
            ? UniWebViewExternalActivityResultStatus.Ok
            : UniWebViewExternalActivityResultStatus.Canceled;
        Data = data;
    }
}
