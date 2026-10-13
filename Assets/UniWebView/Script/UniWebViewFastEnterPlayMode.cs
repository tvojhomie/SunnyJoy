#if UNITY_EDITOR
using UnityEditor;

public partial class UniWebView {

    internal static void ResetGlobalPayloadActionsForFastEnterPlayMode() {
        globalPayloadActions.Clear();
    }
}

[InitializeOnLoad]
internal static class UniWebViewFastEnterPlayMode {

    // This keeps the editor-only callback class discoverable in the runtime assembly.
    static UniWebViewFastEnterPlayMode() {}

    [InitializeOnEnterPlayMode]
    private static void ResetStaticState(EnterPlayModeOptions options) {
        if ((options & EnterPlayModeOptions.DisableDomainReload) == 0) {
            return;
        }

        UniWebViewNativeListener.ResetListenersForFastEnterPlayMode();
        UniWebView.ResetGlobalPayloadActionsForFastEnterPlayMode();
        UniWebViewChannelMethodManager.ResetForFastEnterPlayMode();
    }
}
#endif
