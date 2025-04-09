using System.Drawing.Imaging;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform;

class ImageProvider : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly GraphicsCaptureItem _item;

    public ImageProvider(GraphicsCaptureItem item) => _item = item;

    internal void Attach(PictureBox picture) => CaptureLoop(picture).NoAwait();

    private async Task CaptureLoop(PictureBox picture)
    {
        while (true)
        {
            try
            {
                var captureInfo = await CaptureHelper.StartCapture(_item, _cts.Token);
                picture.Image = new Bitmap
                (
                    captureInfo.Width,
                    captureInfo.Height,
                    captureInfo.Stride,
                    PixelFormat.Format32bppPArgb,
                    captureInfo.DataPointer
                );
                captureInfo.ProcessFrameCallback = picture.Refresh;
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

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
