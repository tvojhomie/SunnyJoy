#if UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;

[InitializeOnLoad]
internal static class UniWebViewAuthenticationSessionEditorCleanup {

    static UniWebViewAuthenticationSessionEditorCleanup() {
        EditorApplication.playModeStateChanged += InvalidateNativeSessionsOnPlayModeExit;
    }

    private static void InvalidateNativeSessionsOnPlayModeExit(PlayModeStateChange state) {
        if (state != PlayModeStateChange.ExitingPlayMode) {
            return;
        }
        try {
            uv_authenticationInvalidateAll();
        } catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
            // The macOS bundle is missing from the project, or an older bundle without this
            // entry point is still loaded in the editor. There is nothing to clean up then.
        }
    }

    [DllImport("UniWebView")]
    private static extern void uv_authenticationInvalidateAll();
}
#endif
