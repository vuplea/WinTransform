// https://github.com/robmikh/ManagedScreenshotDemo
// MIT License
// 
// Copyright (c) 2022 Robert Mikhayelyan
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Nito.Disposables;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace WinTransform.Helpers;

record Frame(int Width, int Height, int Stride, byte[] Bytes);

class FrameSizeChangedException : Exception { }

class CaptureHelper
{
    public static ChannelReader<Frame> Capture(GraphicsCaptureItem item, CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<Frame>();
        ct.Register(() => channel.Writer.Complete(new OperationCanceledException()));
        Task.Run(CaptureAsync, ct).NoAwait();
        return channel.Reader;

        async Task CaptureAsync()
        {
            // Setup D3D
            using var device = Direct3D11Helper.CreateDevice();
            using var d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);
            using var d3dContext = d3dDevice.ImmediateContext;

            // Create our staging texture
            var size = item.Size;
            var description = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = size.Width,
                Height = size.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.Direct3D11.ResourceUsage.Staging,
                BindFlags = SharpDX.Direct3D11.BindFlags.None,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
            };
            using var stagingTexture = new SharpDX.Direct3D11.Texture2D(d3dDevice, description);

            // Setup capture
            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                size);
            using var session = framePool.CreateCaptureSession(item);


            framePool.FrameArrived += (sender, _) =>
            {
                using var frame = sender.TryGetNextFrame();
                if (frame.ContentSize != size)
                {
                    channel.Writer.Complete(new FrameSizeChangedException());
                    return;
                }
                using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);

                // Copy to our staging texture
                d3dContext.CopyResource(bitmap, stagingTexture);
                using var __ = new Disposable(() => d3dContext.UnmapSubresource(stagingTexture, 0));

                // Map our texture and get the bits
                var mapped = d3dContext.MapSubresource(stagingTexture, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                var sourceStride = mapped.RowPitch;

                // Allocate some memory to hold our copy
                var bytes = new byte[frame.ContentSize.Height * mapped.RowPitch];
                Marshal.Copy(mapped.DataPointer, bytes, 0, bytes.Length);
                channel.Writer.TryWrite(new Frame(size.Width, size.Height, mapped.RowPitch, bytes));
            };

            // Start the capture and wait
            session.StartCapture();
            try
            {
                await channel.Reader.Completion;
            }
            catch { }
        }
    }
}
