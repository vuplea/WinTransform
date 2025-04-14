using Microsoft.Extensions.Logging;

namespace WinTransform.Helpers;

public static class Extensions
{
    public static void Trace(this Exception ex, ILogger logger, string label = null) =>
        logger.LogError(ex, nameof(Trace));

    public static async void NoAwait(this Task task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            ex.Trace(logger, nameof(NoAwait));
        }
    }

    public static T AutoSize<T>(this T control) where T : Control
    {
        control.AutoSize = true;
        control.Dock = DockStyle.Fill;
        return control;
    }
}
