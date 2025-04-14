using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Nito.Disposables;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using WinTransform.Render;

namespace WinTransform;

partial class RenderBox
{
    private async Task CaptureAndRenderLoop()
    {
        var context = SynchronizationContext.Current;
        while (true)
        {
            try
            {
                var handle = Handle;
                await Task.Run(() => CaptureAndRender(handle, CaptureSizeChanged), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(CaptureAndRenderLoop));
                await Task.Delay(1000, _cts.Token);
            }
        }

        void CaptureSizeChanged()
        {
            _logger.LogInformation("Capture size changed");
            context.Post(_ => RecalculateSize(maintainImageSize: false), null);
        }
    }

    private async Task CaptureAndRender(IntPtr handle, Action captureSizeChanged = null)
    {
        using var _ = TrackControlSize(out var sizeRecalculated);
        using var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug);
        using var capture = new CaptureSession(_captureItem, device);
        using var shaders = Shaders.Load(device);
        using var renderBuffer = new RenderBuffer(device, handle);
        using var inputBuffer = new InputBuffer(device, _logger);
        using var vertexCache = new VertexCache();
        var fpsCounter = new FpsCounter();
        var lastCaptureSize = _captureItem.Size;
        while (true)
        {
            await await Task.WhenAny(
                capture.WaitFrame(_cts.Token),
                sizeRecalculated.WaitAsync(_cts.Token));

            // Disposing asap seems to help
            using (var frame = capture.GetLatestFrame())
            {
                if (frame != null)
                {
                    if (frame.ContentSize != lastCaptureSize)
                    {
                        lastCaptureSize = frame.ContentSize;
                        captureSizeChanged?.Invoke();
                    }
                    inputBuffer.Update(frame);
                }
            }
            renderBuffer.UpdateSize(Size, _logger);
            vertexCache.Update(device, GetImageSize(maintainImageSize: true), Angle, _logger);
            device.ImmediateContext.Draw(vertexCache.Count, 0);
            renderBuffer.SwapChain.Present(0, PresentFlags.None);
            fpsCounter.TrackFps(_logger);
        }
    }

    Disposable TrackControlSize(out AsyncManualResetEvent sizeRecalculated)
    {
        // use manual reset, AutoResetEvent allocates on each wait
        var recalculatedEvent = new AsyncManualResetEvent();
        sizeRecalculated = recalculatedEvent;
        SizeRecalculated += OnRecalculated;
        return new(() => SizeRecalculated -= OnRecalculated);

        void OnRecalculated()
        {
            recalculatedEvent.Set();
            recalculatedEvent.Reset();
        }
    }
}
