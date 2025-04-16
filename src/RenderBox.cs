using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.Disposables;
using SharpDX;
using System.ComponentModel;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform;

partial class RenderBox : Control
{
    private const int MinimumSizeLength = 150;
    private readonly ILogger<RenderBox> _logger = Program.ServiceProvider.GetRequiredService<ILogger<RenderBox>>();
    private readonly CancellationTokenSource _cts = new();
    private readonly GraphicsCaptureItem _captureItem;
    private bool _inRecalculateSize;

    public RenderBox(GraphicsCaptureItem captureItem)
    {
        _captureItem = captureItem;
        RecalculateSize(maintainImageSize: false);
        SetStyle(ControlStyles.Opaque, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.UserPaint, false);
        CaptureAndRenderLoop().NoAwait(_logger);
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
    private Vector2? _cachedImageSize;
    private Vector2 GetImageSize(bool maintainImageSize)
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
            _cachedImageSize = new((float)imageW, (float)imageH);
        }
        return _cachedImageSize.Value;

        Disposable TraceImageSize()
        {
            var initialSize = _cachedImageSize;
            return new(() =>
            {
                if (initialSize != _cachedImageSize)
                {
                    _logger.LogTrace($"Image size changed: {initialSize} -> {_cachedImageSize}");
                }
            });
        }
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.ResetTransform();
        if (!IsMultipleOf90(Angle))
        {
            using var pen = new Pen(System.Drawing.Color.Black, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height -1);
        }
        static bool IsMultipleOf90(double degrees)
        {
            var ratio = degrees / 90;
            return Math.Abs(ratio - Math.Round(ratio)) < 0.001;
        }
    }

    protected override void OnSizeChanged(EventArgs args) => RecalculateSize(maintainImageSize: false);

    private void RecalculateSize(bool maintainImageSize)
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
        GetGridSize(imageSize.X, imageSize.Y, Angle, out var gridW, out var gridH);
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
        var rad = MathUtil.DegreesToRadians((float)angle);
        rotatedW = Math.Abs(imageW * Math.Cos(rad)) + Math.Abs(imageH * Math.Sin(rad));
        rotatedH = Math.Abs(imageW * Math.Sin(rad)) + Math.Abs(imageH * Math.Cos(rad));
    }

    private static void GetImageSize(double gridW, double gridH, double angle,
        double imageWidthOverHeight, out double imageW, out double imageH)
    {
        // Convert angle from degrees to radians.
        // We only need the absolute values for the bounding box equations.
        var rad = MathUtil.DegreesToRadians((float)angle);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
        }
        base.Dispose(disposing);
    }
}
