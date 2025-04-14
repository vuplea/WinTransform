using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WinTransform;

/// <summary>
/// Handles the logic for DRAGGING the PictureBox around.
/// </summary>
class DragHandler : InteractionHandler
{
    public DragHandler(RenderBox picture, RenderForm renderForm) :
        base(picture, renderForm, Program.ServiceProvider.GetRequiredService<ILogger<DragHandler>>())
    { }

    public override bool CanBeActive() =>
        Dragging || Picture.ClientRectangle.Contains(PictureMouseState.Location);

    protected override void OnDrag()
    {
        var currentPoint = RenderForm.MouseState.Location;

        var dx = currentPoint.X - DragStartInfo.MouseDownPoint.X;
        var dy = currentPoint.Y - DragStartInfo.MouseDownPoint.Y;

        var newLoc = new Point(
            DragStartInfo.OriginalBounds.X + dx,
            DragStartInfo.OriginalBounds.Y + dy);

        var newBounds = new Rectangle(newLoc, DragStartInfo.OriginalBounds.Size);
        newBounds = ApplySnapping(newBounds, RenderForm.ClientSize);
        Picture.Bounds = newBounds;

        Logger.LogDebug($"dx={dx}, dy={dy}, loc=({newLoc.X},{newLoc.Y})");
    }

    private static Rectangle ApplySnapping(Rectangle bounds, Size parentSize)
    {
        const int SnapDistance = 15;

        // Snap left
        if (Math.Abs(bounds.Left - 0) <= SnapDistance)
            bounds.X = 0;

        // Snap right
        int rightDelta = parentSize.Width - bounds.Right;
        if (Math.Abs(rightDelta) <= SnapDistance)
            bounds.X = parentSize.Width - bounds.Width;

        // Snap top
        if (Math.Abs(bounds.Top - 0) <= SnapDistance)
            bounds.Y = 0;

        // Snap bottom
        int bottomDelta = parentSize.Height - bounds.Bottom;
        if (Math.Abs(bottomDelta) <= SnapDistance)
            bounds.Y = parentSize.Height - bounds.Height;

        return bounds;
    }
}
