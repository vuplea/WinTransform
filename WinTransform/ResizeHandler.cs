using System.Diagnostics;

namespace WinTransform;

/// <summary>
/// Handles the logic for RESIZING (with aspect ratio).
/// </summary>
class ResizeHandler : InteractionHandler
{
    private const int EdgeZoneIn = 10;
    private const int EdgeZoneOut = 5;

    private ResizeHandle? DraggingHandle => DragStartInfo?.Get<ResizeHandle>();

    public ResizeHandler(PictureBox picture, RenderForm renderForm) : base(picture, renderForm) { }

    public override bool CanBeActive()
    {
        if (Dragging)
        {
            return true;
        }
        var handle = GetHandle();
        UpdateCursor(handle);
        return handle != ResizeHandle.None;
    }

    /// <summary>
    /// Figures out if the mouse is near an edge/corner => we can start resizing.
    /// </summary>
    protected override IEnumerable<object> AddDraggingData() => [GetHandle()];

    protected override void OnDrag()
    {
        // Perform the actual resize
        var currentPoint = RenderForm.MouseState.Location;

        var dx = currentPoint.X - DragStartInfo.MouseDownPoint.X;
        var dy = currentPoint.Y - DragStartInfo.MouseDownPoint.Y;

        var originalBounds = DragStartInfo.OriginalBounds;
        var newBounds = originalBounds;

        var aspect = (Picture.Image == null)
            ? 1.0f
            : (float)Picture.Image.Width / Picture.Image.Height;

        var newWidth = newBounds.Width;
        var newHeight = newBounds.Height;

        switch (DraggingHandle.Value)
        {
            case ResizeHandle.TopLeft:
                newWidth  = originalBounds.Width - dx;
                newHeight = (int)(newWidth / aspect);
                newBounds.X = originalBounds.X + dx;
                var dH_TL = newHeight - originalBounds.Height;
                newBounds.Y = originalBounds.Y - dH_TL;
                break;

            case ResizeHandle.TopRight:
                newWidth  = originalBounds.Width + dx;
                newHeight = (int)(newWidth / aspect);
                var dH_TR = newHeight - originalBounds.Height;
                newBounds.Y = originalBounds.Y - dH_TR;
                break;

            case ResizeHandle.BottomLeft:
                newWidth  = originalBounds.Width - dx;
                newHeight = (int)(newWidth / aspect);
                newBounds.X = originalBounds.X + dx;
                break;

            case ResizeHandle.BottomRight:
                newWidth  = originalBounds.Width + dx;
                newHeight = (int)(newWidth / aspect);
                break;

            case ResizeHandle.Left:
                newWidth  = originalBounds.Width - dx;
                newHeight = (int)(newWidth / aspect);
                newBounds.X = originalBounds.X + dx;
                break;

            case ResizeHandle.Right:
                newWidth  = originalBounds.Width + dx;
                newHeight = (int)(newWidth / aspect);
                break;

            case ResizeHandle.Top:
                newHeight = originalBounds.Height - dy;
                newWidth  = (int)(newHeight * aspect);
                newBounds.Y = originalBounds.Y + dy;
                break;

            case ResizeHandle.Bottom:
                newHeight = originalBounds.Height + dy;
                newWidth  = (int)(newHeight * aspect);
                break;
        }

        if (newWidth < 10)  newWidth = 10;
        if (newHeight < 10) newHeight = 10;

        newBounds.Width  = newWidth;
        newBounds.Height = newHeight;

        newBounds = SnapHelper.ApplySnapping(newBounds, RenderForm.ClientSize);

        Picture.Bounds = newBounds;

        Debug.WriteLine($"[ResizeHandler.Move] handle={DraggingHandle}, " +
                        $"dx={dx}, dy={dy}, new=({newWidth}x{newHeight}), loc=({newBounds.X},{newBounds.Y})");
    }

    private ResizeHandle GetHandle()
    {
        var localPt = MouseState.Location;
        var nearLeft = localPt.X <= EdgeZoneIn && localPt.X >= -EdgeZoneOut;
        var nearRight = localPt.X >= Picture.Width - EdgeZoneIn && localPt.X <= Picture.Width + EdgeZoneOut;
        var nearTop = localPt.Y <= EdgeZoneIn && localPt.Y >= -EdgeZoneOut;
        var nearBottom = localPt.Y >= Picture.Height - EdgeZoneIn && localPt.Y <= Picture.Height + EdgeZoneOut;

        if (nearLeft && nearTop) return ResizeHandle.TopLeft;
        if (nearRight && nearTop) return ResizeHandle.TopRight;
        if (nearLeft && nearBottom) return ResizeHandle.BottomLeft;
        if (nearRight && nearBottom) return ResizeHandle.BottomRight;
        if (nearLeft) return ResizeHandle.Left;
        if (nearRight) return ResizeHandle.Right;
        if (nearTop) return ResizeHandle.Top;
        if (nearBottom) return ResizeHandle.Bottom;
        return ResizeHandle.None;
    }

    private static void UpdateCursor(ResizeHandle handle) => Cursor.Current = handle switch
    {
        ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
        ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
        ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
        ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
        _ => Cursors.Arrow,
    };
}

/// <summary>
/// Which corner or edge handle is active.
/// </summary>
public enum ResizeHandle
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
