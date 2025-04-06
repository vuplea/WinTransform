using System.Diagnostics;

namespace WinTransform;

static class Program
{
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
