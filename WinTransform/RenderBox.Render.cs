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

    private async Task CaptureAndRender(IntPtr handle)
    {
        using var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug);
        using var capture = new CaptureSession(_captureItem, device);
        using var shaders = Shaders.Load(device);
        while (true)
        {
            using var renderBuffer = RenderBuffer.Create(device, Width, Height, handle);
            using var _ = BuildVerticies(device, Angle);
            var initialCaptureSize = _captureItem.Size;
            var fpsCounter = new FpsCounter();
            while (true)
            {
                var bufferSize = renderBuffer.BackBuffer.Description;
                if (bufferSize.Width != Width || bufferSize.Height != Height)
                {
                    _logger.LogWarning($"Render size changed from {bufferSize.Width}x{bufferSize.Height} to {Width}x{Height}");
                    break;
                }
                using var frame = await capture.WaitFrame(_cts.Token);
                if (frame.ContentSize != initialCaptureSize)
                {
                    static string Format(SizeInt32 size) => $"{size.Width}x{size.Height}";
                    _logger.LogWarning($"Capture size changed from {Format(initialCaptureSize)} to {Format(_captureItem.Size)}");
                    break;
                }
                using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
                using var shaderResource = new ShaderResourceView(device, bitmap);
                device.ImmediateContext.PixelShader.SetShaderResource(0, shaderResource);
                device.ImmediateContext.Draw(vertexCount: 6, startVertexLocation: 0);
                renderBuffer.SwapChain.Present(0, PresentFlags.None);
                fpsCounter.TrackFps(_logger);
            }
        }
    }

    private static IDisposable BuildVerticies(SharpDX.Direct3D11.Device device, double angle)
    {
        var vertices = new Vertex[]
        {
            new(new(-1, +1, 0), new(0, 0)),
            new(new(+1, +1, 0), new(1, 0)),
            new(new(-1, -1, 0), new(0, 1)),
            new(new(+1, +1, 0), new(1, 0)),
            new(new(+1, -1, 0), new(1, 1)),
            new(new(-1, -1, 0), new(0, 1)),
        };
        var rotation = Matrix.RotationZ(MathUtil.DegreesToRadians(-(float)angle));
        Transform(ref vertices, rotation);
        var minX = vertices.Min(v => v.Space.X);
        var minY = vertices.Min(v => v.Space.Y);
        var maxX = vertices.Max(v => v.Space.X);
        var maxY = vertices.Max(v => v.Space.Y);
        var fillClip = Matrix.Translation(-minX, -minY, 0) *
                       Matrix.Scaling(2 / (maxX - minX), 2 / (maxY - minY), 0) *
                       Matrix.Translation(-1, -1, 0);
        Transform(ref vertices, fillClip);

        var vertexBuffer = SharpDX.Direct3D11.Buffer.Create(device, vertices, new()
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
            new VertexBufferBinding(vertexBuffer, stride: Marshal.SizeOf<Vertex>(), offset: 0));
        return vertexBuffer;

        static void Transform(ref Vertex[] vertices, Matrix transform) =>
            vertices = [.. vertices.Select(v => new Vertex(Vector3.TransformCoordinate(v.Space, transform), v.Tex))];
    }

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
