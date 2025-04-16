using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection.Metadata.Ecma335;

namespace WinTransform;

/// <summary>
/// Handles the logic for RESIZING (with aspect ratio).
/// </summary>
class ResizeHandler : InteractionHandler
{
    private ResizeHandle? _draggingHandle;

    public ResizeHandler(RenderBox picture, RenderForm renderForm)
        : base(picture, renderForm, Program.ServiceProvider.GetRequiredService<ILogger<ResizeHandler>>()) { }

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
    protected override void OnStartDragging() => _draggingHandle = GetHandle();

    protected override void OnDrag()
    {
        // Perform the actual resize
        var actLikeHandle = ApplySnapping(RenderForm.MouseState.Location, RenderForm.ClientSize, out var currentPoint)
            ?? _draggingHandle;
        var dx = currentPoint.X - DragStartInfo.MouseDownPoint.X;
        var dy = currentPoint.Y - DragStartInfo.MouseDownPoint.Y;

        var originalBounds = DragStartInfo.OriginalBounds;
        var newBounds = originalBounds;

        var aspect = (double)Picture.Width / Picture.Height;

        var newWidth = newBounds.Width;
        var newHeight = newBounds.Height;

        switch (actLikeHandle)
        {
            case ResizeHandle.TopLeft:
                newWidth = originalBounds.Width - dx;
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

            case ResizeHandle.BottomLeft or ResizeHandle.Left:
                newWidth  = originalBounds.Width - dx;
                newHeight = (int)(newWidth / aspect);
                newBounds.X = originalBounds.X + dx;
                break;

            case ResizeHandle.BottomRight or ResizeHandle.Right:
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

        newBounds.Width  = newWidth;
        newBounds.Height = newHeight;
        Picture.Bounds = newBounds;

        if (originalBounds != newBounds)
        {
            Logger.LogTrace($"handle={_draggingHandle}, " +
                $"old=({originalBounds.Width}x{originalBounds.Height}), " +
                $"new=({newBounds.Width}x{newBounds.Height}), " +
                $"loc=({newBounds.X},{newBounds.Y})");
        }
    }

    private ResizeHandle GetHandle()
    {
        InteractionHelpers.IsNearEdges(
            PictureMouseState.Location,
            Picture.Size,
            edgeZoneIn: 10,
            edgeZoneOut: 10,
            out var nearLeft,
            out var nearRight,
            out var nearTop,
            out var nearBottom);

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

    private static ResizeHandle? ApplySnapping(Point location, Size parentSize, out Point newLocation)
    {
        const int SnapDistance = 15;

        InteractionHelpers.IsNearEdges(
            location,
            parentSize,
            edgeZoneIn: SnapDistance,
            edgeZoneOut: SnapDistance,
            out var nearLeft,
            out var nearRight,
            out var nearTop,
            out var nearBottom);

        if (nearLeft && nearTop)
        {
            newLocation = new Point(0, 0);
            return ResizeHandle.TopLeft;
        }
        if (nearRight && nearTop)
        {
            newLocation = new Point(parentSize.Width, 0);
            return ResizeHandle.TopRight;
        }
        if (nearLeft && nearBottom)
        {
            newLocation = new Point(0, parentSize.Height);
            return ResizeHandle.BottomLeft;
        }
        if (nearRight && nearBottom)
        {
            newLocation = new Point(parentSize.Width, parentSize.Height);
            return ResizeHandle.BottomRight;
        }
        if (nearLeft)
        {
            newLocation = new Point(0, location.Y);
            return ResizeHandle.Left;
        }
        if (nearRight)
        {
            newLocation = new Point(parentSize.Width, location.Y);
            return ResizeHandle.Right;
        }
        if (nearTop)
        {
            newLocation = new Point(location.X, 0);
            return ResizeHandle.Top;
        }
        if (nearBottom)
        {
            newLocation = new Point(location.X, parentSize.Height);
            return ResizeHandle.Bottom;
        }

        newLocation = location;
        return null;
    }
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
