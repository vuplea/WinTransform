namespace WinTransform;

static class SnapHelper
{
    private const int SnapDistance = 15;

    public static Rectangle ApplySnapping(Rectangle bounds, Size clientSize)
    {
        // Snap left
        if (Math.Abs(bounds.Left - 0) <= SnapDistance)
            bounds.X = 0;

        // Snap right
        int rightDelta = clientSize.Width - bounds.Right;
        if (Math.Abs(rightDelta) <= SnapDistance)
            bounds.X = clientSize.Width - bounds.Width;

        // Snap top
        if (Math.Abs(bounds.Top - 0) <= SnapDistance)
            bounds.Y = 0;

        // Snap bottom
        int bottomDelta = clientSize.Height - bounds.Bottom;
        if (Math.Abs(bottomDelta) <= SnapDistance)
            bounds.Y = clientSize.Height - bounds.Height;

        return bounds;
    }
}