using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Nito.Disposables;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinTransform.Helpers;

namespace WinTransform;

partial class RenderBox
{
    private async Task CaptureAndRenderLoop()
    {
        var context = SynchronizationContext.Current;
        while (true)
        {
            try
            {
                var handle = Handle;
                await Task.Run(() => CaptureAndRender(handle, CaptureSizeChanged), _cts.Token);
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

        void CaptureSizeChanged()
        {
            _logger.LogInformation("Capture size changed");
            context.Post(_ => RecalculateSize(maintainImageSize: false), null);
        }
    }

    private async Task CaptureAndRender(IntPtr handle, Action captureSizeChanged = null)
    {
        using var _ = TrackControlSize(out var sizeRecalculated);
        using var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug);
        using var capture = new CaptureSession(_captureItem, device);
        using var shaders = Shaders.Load(device);
        using var renderBuffer = new RenderBuffer(device, handle);
        using var inputBuffer = new InputBuffer(device, _logger);
        using var vertexCache = new VertexCache();
        var fpsCounter = new FpsCounter();
        var lastCaptureSize = _captureItem.Size;
        while (true)
        {
            await await Task.WhenAny(
                capture.WaitFrame(_cts.Token),
                sizeRecalculated.WaitAsync(_cts.Token));

            // Disposing asap seems to help
            using (var frame = capture.GetLatestFrame())
            {
                if (frame != null)
                {
                    if (frame.ContentSize != lastCaptureSize)
                    {
                        lastCaptureSize = frame.ContentSize;
                        captureSizeChanged?.Invoke();
                    }
                    inputBuffer.Update(frame);
                }
            }
            renderBuffer.UpdateSize(Size, _logger);
            vertexCache.Update(device, GetImageSize(maintainImageSize: true), Angle, _logger);
            device.ImmediateContext.Draw(vertexCache.Count, 0);
            renderBuffer.SwapChain.Present(0, PresentFlags.None);
            fpsCounter.TrackFps(_logger);
        }
    }

    Disposable TrackControlSize(out AsyncManualResetEvent sizeRecalculated)
    {
        // use manual reset, AutoResetEvent allocates on each wait
        var recalculatedEvent = new AsyncManualResetEvent();
        sizeRecalculated = recalculatedEvent;
        SizeRecalculated += OnRecalculated;
        return new(() => SizeRecalculated -= OnRecalculated);

        void OnRecalculated()
        {
            recalculatedEvent.Set();
            recalculatedEvent.Reset();
        }
    }
}

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

class VertexCache : IDisposable
{
    record Key(Vector2 ImageSize, double Angle);
    private Key _key;
    private SharpDX.Direct3D11.Buffer _vertexBuffer;

    public int Count { get; private set; }

    public void Update(SharpDX.Direct3D11.Device device, Vector2 imageSize, double angle, ILogger logger)
    {
        var key = new Key(imageSize, angle);
        if (_key == key)
        {
            return;
        }
        _key = key;
        _vertexBuffer?.Dispose();
        logger.LogInformation("Generating vertex buffer");
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

        Count = vertices.Length;
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
