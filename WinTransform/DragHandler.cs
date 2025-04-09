using System.Diagnostics;

namespace WinTransform
{
    /// <summary>
    /// Handles the logic for DRAGGING the PictureBox around.
    /// </summary>
    class DragHandler : InteractionHandler
    {
        public DragHandler(PictureBox picture, RenderForm renderForm) : base(picture, renderForm) { }

        public override bool CanBeActive() =>
            Dragging || Picture.ClientRectangle.Contains(MouseState.Location);

        protected override void OnDrag()
        {
            var currentPoint = RenderForm.MouseState.Location;

            var dx = currentPoint.X - DragStartInfo.MouseDownPoint.X;
            var dy = currentPoint.Y - DragStartInfo.MouseDownPoint.Y;

            var newLoc = new Point(
                DragStartInfo.OriginalBounds.X + dx,
                DragStartInfo.OriginalBounds.Y + dy);

            var newBounds = new Rectangle(newLoc, DragStartInfo.OriginalBounds.Size);

            // Snap
            newBounds = SnapHelper.ApplySnapping(newBounds, RenderForm.ClientSize);

            Picture.Bounds = newBounds;

            Debug.WriteLine($"[DragHandler.Move] dx={dx}, dy={dy}, loc=({newLoc.X},{newLoc.Y})");
        }
    }
}
