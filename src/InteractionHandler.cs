using WinTransform.Helpers;
using Microsoft.Extensions.Logging;

namespace WinTransform;

record DragStartInfo(Rectangle OriginalBounds, Point MouseDownPoint);

abstract class InteractionHandler
{
    protected RenderBox Picture { get; }
    protected IRenderForm RenderForm { get; }
    protected ILogger Logger { get; }

    protected MouseEventArgs PictureMouseState
    {
        get
        {
            var parentState = RenderForm.MouseState;
            return new MouseEventArgs(
                parentState.Button,
                parentState.Clicks,
                parentState.Location.X - Picture.Location.X,
                parentState.Location.Y - Picture.Location.Y,
                parentState.Delta);
        }
    }

    /// <summary>
    /// Initial coordinates of the PictureBox in parent space.
    /// </summary>
    protected DragStartInfo DragStartInfo { get; private set; }

    /// <summary>
    /// Currently dragging?
    /// </summary>
    protected bool Dragging => DragStartInfo != null;


    private void StartDragging()
    {
        Logger.LogInformation("StartDragging");
        DragStartInfo = new DragStartInfo(Picture.Bounds, RenderForm.MouseState.Location);
        OnStartDragging();
    }

    protected virtual void OnStartDragging() { }

    protected void StopDragging()
    {
        DragStartInfo = null;
        Logger.LogInformation("StopDragging");
    }


    /// <summary>
    /// Returns true if the handler wants to become the active one.
    /// </summary>
    public abstract bool CanBeActive();
    protected virtual void OnDrag() { }

    public InteractionHandler(RenderBox picture, IRenderForm renderForm, ILogger logger)
    {
        Picture = picture;
        RenderForm = renderForm;
        Logger = logger;
        RenderForm.MouseStateChanged += () => IfActive(() =>
        {
            switch (RenderForm.MouseState.Type)
            {
                case MouseEventType.MouseDown:
                    StartDragging();
                    break;
                case MouseEventType.MouseUp:
                    StopDragging();
                    break;
                case MouseEventType.MouseMove:
                    CheckDragging();
                    if (Dragging)
                    {
                        OnDrag();
                    }
                    break;
            }
        });
    }

    private void CheckDragging()
    {
        if (Dragging && PictureMouseState.Button != MouseButtons.Left)
        {
            Logger.LogWarning("Was dragging but not holding left button!");
            StopDragging();
        }
    }

    protected void IfActive(Action action)
    {
        if (RenderForm.IsHandlerActive(this))
        {
            action();
        }
    }
}

interface IRenderForm
{
    Size ClientSize { get; }
    MouseState MouseState { get; }
    event Action MouseStateChanged;
    bool IsHandlerActive(InteractionHandler handler);
}
