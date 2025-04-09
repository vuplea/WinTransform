using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WinTransform;

static class Program
{
    public static ServiceProvider ServiceProvider { get; } = new ServiceCollection()
        .AddLogging(configure => configure.AddConsole())
        .BuildServiceProvider();

    [STAThread]
    public static void Main()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
