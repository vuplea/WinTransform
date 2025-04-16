using Microsoft.Extensions.Logging;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System.Runtime.InteropServices;

namespace WinTransform.Render;

class VertexCache : IDisposable
{
    record Key(Vector2 ImageSize, double Angle);
    private Key _key;
    private SharpDX.Direct3D11.Buffer _vertexBuffer;

    public int Count { get; private set; }

    public void Update(Device device, Vector2 imageSize, double angle, ILogger logger)
    {
        var key = new Key(imageSize, angle);
        if (_key == key)
        {
            return;
        }
        _key = key;
        _vertexBuffer?.Dispose();
        logger.LogTrace("Generating vertex buffer");
        UpdateCore(device, imageSize, angle);
    }

    private void UpdateCore(Device device, Vector2 imageSize, double angle)
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
