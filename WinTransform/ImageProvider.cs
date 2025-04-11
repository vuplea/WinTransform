using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Drawing.Imaging;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform;

class ImageProvider : IDisposable
{
    private readonly ILogger<ImageProvider> _logger = Program.ServiceProvider.GetRequiredService<ILogger<ImageProvider>>();
    private readonly CancellationTokenSource _cts = new();
    private readonly GraphicsCaptureItem _item;

    public ImageProvider(GraphicsCaptureItem item) => _item = item;

    internal void Attach(RotatingPictureBox picture) => CaptureLoop(picture).NoAwait(_logger);

    private async Task CaptureLoop(RotatingPictureBox picture)
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
                ex.Trace(_logger);
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
