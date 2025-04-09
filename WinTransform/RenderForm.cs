using Nito.Disposables;
using System.ComponentModel;
using System.Diagnostics;
using WinTransform.Helpers;

namespace WinTransform;

[DesignerCategory("")]
class RenderForm : Form, IRenderForm
{
    private readonly PictureBox _picture;
    private readonly DragHandler _dragHandler;
    private readonly ResizeHandler _resizeHandler;
    private InteractionHandler _activeHandler;

    public event Action MouseStateChanged;

    public bool IsHandlerActive(InteractionHandler handler) =>
        _activeHandler == handler;

    public MouseState MouseState => this.MouseState();

    private void ResetPictureSize()
    {
        if (_picture.Image == null)
        {
            return;
        }
        var aspect = (float)_picture.Image.Width / _picture.Image.Height;
        var w = ClientSize.Width / 2;
        var h = (int)(w / aspect);
        var x = (ClientSize.Width - w) / 2;
        var y = (ClientSize.Height - h) / 2;
        _picture.Bounds = new Rectangle(x, y, w, h);
    }

    public RenderForm(ImageProvider imageProvider)
    {
        Text = "Split Logic: DragHandler & ResizeHandler";
        //DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        //StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(800, 600);

        _picture = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.None
        };
        ResetPictureSize();
        _dragHandler = new DragHandler(_picture, this);
        _resizeHandler = new ResizeHandler(_picture, this);

        imageProvider.Attach(_picture);
        FormClosed += (_, __) => imageProvider.Dispose();

        Controls.Add(_picture);
        this.TrackMouseState();
        this.OnMouseStateChanged(_ =>
        {
            DetermineActiveHandler();
            MouseStateChanged?.Invoke();
        });
    }

    private void DetermineActiveHandler()
    {
        using var _ = TraceHandlerChanges();
        // Prefer maintaining control to the current handler
        if (_activeHandler != null && _activeHandler.CanBeActive())
        {
            return;
        }
        // ResizeHandler is preferred over DragHandler
        if (_resizeHandler.CanBeActive())
        {
            _activeHandler = _resizeHandler;
            return;
        }
        if (_dragHandler.CanBeActive())
        {
            _activeHandler = _dragHandler;
            return;
        }
        _activeHandler = null;
        return;

        Disposable TraceHandlerChanges()
        {
            var initialHandler = _activeHandler;
            return new Disposable(() =>
            {
                if (_activeHandler != initialHandler)
                {
                    Debug.WriteLine($"Handler: {_activeHandler?.GetType().Name ?? "null"}");
                }
            });
        }
    }
}

