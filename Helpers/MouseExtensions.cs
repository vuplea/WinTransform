using System.Runtime.CompilerServices;

namespace WinTransform.Helpers;

/// <summary>
/// Ability to track mouse events even when the active control is a child, and to query the last known mouse state.
/// </summary>
public static class MouseExtensions
{
    static readonly ConditionalWeakTable<Control, MouseState> LastMouseStates = [];

    public static void TrackMouseState(this Control control) =>
        control.OnMouseStateChanged(args => LastMouseStates.AddOrUpdate(control, args));

    public static MouseState MouseState(this Control control) => LastMouseStates.TryGetValue(control, out var args)
            ? args
            : new(MouseButtons.None, 0, 0, 0, 0, MouseEventType.MouseMove);

    public static void OnMouseStateChanged(this Control control, Action<MouseState> action) =>
        control.ListenMouseEventRecursively((c, handler) =>
        {
            c.MouseDown += (_, args) => handler(args, MouseEventType.MouseDown);
            c.MouseUp += (_, args) => handler(args, MouseEventType.MouseUp);
            c.MouseMove += (_, args) => handler(args, MouseEventType.MouseMove);
        }, action);

    private static void ListenMouseEventRecursively(
        this Control root,
        Action<Control, Action<MouseEventArgs, MouseEventType>> subscribe,
        Action<MouseState> action)
    {
        Subscribe(root);
        void Subscribe(Control control)
        {
            subscribe(control, (args, type) =>
            {
                var offset = Point.Empty;
                for (var controlIter = control; controlIter != root; controlIter = controlIter.Parent)
                {
                    offset = new(offset.X + controlIter.Location.X, offset.Y + controlIter.Location.Y);
                }
                var state = new MouseState
                (
                    args.Button,
                    args.Clicks,
                    args.X + offset.X,
                    args.Y + offset.Y,
                    args.Delta,
                    type
                );
                action(state);
            });
            foreach (var child in control.Controls.Cast<Control>())
            {
                Subscribe(child);
            }
        }
    }
}

public class MouseState : MouseEventArgs
{
    public MouseEventType Type { get; }

    public MouseState(MouseButtons button, int clicks, int x, int y, int delta, MouseEventType type)
        : base(button, clicks, x, y, delta)
    {
        Type = type;
    }
}

public enum MouseEventType
{
    MouseDown,
    MouseUp,
    MouseMove,
}
