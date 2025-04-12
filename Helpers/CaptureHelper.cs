// https://github.com/robmikh/ManagedScreenshotDemo
// MIT License
// 
// Copyright (c) 2022 Robert Mikhayelyan
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Nito.AsyncEx;
using Nito.Disposables;
using SharpDX.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace WinTransform.Helpers;

public class CaptureInfo
{
    public Action ProcessFrameCallback { get; set; }
    public Task CaptureTask { get; internal set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int Stride { get; internal set; }
    public IntPtr DataPointer { get; internal set; }
}

class FrameReadyEvent
{
    // Use TCS instead of Nito to avoid TaskCompletionOptions.RunContinuationsAsynchronously
    private volatile TaskCompletionSource _tcs = new();
    public async Task WaitAsync()
    {
        await _tcs.Task;
        _tcs = new();
    }
    public void Set() => _tcs.TrySetResult();
}

public class FrameSizeChangedException : Exception { }

public class CaptureHelper
{
    public static async Task<CaptureInfo> StartCapture(GraphicsCaptureItem item, CancellationToken ct)
    {
        var context = SynchronizationContext.Current ?? new();
        var infoReady = new AsyncManualResetEvent();
        var info = new CaptureInfo();
        info.CaptureTask = Task.Run(CaptureAsync, ct);
        await infoReady.WaitAsync(ct);
        return info;

        async Task CaptureAsync()
        {
            // Setup D3D
            using var device = Direct3D11Helper.CreateDevice();
            using var d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);
            using var d3dContext = d3dDevice.ImmediateContext;

            // Create our staging texture
            info.Width = item.Size.Width;
            info.Height = item.Size.Height;
            var description = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = info.Width,
                Height = info.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.Direct3D11.ResourceUsage.Staging,
                BindFlags = SharpDX.Direct3D11.BindFlags.None,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
            };
            using var stagingTexture = new SharpDX.Direct3D11.Texture2D(d3dDevice, description);
            var mapped = d3dContext.MapSubresource(stagingTexture, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            using var _ = new Disposable(() => d3dContext.UnmapSubresource(stagingTexture, 0));
            info.Stride = mapped.RowPitch;
            info.DataPointer = mapped.DataPointer;
            infoReady.Set();

            // Setup capture
            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                numberOfBuffers: 1,
                item.Size);
            var frameReady = new FrameReadyEvent();
            framePool.FrameArrived += (_, _) => frameReady.Set();
            using var session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            session.IsBorderRequired = false;
            session.StartCapture();
            while (true)
            {
                await frameReady.WaitAsync();
                using var frame = LatestFrameOrDefault(framePool);
                if (frame == null)
                {
                    continue;
                }
                if (frame.ContentSize != item.Size)
                {
                    throw new FrameSizeChangedException();
                }
                using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
                d3dContext.CopyResource(bitmap, stagingTexture);
                context.Send(() => info.ProcessFrameCallback?.Invoke());
            }
        }
    }

    private static Direct3D11CaptureFrame LatestFrameOrDefault(Direct3D11CaptureFramePool framePool)
    {
        Direct3D11CaptureFrame latestFrame = null;
        while (framePool.TryGetNextFrame() is { } frame)
        {
            latestFrame?.Dispose();
            latestFrame = frame;
        }
        return latestFrame;
    }
}
