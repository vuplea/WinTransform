using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
        _picture = new PictureBox().AutoSize();
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
                var channel = CaptureHelper.Capture(item, _cts.Token);
                while (true)
                {
                    await channel.WaitToReadAsync();
                    Frame frame = null;
                    while (channel.TryRead(out var latestFrame))
                    {
                        frame = latestFrame;
                    }
                    Render(frame);
                }

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

    private void Render(Frame frame)
    {
        _picture.Image = new Bitmap(
            frame.Width,
            frame.Height,
            frame.Stride,
            PixelFormat.Format32bppPArgb, Marshal.UnsafeAddrOfPinnedArrayElement(frame.Bytes, 0));
        _picture.Refresh();
    }
}