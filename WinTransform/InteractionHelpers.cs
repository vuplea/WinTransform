namespace WinTransform;

static class InteractionHelpers
{
    public static Rectangle ApplySnapping(Rectangle bounds, Size clientSize)
    {
        const int SnapDistance = 15;

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

    public static void IsNearEdges(
        Point point,
        Size size,
        int edgeZoneIn,
        int edgeZoneOut,
        out bool nearLeft,
        out bool nearRight,
        out bool nearTop,
        out bool nearBottom)
    {
        nearLeft = point.X <= edgeZoneIn && point.X >= -edgeZoneOut;
        nearRight = point.X >= size.Width - edgeZoneIn && point.X <= size.Width + edgeZoneOut;
        nearTop = point.Y <= edgeZoneIn && point.Y >= -edgeZoneOut;
        nearBottom = point.Y >= size.Height - edgeZoneIn && point.Y <= size.Height + edgeZoneOut;
    }

}