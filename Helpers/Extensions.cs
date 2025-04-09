namespace WinTransform.Helpers;

public static class Extensions
{
    public static void Trace(this Exception ex) => System.Diagnostics.Trace.TraceError(ex.ToString());

    public static async void NoAwait(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            ex.Trace();
        }
    }

    public static T AutoSize<T>(this T control) where T : Control
    {
        control.AutoSize = true;
        control.Dock = DockStyle.Fill;
        return control;
    }
}
