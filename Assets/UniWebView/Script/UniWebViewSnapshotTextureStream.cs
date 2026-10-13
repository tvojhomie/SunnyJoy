//
//  UniWebViewSnapshotTextureStream.cs
//  Created by Wang Wei (@onevcat) on 2026-05-28.
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

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Represents a live snapshot texture stream owned by a <see cref="UniWebView"/>.
/// </summary>
/// <remarks>
/// The stream owns the texture it exposes. Assign <see cref="Texture"/> once when <see cref="IsReady"/> becomes
/// <c>true</c>, then call <see cref="Dispose"/> when the stream is no longer needed. Do not destroy the texture yourself.
/// </remarks>
public sealed class UniWebViewSnapshotTextureStream : IDisposable {

    private readonly UniWebView owner;
    private bool stopped;
    private bool readyActionInvoked;
    private Action<Texture2D> onReady;
    private IntPtr cpuFrameBuffer = IntPtr.Zero;
    private int cpuFrameBufferSize;

    internal UniWebViewSnapshotTextureStream(
        UniWebView owner,
        string webViewName,
        long streamId,
        Rect rect,
        float refreshInterval,
        Action<Texture2D> onReady
    ) {
        this.owner = owner;
        WebViewName = webViewName;
        StreamId = streamId;
        Rect = rect;
        RefreshInterval = refreshInterval;
        this.onReady = onReady;
    }

    /// <summary>
    /// The live texture updated in place by this stream. It is <c>null</c> until the first native frame is uploaded.
    /// </summary>
    public Texture2D Texture { get; private set; }

    /// <summary>
    /// Whether the stream has received its first usable native texture frame.
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// The current stream texture width in pixels.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// The current stream texture height in pixels.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// The local web view rectangle captured by this stream.
    /// </summary>
    public Rect Rect { get; }

    /// <summary>
    /// The latest uploaded frame index.
    /// </summary>
    public long FrameIndex { get; private set; }

    internal string WebViewName { get; }
    internal long StreamId { get; }
    internal float RefreshInterval { get; }
    internal bool IsStopped => stopped;

    /// <summary>
    /// Stops the stream and releases the stream-owned texture.
    /// </summary>
    public void Stop() {
        if (stopped) {
            return;
        }
        owner.StopSnapshotTextureStream(this);
    }

    /// <summary>
    /// Stops the stream and releases the stream-owned texture.
    /// </summary>
    public void Dispose() {
        Stop();
    }

    internal void MarkStoppedByOwner() {
        if (stopped) {
            return;
        }

        stopped = true;
        IsReady = false;
        Width = 0;
        Height = 0;
        FrameIndex = 0;
        onReady = null;

        if (Texture != null) {
            DestroyTexture(Texture);
            Texture = null;
        }

        if (cpuFrameBuffer != IntPtr.Zero) {
            Marshal.FreeHGlobal(cpuFrameBuffer);
            cpuFrameBuffer = IntPtr.Zero;
            cpuFrameBufferSize = 0;
        }
    }

    internal void UpdateTexture(IntPtr nativeTexturePtr, int width, int height, long frameIndex) {
        if (stopped || nativeTexturePtr == IntPtr.Zero || width <= 0 || height <= 0) {
            return;
        }

        if (Texture == null || Width != width || Height != height) {
            if (Texture != null) {
                DestroyTexture(Texture);
            }
            Texture = Texture2D.CreateExternalTexture(
                width, height, UniWebViewInterface.GetSnapshotTextureStreamTextureFormat(), false, false, nativeTexturePtr
            );
        } else {
            Texture.UpdateExternalTexture(nativeTexturePtr);
        }

        Width = width;
        Height = height;
        FrameIndex = frameIndex;
        IsReady = true;
        InvokeOnReadyIfNeeded();
    }

    // Returns a stream-owned unmanaged buffer sized for a width x height RGBA frame. The native
    // side copies the latest frame into it, which avoids exposing native-owned pixel memory whose
    // lifetime is not controlled by the main thread.
    internal IntPtr PrepareCpuFrameBuffer(int width, int height) {
        if (stopped || width <= 0 || height <= 0) {
            return IntPtr.Zero;
        }

        var size = width * height * 4;
        if (cpuFrameBuffer == IntPtr.Zero) {
            cpuFrameBuffer = Marshal.AllocHGlobal(size);
            cpuFrameBufferSize = size;
        } else if (cpuFrameBufferSize != size) {
            cpuFrameBuffer = Marshal.ReAllocHGlobal(cpuFrameBuffer, (IntPtr)size);
            cpuFrameBufferSize = size;
        }
        return cpuFrameBuffer;
    }

    internal void UpdateTextureWithCpuFrame(int width, int height, long frameIndex) {
        if (stopped || cpuFrameBuffer == IntPtr.Zero || width <= 0 || height <= 0) {
            return;
        }

        var size = width * height * 4;
        if (cpuFrameBufferSize != size) {
            return;
        }

        if (Texture == null || Width != width || Height != height) {
            if (Texture != null) {
                DestroyTexture(Texture);
            }
            Texture = new Texture2D(
                width, height, UniWebViewInterface.GetSnapshotTextureStreamTextureFormat(), false, false
            );
        }
        Texture.LoadRawTextureData(cpuFrameBuffer, size);
        Texture.Apply(false, false);

        Width = width;
        Height = height;
        FrameIndex = frameIndex;
        IsReady = true;
        InvokeOnReadyIfNeeded();
    }

    private void InvokeOnReadyIfNeeded() {
        if (readyActionInvoked || onReady == null) {
            return;
        }
        readyActionInvoked = true;
        var action = onReady;
        onReady = null;
        action(Texture);
    }

    private static void DestroyTexture(Texture2D texture) {
        if (Application.isPlaying) {
            Object.Destroy(texture);
        } else {
            Object.DestroyImmediate(texture);
        }
    }
}
