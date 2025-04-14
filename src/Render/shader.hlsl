struct VS_INPUT
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

// Pass vertex position straight to clip space; pass UV to pixel shader.
VS_OUTPUT VSMain(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = float4(input.Position, 1.0f);
    output.TexCoord = input.TexCoord;
    return output;
}

// Sample from our texture and return color
Texture2D    gTexture : register(t0);
SamplerState gSampler : register(s0);

float4 PSMain(VS_OUTPUT input) : SV_Target
{
    return gTexture.Sample(gSampler, input.TexCoord);
}
