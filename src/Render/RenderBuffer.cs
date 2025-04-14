using Microsoft.Extensions.Logging;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace WinTransform.Render;

class RenderBuffer : IDisposable
{
    private readonly SharpDX.Direct3D11.Device _device;
    private Size _size;

    public SwapChain SwapChain { get; }
    public Texture2D BackBuffer { get; internal set; }
    public RenderTargetView TargetView { get; internal set; }

    public RenderBuffer(SharpDX.Direct3D11.Device device, nint windowHandle)
    {
        _device = device;
        using var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device1>();
        dxgiDevice.MaximumFrameLatency = 1;
        var description = new SwapChainDescription
        {
            ModeDescription = new ModeDescription(0, 0, Rational.Empty, Format.B8G8R8A8_UNorm),
            SampleDescription = new SampleDescription(1, 0),
            Usage = Usage.RenderTargetOutput,
            BufferCount = 1,
            OutputHandle = windowHandle,
            SwapEffect = SwapEffect.Discard,
            IsWindowed = true
        };
        using var factory = new Factory1();
        SwapChain = new SwapChain(factory, device, description);
        UpdateBufferAndView();
    }

    public void UpdateSize(Size size, ILogger logger)
    {
        if (size == _size)
        {
            return;
        }
        _size = size;
        logger.LogInformation("Resizing render buffer");
        var viewport = new ViewportF(0, 0, size.Width, size.Height, 0.0f, 1.0f);
        _device.ImmediateContext.Rasterizer.SetViewport(viewport);
        BackBuffer?.Dispose();
        TargetView?.Dispose();
        SwapChain.ResizeBuffers(1, size.Width, size.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
        UpdateBufferAndView();
    }

    private void UpdateBufferAndView()
    {
        BackBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(SwapChain, 0);
        TargetView = new RenderTargetView(_device, BackBuffer);
        _device.ImmediateContext.OutputMerger.SetTargets(TargetView);
    }

    public void Dispose()
    {
        TargetView.Dispose();
        BackBuffer.Dispose();
        SwapChain.Dispose();
    }
}
