using System.ComponentModel;
using WinTransform.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Nito.Disposables;
using Windows.Graphics.Capture;

namespace WinTransform;

[DesignerCategory("")]
class RenderForm : Form, IRenderForm
{
    private readonly ILogger<RenderForm> _logger = Program.ServiceProvider.GetRequiredService<ILogger<RenderForm>>();
    private readonly RenderBox _picture;
    private readonly IReadOnlyCollection<InteractionHandler> _handlers;
    private InteractionHandler _activeHandler;

    public event Action MouseStateChanged;

    public bool IsHandlerActive(InteractionHandler handler) =>
        _activeHandler == handler;

    public MouseState MouseState => this.MouseState();

    public RenderForm(GraphicsCaptureItem captureItem)
    {
        Text = "Split Logic: DragHandler & ResizeHandler";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(800, 600);

        _picture = new RenderBox(captureItem);
        _handlers =
        [
            // register in the order of "CanBeActive" preference
            new RotationHandler(_picture, this),
            new ResizeHandler(_picture, this),
            new DragHandler(_picture, this),
        ];

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
        _activeHandler = _handlers.FirstOrDefault(h => h.CanBeActive());
        return;

        Disposable TraceHandlerChanges()
        {
            var initialHandler = _activeHandler;
            return new Disposable(() =>
            {
                if (_activeHandler != initialHandler)
                {
                    _logger.LogTrace($"Handler: {_activeHandler?.GetType().Name ?? "null"}");
                }
            });
        }
    }
}