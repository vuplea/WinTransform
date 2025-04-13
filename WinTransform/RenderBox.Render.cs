using Microsoft.Extensions.Logging;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinTransform.Helpers;

namespace WinTransform;

partial class RenderBox
{
    private async Task CaptureAndRenderLoop()
    {
        while (true)
        {
            try
            {
                var handle = Handle;
                await Task.Run(() => CaptureAndRender(handle), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(CaptureAndRenderLoop));
                await Task.Delay(1000, _cts.Token);
            }
        }
    }

    private async Task CaptureAndRender(IntPtr handle, Action captureSizeChanged = null)
    {
        using var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug);
        using var capture = new CaptureSession(_captureItem, device);
        using var shaders = Shaders.Load(device);
        using var renderBuffer = new RenderBuffer(device, handle);
        using var vertexCache = new VertexCache();
        var fpsCounter = new FpsCounter();
        var lastCaptureSize = _captureItem.Size;
        while (true)
        {
            using var frame = await capture.WaitFrame(_cts.Token);
            if (frame.ContentSize != lastCaptureSize)
            {
                lastCaptureSize = frame.ContentSize;
                captureSizeChanged?.Invoke();
                static string Format(SizeInt32 size) => $"{size.Width}x{size.Height}";
                _logger.LogWarning($"Capture size changed from {Format(lastCaptureSize)} to {Format(_captureItem.Size)}");
            }
            renderBuffer.SetSize(Size);
            vertexCache.Update(device, GetImageSize(maintainImageSize: true), Angle);
            using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
            using var shaderResource = new ShaderResourceView(device, bitmap);
            device.ImmediateContext.PixelShader.SetShaderResource(0, shaderResource);
            device.ImmediateContext.Draw(vertexCount: 6, startVertexLocation: 0);
            renderBuffer.SwapChain.Present(0, PresentFlags.None);
            fpsCounter.TrackFps(_logger);
        }
    }
}

class VertexCache : IDisposable
{
    record Key(Vector2 ImageSize, double Angle);
    private Key _key;
    private SharpDX.Direct3D11.Buffer _vertexBuffer;

    public void Update(SharpDX.Direct3D11.Device device, Vector2 imageSize, double angle)
    {
        var key = new Key(imageSize, angle);
        if (_key == key)
        {
            return;
        }
        _key = key;
        _vertexBuffer?.Dispose();
        UpdateCore(device, imageSize, angle);
    }

    private void UpdateCore(SharpDX.Direct3D11.Device device, Vector2 imageSize, double angle)
    {
        var w = imageSize.X;
        var h = imageSize.Y;
        var vertices = new Vertex[]
        {
            new(new(0, 0, 0), new(0, 0)),
            new(new(w, 0, 0), new(1, 0)),
            new(new(0, h, 0), new(0, 1)),

            new(new(w, 0, 0), new(1, 0)),
            new(new(w, h, 0), new(1, 1)),
            new(new(0, h, 0), new(0, 1)),
        };
        var rotation = Matrix.RotationZ(MathUtil.DegreesToRadians((float)angle));
        Transform(ref vertices, rotation);
        var minX = vertices.Min(v => v.Space.X);
        var minY = vertices.Min(v => v.Space.Y);
        var maxX = vertices.Max(v => v.Space.X);
        var maxY = vertices.Max(v => v.Space.Y);
        var fillClip = Matrix.Translation(-minX, -minY, 0) *
                       Matrix.Scaling(2 / (maxX - minX), -2 / (maxY - minY), 0) *
                       Matrix.Translation(-1, 1, 0);
        Transform(ref vertices, fillClip);

        _vertexBuffer = SharpDX.Direct3D11.Buffer.Create(device, vertices, new()
        {
            SizeInBytes = vertices.Length * Marshal.SizeOf<Vertex>(),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.VertexBuffer,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None,
            StructureByteStride = 0
        });
        var assembler = device.ImmediateContext.InputAssembler;
        assembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        assembler.SetVertexBuffers(slot: 0,
            new VertexBufferBinding(_vertexBuffer, stride: Marshal.SizeOf<Vertex>(), offset: 0));

        static void Transform(ref Vertex[] vertices, Matrix transform) =>
            vertices = [.. vertices.Select(v => new Vertex(Vector3.TransformCoordinate(v.Space, transform), v.Tex))];
    }

    public void Dispose() => _vertexBuffer?.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Space;
        public Vector2 Tex;
        public Vertex(Vector3 space, Vector2 tex)
        {
            Space = space;
            Tex = tex;
        }
    }
}

record Shaders(VertexShader VertexShader, PixelShader PixelShader, InputLayout InputLayout,
    SamplerState Sampler, CompilationResult BytecodeVertex, CompilationResult BytecodePixel) : IDisposable
{
    public static Shaders Load(SharpDX.Direct3D11.Device device)
    {
        var context = device.ImmediateContext;
        // Vertex shader
        var vertexShaderBytecode = ShaderBytecode.CompileFromFile(
            "shader.hlsl",
            "VSMain",
            "vs_4_0",
            ShaderFlags.None,
            EffectFlags.None
        );
        var vertexShader = new VertexShader(device, vertexShaderBytecode);
        context.VertexShader.Set(vertexShader);

        // Pixel shader
        var pixelShaderBytecode = ShaderBytecode.CompileFromFile(
            "shader.hlsl",
            "PSMain",
            "ps_4_0",
            ShaderFlags.None,
            EffectFlags.None
        );
        var pixelShader = new PixelShader(device, pixelShaderBytecode);
        context.PixelShader.Set(pixelShader);
        var sampler = new SamplerState(device, new()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            ComparisonFunction = Comparison.Never,
            MaximumLod = float.MaxValue
        });
        context.PixelShader.SetSampler(0, sampler);

        // Input layout
        var inputElements = new[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
        };
        var inputLayout = new InputLayout(device, ShaderSignature.GetInputSignature(vertexShaderBytecode), inputElements);
        context.InputAssembler.InputLayout = inputLayout;
        return new(vertexShader, pixelShader, inputLayout, sampler, vertexShaderBytecode, pixelShaderBytecode);
    }

    public void Dispose()
    {
        InputLayout.Dispose();
        Sampler.Dispose();
        PixelShader.Dispose();
        BytecodePixel.Dispose();
        VertexShader.Dispose();
        BytecodeVertex.Dispose();
    }
}

class FpsCounter
{
    private int _frameCount = 0;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    internal void TrackFps(ILogger logger)
    {
        _frameCount++;
        if (_stopwatch.ElapsedMilliseconds > 1000)
        {
            logger.LogInformation($"FPS: {_frameCount}");
            _frameCount = 0;
            _stopwatch.Restart();
        }
    }
}
