using System.Buffers.Binary;
using System.IO.Compression;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderCaptureTests
{
    [Fact]
    public void CaptureStateConsumesSingleRequestAndStoresFrame()
    {
        var capture = new SceneCaptureState();
        capture.Request();

        Assert.True(capture.IsRequested);
        Assert.True(capture.ConsumeRequest());
        Assert.False(capture.IsRequested);
        Assert.False(capture.ConsumeRequest());

        var frame = new RenderCaptureFrame(1, 1, DateTimeOffset.UtcNow, [1, 2, 3, 4]);
        capture.Complete(frame);

        Assert.Same(frame, capture.LastFrame);
        Assert.Equal(4, capture.LastFrame!.ByteLength);
    }

    [Fact]
    public void FlipsRgbaRowsFromOpenGlBottomLeftOrigin()
    {
        byte[] bottomLeftRows =
        [
            1, 0, 0, 255,
            2, 0, 0, 255,
            3, 0, 0, 255,
            4, 0, 0, 255
        ];

        var topLeftRows = RenderCapture.FlipRgbaRows(bottomLeftRows, width: 2, height: 2);

        Assert.Equal(
            [
                3, 0, 0, 255,
                4, 0, 0, 255,
                1, 0, 0, 255,
                2, 0, 0, 255
            ],
            topLeftRows);
    }

    [Fact]
    public void ValidatesRgbaFrameSize()
    {
        Assert.True(RenderCapture.IsValidRgbaFrame(2, 3, 24));
        Assert.False(RenderCapture.IsValidRgbaFrame(2, 3, 23));
        Assert.False(RenderCapture.IsValidRgbaFrame(0, 3, 0));
    }

    [Fact]
    public void EncodesRgbaPngWithIhdrAndZlibScanlines()
    {
        byte[] pixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 128
        ];

        var png = RenderPngWriter.EncodeRgba(width: 2, height: 1, pixels);

        Assert.Equal([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], png[..8]);
        Assert.Equal(13u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(8, 4)));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);

        var idat = ReadSingleChunkData(png, "IDAT");
        using var compressed = new MemoryStream(idat);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var scanlines = new MemoryStream();
        zlib.CopyTo(scanlines);

        Assert.Equal([0, 255, 0, 0, 255, 0, 255, 0, 128], scanlines.ToArray());
    }

    [Fact]
    public void WritesCapturedFrameAsPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zhengyan-mmd-{Guid.NewGuid():N}.png");
        try
        {
            var frame = new RenderCaptureFrame(1, 1, DateTimeOffset.UtcNow, [1, 2, 3, 4]);
            RenderPngWriter.WriteFrame(path, frame);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal([0x89, 0x50, 0x4e, 0x47], bytes[..4]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void RejectsInvalidPngFrameSize()
    {
        Assert.Throws<ArgumentException>(() => RenderPngWriter.EncodeRgba(2, 2, [1, 2, 3, 4]));
    }

    private static byte[] ReadSingleChunkData(byte[] png, string chunkType)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4)));
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == chunkType)
            {
                return png.AsSpan(offset + 8, length).ToArray();
            }

            offset = checked(offset + 12 + length);
        }

        throw new InvalidOperationException($"PNG chunk not found: {chunkType}");
    }
}
