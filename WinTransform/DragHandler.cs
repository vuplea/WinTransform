using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WinTransform
{
    /// <summary>
    /// Handles the logic for DRAGGING the PictureBox around.
    /// </summary>
    class DragHandler : InteractionHandler
    {
        public DragHandler(RotatingPictureBox picture, RenderForm renderForm) :
            base(picture, renderForm, Program.ServiceProvider.GetRequiredService<ILogger<DragHandler>>()) { }

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

            // Snap
            newBounds = InteractionHelpers.ApplySnapping(newBounds, RenderForm.ClientSize);

            Picture.Bounds = newBounds;

            Logger.LogDebug($"dx={dx}, dy={dy}, loc=({newLoc.X},{newLoc.Y})");
        }
    }
}
