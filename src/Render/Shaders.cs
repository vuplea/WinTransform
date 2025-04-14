using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace WinTransform.Render;

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
