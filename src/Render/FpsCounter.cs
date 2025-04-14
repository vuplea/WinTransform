using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WinTransform.Render;

class FpsCounter
{
    private int _frameCount = 0;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    internal void TrackFps(ILogger logger)
    {
        _frameCount++;
        if (_stopwatch.ElapsedMilliseconds > 1000)
        {
            logger.LogInformation($"FPS: {_frameCount}");
            _frameCount = 0;
            _stopwatch.Restart();
        }
    }
}
