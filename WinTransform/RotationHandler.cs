using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WinTransform;

class RotationHandler : InteractionHandler
{
    private const double SnapThreshold = 1f;  // degrees within which we snap to multiples of 90

    private double _initialDraggingAngle;
    private double _initialPictureAngle;
    private Point _initialPictureCenter;

    public RotationHandler(RenderBox picture, RenderForm renderForm)
        : base(picture, renderForm, Program.ServiceProvider.GetRequiredService<ILogger<RotationHandler>>())
    {
        // Hook the PictureBox paint event so we can rotate during drawing.
    }

    public override bool CanBeActive()
    {
        if (Dragging)
            return true;

        InteractionHelpers.IsNearEdges(
            PictureMouseState.Location,
            Picture.Size,
            edgeZoneIn: 0,
            edgeZoneOut: 25,
            out var nearLeft,
            out var nearRight,
            out var nearTop,
            out var nearBottom);
        var canBeActive = new bool[] { nearLeft, nearRight, nearTop, nearBottom }.Count(b => b) == 1;
        UpdateCursor(canBeActive);
        return canBeActive;
    }

    /// <summary>
    /// Start dragging, record initial angle.
    /// </summary>
    protected override void OnStartDragging()
    {
        _initialPictureAngle = Picture.Angle;
        _initialPictureCenter = new Point(Picture.Width / 2, Picture.Height / 2);
        _initialDraggingAngle = GetDraggingAngle();
    }

    protected override void OnDrag()
    {
        var currentDraggingAngle = GetDraggingAngle();
        var delta = currentDraggingAngle - _initialDraggingAngle;
        var newPictureAngle = _initialPictureAngle + delta;
        newPictureAngle = SnapAngle(newPictureAngle, 30f, SnapThreshold);
        Picture.Angle = newPictureAngle;
        Logger.LogDebug($"OnDrag: newAngle={newPictureAngle:F2}, delta={delta:F2}");
    }

    private double GetDraggingAngle()
    {
        var mouse = PictureMouseState.Location;
        var dx = mouse.X - _initialPictureCenter.X;
        var dy = mouse.Y - _initialPictureCenter.Y;
        return Math.Atan2(dy, dx) * 180.0 / Math.PI;
    }

    // Use a "hand" or custom cursor if in the rotation zone
    private static void UpdateCursor(bool inRotationZone) =>
        Cursor.Current = inRotationZone ? Cursors.Hand : Cursors.Default;

    /// <summary>
    /// Snap an angle to multiples of 'step' if within a specified threshold.
    /// Example: step = 90°, threshold = 5° => snap angles near 0°, 90°, 180°, 270°, ...
    /// </summary>
    private static double SnapAngle(double angle, double step, double threshold)
    {
        // Find the nearest multiple of step
        var nearestMultiple = step * Math.Round(angle / step);
        // If within threshold, snap
        return Math.Abs(angle - nearestMultiple) <= threshold
            ? nearestMultiple
            : angle;
    }
}
