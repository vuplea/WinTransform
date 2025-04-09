using System.Diagnostics;
using WinTransform.Helpers;

namespace WinTransform;

record DragStartInfo(Rectangle OriginalBounds, Point MouseDownPoint)
{
    private readonly Dictionary<Type, object> _additionalData = [];
    public DragStartInfo Add(object data)
    {
        _additionalData[data.GetType()] = data;
        return this;
    }
    public T Get<T>() => (T)_additionalData[typeof(T)];
}

abstract class InteractionHandler
{
    protected PictureBox Picture { get; }
    protected IRenderForm RenderForm { get; }
    protected MouseEventArgs MouseState
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

    protected void StartDragging()
    {
        Trace.TraceInformation($"[{GetType().Name}] StartDragging");
        DragStartInfo = new DragStartInfo(Picture.Bounds, RenderForm.MouseState.Location);
        foreach (var value in AddDraggingData())
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            DragStartInfo.Add(value);
        }
    }

    protected virtual IEnumerable<object> AddDraggingData() => [];

    protected void StopDragging()
    {
        DragStartInfo = null;
        Trace.TraceInformation($"[{GetType().Name}] StopDragging");
    }


    /// <summary>
    /// Returns true if the handler wants to become the active one.
    /// </summary>
    public abstract bool CanBeActive();
    protected virtual void OnDrag() { }

    public InteractionHandler(PictureBox picture, IRenderForm renderForm)
    {
        Picture = picture;
        RenderForm = renderForm;
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
        if (Dragging && MouseState.Button != MouseButtons.Left)
        {
            Trace.TraceWarning($"[{GetType().Name}] Was dragging but not holding left button!");
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
