using System.ComponentModel;
using System.Drawing.Imaging;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform;

[DesignerCategory("")]
class RenderForm : Form
{
    private readonly CancellationTokenSource _cts = new();
    private readonly PictureBox _picture;

    public RenderForm(GraphicsCaptureItem item)
    {
        this.AutoSize();
        Height = 0;
        _picture = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom }.AutoSize();
        Controls.Add(_picture);
        CaptureLoop(item).NoAwait();
        FormClosed += (_, __) => _cts.Cancel();
    }

    private async Task CaptureLoop(GraphicsCaptureItem item)
    {
        while (true)
        {
            try
            {
                var captureInfo = await CaptureHelper.StartCapture(item, _cts.Token);
                _picture.Image = new Bitmap
                (
                    captureInfo.Width,
                    captureInfo.Height,
                    captureInfo.Stride,
                    PixelFormat.Format32bppPArgb,
                    captureInfo.DataPointer
                );
                captureInfo.ProcessFrameCallback = _picture.Refresh;
                await captureInfo.CaptureTask;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ex.Trace();
                await Task.Delay(1000);
            }
        }
    }
}