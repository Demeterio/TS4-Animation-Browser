using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TS4AnimationBrowser.App;

public static class DdsImageDecoder
{
    private const uint Magic = 0x20534444; // "DDS "
    private const uint FourCcDxt1 = 0x31545844;
    private const uint FourCcDxt3 = 0x33545844;
    private const uint FourCcDxt5 = 0x35545844;
    private const uint FourCcDst1 = 0x31545344;
    private const uint FourCcDst3 = 0x33545344;
    private const uint FourCcDst5 = 0x35545344;
    private const uint FourCcDx10 = 0x30315844;

    public static BitmapSource Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 128 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4)) != Magic)
            throw new InvalidDataException("The VFX image resource is not a DDS stream.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4)) != 124)
            throw new InvalidDataException("Unsupported DDS header size.");

        var height = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12, 4)));
        var width = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16, 4)));
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            throw new InvalidDataException($"Unsupported DDS dimensions {width}×{height}.");

        var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(80, 4));
        var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(84, 4));
        var rgbBitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(88, 4));
        var redMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(92, 4));
        var greenMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(96, 4));
        var blueMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(100, 4));
        var alphaMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(104, 4));
        var pixelData = data;
        var offset = 128;

        DdsFormat format;
        if (fourCc == FourCcDxt1)
            format = DdsFormat.Bc1;
        else if (fourCc == FourCcDxt3)
            format = DdsFormat.Bc2;
        else if (fourCc == FourCcDxt5)
            format = DdsFormat.Bc3;
        else if (fourCc == FourCcDst1)
        {
            pixelData = UnshuffleDst1(data, offset);
            offset = 0;
            format = DdsFormat.Bc1;
        }
        else if (fourCc == FourCcDst3)
        {
            throw new NotSupportedException("DST3 image resources are not supported because the reference implementation has no validated sample format.");
        }
        else if (fourCc == FourCcDst5)
        {
            pixelData = UnshuffleDst5(data, offset);
            offset = 0;
            format = DdsFormat.Bc3;
        }
        else if (fourCc == FourCcDx10)
        {
            if (data.Length < 148)
                throw new InvalidDataException("DDS DX10 header is truncated.");
            var dxgiFormat = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(128, 4));
            offset = 148;
            format = dxgiFormat switch
            {
                71 or 72 => DdsFormat.Bc1,
                74 or 75 => DdsFormat.Bc2,
                77 or 78 => DdsFormat.Bc3,
                87 or 91 => DdsFormat.Bgra32,
                _ => throw new NotSupportedException($"DDS DXGI format {dxgiFormat} is not supported yet.")
            };
        }
        else if ((pixelFlags & 0x40) != 0 && rgbBitCount == 32)
        {
            format = DdsFormat.Bgra32;
        }
        else
        {
            throw new NotSupportedException($"DDS FourCC 0x{fourCc:X8} / {rgbBitCount}-bit format is not supported yet.");
        }

        var pixels = new byte[checked(width * height * 4)];
        switch (format)
        {
            case DdsFormat.Bc1:
                DecodeBc1(pixelData, offset, width, height, pixels);
                break;
            case DdsFormat.Bc2:
                DecodeBc2(pixelData, offset, width, height, pixels);
                break;
            case DdsFormat.Bc3:
                DecodeBc3(pixelData, offset, width, height, pixels);
                break;
            case DdsFormat.Bgra32:
                DecodeUncompressed32(pixelData, offset, width, height, pixels, redMask, greenMask, blueMask, alphaMask);
                break;
        }

        return BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            checked(width * 4));
    }

    private static byte[] UnshuffleDst1(byte[] data, int offset)
    {
        var dataSize = data.Length - offset;
        if (dataSize <= 0 || dataSize % 8 != 0)
            throw new InvalidDataException("DST1 payload size is not aligned to 8-byte BC1 blocks.");

        var blockCount = dataSize / 8;
        var firstHalf = offset;
        var secondHalf = offset + blockCount * 4;
        var output = new byte[dataSize];
        var destination = 0;

        for (var block = 0; block < blockCount; block++)
        {
            Buffer.BlockCopy(data, firstHalf + block * 4, output, destination, 4);
            destination += 4;
            Buffer.BlockCopy(data, secondHalf + block * 4, output, destination, 4);
            destination += 4;
        }

        return output;
    }

    private static byte[] UnshuffleDst5(byte[] data, int offset)
    {
        var dataSize = data.Length - offset;
        if (dataSize <= 0 || dataSize % 16 != 0)
            throw new InvalidDataException("DST5 payload size is not aligned to 16-byte BC3 blocks.");

        var blockCount = dataSize / 16;

        // Sims 4 DST5 stores the four pieces of every BC3 block in separate contiguous streams:
        // all 2-byte chunks, all 4-byte color chunks, all 6-byte alpha-index chunks, then the
        // final 4-byte chunks. Reassemble each block as 2 + 6 + 4 + 4, matching standard DXT5.
        var stream0 = offset;
        var stream2 = stream0 + blockCount * 2;
        var stream1 = stream2 + blockCount * 4;
        var stream3 = stream1 + blockCount * 6;
        var output = new byte[dataSize];
        var destination = 0;

        for (var block = 0; block < blockCount; block++)
        {
            Buffer.BlockCopy(data, stream0 + block * 2, output, destination, 2);
            destination += 2;
            Buffer.BlockCopy(data, stream1 + block * 6, output, destination, 6);
            destination += 6;
            Buffer.BlockCopy(data, stream2 + block * 4, output, destination, 4);
            destination += 4;
            Buffer.BlockCopy(data, stream3 + block * 4, output, destination, 4);
            destination += 4;
        }

        return output;
    }

    private static void DecodeBc1(byte[] data, int offset, int width, int height, byte[] pixels)
    {
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        var required = checked(blocksWide * blocksHigh * 8);
        EnsureAvailable(data, offset, required);
        var noAlpha = ReadOnlySpan<byte>.Empty;

        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                DecodeColorBlock(data.AsSpan(offset, 8), width, height, bx, by, pixels, allowTransparent: true, noAlpha, hasExternalAlpha: false);
                offset += 8;
            }
        }
    }

    private static void DecodeBc2(byte[] data, int offset, int width, int height, byte[] pixels)
    {
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        var required = checked(blocksWide * blocksHigh * 16);
        EnsureAvailable(data, offset, required);

        Span<byte> alpha = stackalloc byte[16];
        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                var alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
                for (var i = 0; i < 16; i++)
                    alpha[i] = (byte)(((alphaBits >> (i * 4)) & 0xF) * 17);
                DecodeColorBlock(data.AsSpan(offset + 8, 8), width, height, bx, by, pixels, allowTransparent: false, alpha, hasExternalAlpha: true);
                offset += 16;
            }
        }
    }

    private static void DecodeBc3(byte[] data, int offset, int width, int height, byte[] pixels)
    {
        var blocksWide = (width + 3) / 4;
        var blocksHigh = (height + 3) / 4;
        var required = checked(blocksWide * blocksHigh * 16);
        EnsureAvailable(data, offset, required);

        Span<byte> alpha = stackalloc byte[16];
        Span<byte> palette = stackalloc byte[8];
        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                var a0 = data[offset];
                var a1 = data[offset + 1];
                palette[0] = a0;
                palette[1] = a1;
                if (a0 > a1)
                {
                    for (var i = 1; i <= 6; i++)
                        palette[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                }
                else
                {
                    for (var i = 1; i <= 4; i++)
                        palette[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                    palette[6] = 0;
                    palette[7] = 255;
                }

                ulong alphaBits = 0;
                for (var i = 0; i < 6; i++)
                    alphaBits |= (ulong)data[offset + 2 + i] << (8 * i);
                for (var i = 0; i < 16; i++)
                    alpha[i] = palette[(int)((alphaBits >> (i * 3)) & 0x7)];

                DecodeColorBlock(data.AsSpan(offset + 8, 8), width, height, bx, by, pixels, allowTransparent: false, alpha, hasExternalAlpha: true);
                offset += 16;
            }
        }
    }

    private static void DecodeColorBlock(
        ReadOnlySpan<byte> block,
        int width,
        int height,
        int blockX,
        int blockY,
        byte[] pixels,
        bool allowTransparent,
        ReadOnlySpan<byte> alphaValues,
        bool hasExternalAlpha)
    {
        var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(0, 2));
        var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(2, 2));
        Span<Rgba> colors = stackalloc Rgba[4];
        colors[0] = Decode565(c0);
        colors[1] = Decode565(c1);

        if (c0 > c1 || !allowTransparent)
        {
            colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
            colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);
        }
        else
        {
            colors[2] = Lerp(colors[0], colors[1], 1, 1, 2);
            colors[3] = new Rgba(0, 0, 0, 0);
        }

        var indices = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(4, 4));
        for (var py = 0; py < 4; py++)
        {
            var y = blockY * 4 + py;
            if (y >= height)
                continue;
            for (var px = 0; px < 4; px++)
            {
                var x = blockX * 4 + px;
                if (x >= width)
                    continue;

                var local = py * 4 + px;
                var color = colors[(int)((indices >> (local * 2)) & 0x3)];
                var alpha = hasExternalAlpha ? alphaValues[local] : color.A;
                var destination = (y * width + x) * 4;
                pixels[destination] = color.B;
                pixels[destination + 1] = color.G;
                pixels[destination + 2] = color.R;
                pixels[destination + 3] = alpha;
            }
        }
    }

    private static void DecodeUncompressed32(
        byte[] data,
        int offset,
        int width,
        int height,
        byte[] pixels,
        uint redMask,
        uint greenMask,
        uint blueMask,
        uint alphaMask)
    {
        var required = checked(width * height * 4);
        EnsureAvailable(data, offset, required);
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var raw = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + pixel * 4, 4));
            var destination = pixel * 4;
            pixels[destination] = Extract(raw, blueMask, 0);
            pixels[destination + 1] = Extract(raw, greenMask, 0);
            pixels[destination + 2] = Extract(raw, redMask, 0);
            pixels[destination + 3] = alphaMask == 0 ? (byte)255 : Extract(raw, alphaMask, 255);
        }
    }

    private static byte Extract(uint value, uint mask, byte fallback)
    {
        if (mask == 0)
            return fallback;
        var shift = BitOperations.TrailingZeroCount(mask);
        var maximum = mask >> shift;
        var component = (value & mask) >> shift;
        return maximum == 0 ? fallback : (byte)Math.Round(component * 255.0 / maximum);
    }

    private static Rgba Decode565(ushort value)
    {
        var r = (byte)(((value >> 11) & 0x1F) * 255 / 31);
        var g = (byte)(((value >> 5) & 0x3F) * 255 / 63);
        var b = (byte)((value & 0x1F) * 255 / 31);
        return new Rgba(r, g, b, 255);
    }

    private static Rgba Lerp(Rgba a, Rgba b, int weightA, int weightB, int divisor)
        => new(
            (byte)((a.R * weightA + b.R * weightB) / divisor),
            (byte)((a.G * weightA + b.G * weightB) / divisor),
            (byte)((a.B * weightA + b.B * weightB) / divisor),
            255);

    private static void EnsureAvailable(byte[] data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
            throw new InvalidDataException("DDS pixel data is truncated.");
    }

    private enum DdsFormat
    {
        Bc1,
        Bc2,
        Bc3,
        Bgra32
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A);
}
