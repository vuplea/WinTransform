using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace WinTransform.Helpers;

public record RenderBuffer(SwapChain SwapChain, Texture2D BackBuffer, RenderTargetView TargetView) : IDisposable
{
    public static RenderBuffer Create(SharpDX.Direct3D11.Device device, int width, int height, IntPtr windowHandle)
    {
        var description = new SwapChainDescription
        {
            ModeDescription = new ModeDescription(width, height, Rational.Empty, Format.B8G8R8A8_UNorm),
            SampleDescription = new SampleDescription(1, 0),
            Usage = Usage.RenderTargetOutput,
            BufferCount = 1,
            OutputHandle = windowHandle,
            SwapEffect = SwapEffect.Discard,
            IsWindowed = true
        };
        using var factory = new Factory1();
        var swapChain = new SwapChain(factory, device, description);
        var backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);
        var targetView = new RenderTargetView(device, backBuffer);
        device.ImmediateContext.OutputMerger.SetTargets(targetView);
        var viewport = new ViewportF(0, 0, width, height, 0.0f, 1.0f);
        device.ImmediateContext.Rasterizer.SetViewport(viewport);
        using var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device1>();
        dxgiDevice.MaximumFrameLatency = 1;
        return new RenderBuffer(swapChain, backBuffer, targetView);
    }

    public void Dispose()
    {
        TargetView.Dispose();
        BackBuffer.Dispose();
        SwapChain.Dispose();
    }
}
