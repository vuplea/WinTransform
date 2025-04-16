using Microsoft.Extensions.Logging;
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
            try
            {
                await fpsCounter.AddFraction("WaitFrame", () =>
                    capture.WaitFrame(_cts.Token).WaitAsync(TimeSpan.FromMilliseconds(5)));
            }
            catch (TimeoutException) { }

            // Disposing asap might help
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
            fpsCounter.AddFraction("Draw", () => device.ImmediateContext.Draw(vertexCache.Count, 0));
            fpsCounter.AddFraction("Present", () => renderBuffer.SwapChain.Present(0, PresentFlags.AllowTearing));
            fpsCounter.TrackFps(_logger);
        }
    }
}
