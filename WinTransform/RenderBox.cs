using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.Disposables;
using System.ComponentModel;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using WinTransform.Helpers;
using System.Diagnostics;

namespace WinTransform;

public class RenderBox : Control
{
    private const int MinimumSizeLength = 150;
    private readonly ILogger<RenderBox> _logger = Program.ServiceProvider.GetRequiredService<ILogger<RenderBox>>();
    private readonly CancellationTokenSource _cts = new();
    private readonly GraphicsCaptureItem _captureItem;
    private bool _inRecalculateSize;

    public RenderBox(GraphicsCaptureItem captureItem)
    {
        _captureItem = captureItem;
        RecalculateSize();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        SetStyle(ControlStyles.Opaque, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.UserPaint, false);
        CaptureLoop().NoAwait(_logger);
        base.OnHandleCreated(e);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Angle
    {
        get;
        set
        {
            field = value;
            RecalculateSize(maintainImageSize: true);
        }
    }

    // do not recompute ImageSize during rotations from grid bounds since it will lead to drift.
    record SizeFloat(double Width, double Height);
    private SizeFloat _cachedImageSize;
    private SizeFloat GetImageSize(bool maintainImageSize)
    {
        using var _ = TraceImageSize();
        if (!maintainImageSize)
        {
            _cachedImageSize = null;
        }
        if (_cachedImageSize == null)
        {
            GetImageSize(Width, Height, Angle,
                (double)_captureItem.Size.Width / _captureItem.Size.Height,
                out var imageW, out var imageH);
            _cachedImageSize = new(imageW, imageH);
        }
        return _cachedImageSize;

        Disposable TraceImageSize()
        {
            var initialSize = _cachedImageSize;
            return new(() =>
            {
                if (initialSize != _cachedImageSize)
                {
                    _logger.LogDebug($"Image size changed: {initialSize} -> {_cachedImageSize}");
                }
            });
        }
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.ResetTransform();
        if (!IsMultipleOf90(Angle))
        {
            using var pen = new Pen(Color.Black, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height -1);
        }
        static bool IsMultipleOf90(double degrees)
        {
            var ratio = degrees / 90;
            return Math.Abs(ratio - Math.Round(ratio)) < 0.001;
        }
    }

    protected override void OnSizeChanged(EventArgs args) => RecalculateSize();

    private void RecalculateSize(bool maintainImageSize = false)
    {
        using var _ = PreventRecursion(out var shouldReturn);
        if (shouldReturn)
        {
            return;
        }
        if (Width < MinimumSizeLength)
        {
            Width = MinimumSizeLength;
        }
        if (Height < MinimumSizeLength)
        {
            Height = MinimumSizeLength;
        }
        var imageSize = GetImageSize(maintainImageSize);
        GetGridSize(imageSize.Width, imageSize.Height, Angle, out var gridW, out var gridH);
        Width = (int)Math.Round(gridW);
        Height = (int)Math.Round(gridH);
        return;

        Disposable PreventRecursion(out bool shouldReturn)
        {
            if (_inRecalculateSize)
            {
                shouldReturn = true;
                return null;
            }
            _inRecalculateSize = true;
            shouldReturn = false;
            return new(() => _inRecalculateSize = false);
        }
    }

    private static void GetGridSize(double imageW, double imageH, double angle, out double rotatedW, out double rotatedH)
    {
        // The rotated bounding box for a w×h rectangle, rotated by θ, 
        // has width = |w cos θ| + |h sin θ|, height = |h cos θ| + |w sin θ|
        var rad = angle * Math.PI / 180.0;
        rotatedW = Math.Abs(imageW * Math.Cos(rad)) + Math.Abs(imageH * Math.Sin(rad));
        rotatedH = Math.Abs(imageW * Math.Sin(rad)) + Math.Abs(imageH * Math.Cos(rad));
    }

    private static void GetImageSize(double gridW, double gridH, double angle,
        double imageWidthOverHeight, out double imageW, out double imageH)
    {
        // Convert angle from degrees to radians.
        // We only need the absolute values for the bounding box equations.
        var rad = angle * Math.PI / 180.0;
        var absC = Math.Abs(Math.Cos(rad));
        var absS = Math.Abs(Math.Sin(rad));

        // Solve for imageH in two ways:
        var hFromGridW = gridW / (absC * imageWidthOverHeight + absS);
        var hFromGridH = gridH / (absS * imageWidthOverHeight + absC);

        // In perfect math, these should be identical. 
        // In practice, you can take the average or check for consistency:
        imageH = (hFromGridW + hFromGridH) / 2.0;
        // Then imageW follows from the ratio
        imageW = imageWidthOverHeight * imageH;
    }

    private async Task CaptureLoop()
    {
        while (true)
        {
            try
            {
                await CaptureTask(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(CaptureLoop));
                await Task.Delay(1000, _cts.Token);
            }
        }
    }

    private async Task CaptureTask(CancellationToken ct)
    {
        var initialCaptureSize = _captureItem.Size;
        var description = new SwapChainDescription
        {
            ModeDescription = new ModeDescription(Width, Height, Rational.Empty, Format.B8G8R8A8_UNorm),
            SampleDescription = new SampleDescription(1, 0),
            Usage = Usage.RenderTargetOutput,
            BufferCount = 1,
            OutputHandle = Handle,
            SwapEffect = SwapEffect.Discard,
            IsWindowed = true
        };
        SharpDX.Direct3D11.Device.CreateWithSwapChain(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            description,
            out var device,
            out var swapChain);
        using var _ = device;
        using var __ = swapChain;
        using var deviceContext = device.ImmediateContext;
        using var backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);

        using var graphicsDevice = Direct3D11Helper.AsGraphicsDevice(device);
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            graphicsDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            numberOfBuffers: 1,
            initialCaptureSize);
        var frameReady = new FrameReadyEvent();
        framePool.FrameArrived += (_, _) => frameReady.Set();
        using var session = framePool.CreateCaptureSession(_captureItem);
        session.IsCursorCaptureEnabled = false;
        session.IsBorderRequired = false;
        session.StartCapture();

        while (true)
        {
            await frameReady.WaitAsync(ct);
            using var frame = LatestFrameOrDefault(framePool);
            if (frame == null)
            {
                continue;
            }
            Trace.Assert(frame.ContentSize == _captureItem.Size);
            if (_captureItem.Size != initialCaptureSize)
            {
                throw new FrameSizeChangedException();
            }
            using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
            deviceContext.CopyResource(bitmap, backBuffer);
            swapChain.Present(0, PresentFlags.None);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
        }
        base.Dispose(disposing);
    }
}

class FrameSizeChangedException : Exception { }
class FrameReadyEvent
{
    private volatile TaskCompletionSource _tcs = new();
    public async Task WaitAsync(CancellationToken ct)
    {
        await _tcs.Task.WaitAsync(ct);
        _tcs = new();
    }
    public void Set() => _tcs.TrySetResult();
}
