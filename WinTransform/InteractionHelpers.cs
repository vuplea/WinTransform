namespace WinTransform;

static class InteractionHelpers
{
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