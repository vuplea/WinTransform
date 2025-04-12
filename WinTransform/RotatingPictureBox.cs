using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.Disposables;
using System.ComponentModel;
using WinTransform;

public class RotatingPictureBox : Control
{
    private const int MinimumSizeLength = 150;
    private readonly ILogger<RotatingPictureBox> _logger = Program.ServiceProvider.GetRequiredService<ILogger<RotatingPictureBox>>();
    private bool _inRecalculateSize;

    public RotatingPictureBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        RecalculateSize();
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image Image
    {
        get;
        set
        {
            field = value;
            RecalculateSize();
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
            GetImageSize(Width, Height, Angle, (double)Image.Width / Image.Height, out var imageW, out var imageH);
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
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        e.Graphics.Clear(BackColor);
        if (Image == null)
        {
            return;
        }
        e.Graphics.TranslateTransform(Width / 2f, Height / 2f);
        e.Graphics.RotateTransform((float)Angle);
        //.Graphics.TranslateTransform(-Width / 2f, -Height / 2f);
        var imageSize = GetImageSize(maintainImageSize: true);
        e.Graphics.DrawImage(Image, new Rectangle((int)-imageSize.Width/2, (int)-imageSize.Height/2, (int)imageSize.Width, (int)imageSize.Height));
        DrawBorder();
        return;

        void DrawBorder()
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
    }

    protected override void OnSizeChanged(EventArgs args) => RecalculateSize();

    private void RecalculateSize(bool maintainImageSize = false)
    {
        using var __ = InvalidateIfNeeded();
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
        if (Image == null)
        {
            return;
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
        Disposable InvalidateIfNeeded()
        {
            var initialWidth = Width;
            var initialHeight = Height;
            return new Disposable(() =>
            {
                if (Width != initialWidth || Height != initialHeight)
                {
                    Invalidate();
                }
            });
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
}
