using Microsoft.Extensions.Logging;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform.Render;

class InputBuffer : IDisposable
{
    private readonly SharpDX.Direct3D11.Device _device;
    private readonly ILogger _logger;
    private ShaderResourceView _shaderResource;

    public Texture2D Buffer { get; private set; }

    public InputBuffer(SharpDX.Direct3D11.Device device, ILogger logger)
    {
        _device = device;
        _logger = logger;
        UpdateBuffer(1, 1);
    }

    public void Update(Direct3D11CaptureFrame frame)
    {
        UpdateBuffer(frame.ContentSize.Width, frame.ContentSize.Height);
        using var frameTexture = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
        _device.ImmediateContext.CopyResource(frameTexture, Buffer);
    }

    private void UpdateBuffer(int width, int height)
    {
        if (Buffer?.Description.Width == width && Buffer?.Description.Height == height)
        {
            return;
        }
        Dispose();
        _logger.LogInformation("Creating input buffer");
        var description = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        };
        Buffer = new Texture2D(_device, description);
        _shaderResource = new ShaderResourceView(_device, Buffer);
        _device.ImmediateContext.PixelShader.SetShaderResource(0, _shaderResource);
    }

    public void Dispose()
    {
        _shaderResource?.Dispose();
        Buffer?.Dispose();
    }
}
