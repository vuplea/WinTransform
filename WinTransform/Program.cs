using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WinTransform;

static class Program
{
    public static ServiceProvider ServiceProvider { get; } = new ServiceCollection()
        .AddLogging(configure => configure
            .AddConsole()
#if DEBUG
            .SetMinimumLevel(LogLevel.Debug)
#endif
        )
        .BuildServiceProvider();

    [STAThread]
    public static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
