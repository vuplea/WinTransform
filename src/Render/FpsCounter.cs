#pragma warning disable CS4014
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WinTransform.Render;

class FpsCounter
{
    private int _frameCount = 0;
    private readonly TimeSpan _samplePeriod = TimeSpan.FromSeconds(5);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Dictionary<string, TimeSpan> _fractions = new();

    public void TrackFps(ILogger logger)
    {
        _frameCount++;
        var elapsed = _stopwatch.Elapsed;
        if (elapsed > _samplePeriod)
        {
            var fps = _frameCount / elapsed.TotalSeconds;
            var fractions = string.Join(' ', _fractions.Select(kvp => $"{kvp.Key}={(kvp.Value / elapsed) * 100 :F2}"));
            logger.LogInformation($"FPS: {fps:F2} {fractions}");
            _frameCount = 0;
            _stopwatch.Restart();
            _fractions.Clear();
        }
    }

    public void AddFraction(string label, Action action) => AddFraction(label, () =>
    {
        action();
        return Task.CompletedTask;
    });

    public async Task AddFraction(string label, Func<Task> action)
    {
        var initial = _stopwatch.Elapsed;
        try
        {
            await action();
        }
        finally
        {
            var final = _stopwatch.Elapsed;
            var current = _fractions.TryGetValue(label, out var value) ? value : TimeSpan.Zero;
            _fractions[label] = current + final - initial;
        }
    }
}
