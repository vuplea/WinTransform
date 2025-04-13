﻿using SharpDX.Direct3D11;
using System.Diagnostics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace WinTransform.Helpers;

public class CaptureSession : IDisposable
{
    // we want TaskCompletionOptions.RunContinuationsAsynchronously = False to process directly on capture thread
    private TaskCompletionSource _frameReady = new();
    private readonly GraphicsCaptureItem _captureItem;
    private readonly IDirect3DDevice _graphicsDevice;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;

    public Direct3D11CaptureFrame LatestFrame { get; internal set; }

    public CaptureSession(GraphicsCaptureItem captureItem, Device device)
    {
        _captureItem = captureItem;
        _graphicsDevice = Direct3D11Helper.AsGraphicsDevice(device);
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _graphicsDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            numberOfBuffers: 1,
            captureItem.Size);
        _framePool.FrameArrived += (_, _) => _frameReady.TrySetResult();
        _session = _framePool.CreateCaptureSession(_captureItem);
        _session.MinUpdateInterval = TimeSpan.FromMilliseconds(1);
        _session.IsBorderRequired = false;
        _session.StartCapture();
    }

    public async Task WaitFrame(CancellationToken ct = default)
    {
        await _frameReady.Task.WaitAsync(ct);
        _frameReady = new();
        while (_framePool.TryGetNextFrame() is { } frame)
        {
            LatestFrame?.Dispose();
            LatestFrame = frame;
        }
        Trace.Assert(LatestFrame != null);
    }

    public void Dispose()
    {
        LatestFrame?.Dispose();
        _session.Dispose();
        _framePool.Dispose();
        _graphicsDevice.Dispose();
    }
}
