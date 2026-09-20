using System.Numerics;
using System.Text;

namespace TS4AnimationBrowser.Core.Animation;

public static class ClipDecoder
{
    private static readonly byte[] CodecMagic = Encoding.ASCII.GetBytes("_pilC3S_");

    public static ClipAnimation Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 48)
            throw new InvalidDataException("CLIP resource is too small.");

        var metadata = ReadMetadata(data);
        var codecOffset = FindSequence(data, CodecMagic);
        if (codecOffset < 0)
            throw new InvalidDataException("CLIP codec marker _pilC3S_ was not found.");

        var codec = DecodeCodec(data.AsSpan(codecOffset));
        return new ClipAnimation
        {
            Version = metadata.Version,
            Flags = metadata.Flags,
            DurationSeconds = metadata.Duration,
            InitialRotation = metadata.InitialRotation,
            InitialTranslation = metadata.InitialTranslation,
            Name = metadata.Name,
            RigNamespace = metadata.RigNamespace,
            CodecVersion = codec.CodecVersion,
            FrameDuration = codec.FrameDuration,
            MaxFrameCount = codec.MaxFrameCount,
            CodecAnimationName = codec.AnimationName,
            CodecSourceName = codec.SourceName,
            Tracks = codec.Tracks
        };
    }

    private static ClipMetadata ReadMetadata(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var version = reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        var duration = reader.ReadSingle();
        var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var translation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        if (version >= 5)
            reader.ReadUInt32();
        if (version >= 10)
        {
            reader.ReadUInt32();
            reader.ReadUInt32();
        }
        if (version >= 11)
            reader.ReadUInt32();

        var name = version >= 7 ? ReadString32(reader) : string.Empty;
        var rigNamespace = ReadString32(reader);
        return new ClipMetadata(version, flags, duration, rotation, translation, name, rigNamespace);
    }

    private static CodecResult DecodeCodec(ReadOnlySpan<byte> codecData)
    {
        using var stream = new MemoryStream(codecData.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != "_pilC3S_")
            throw new InvalidDataException("Invalid CLIP codec marker.");

        var codecVersion = reader.ReadUInt32();
        reader.ReadUInt32();
        var frameDuration = reader.ReadSingle();
        var maxFrameCount = reader.ReadUInt16();
        reader.ReadUInt16();
        var channelCount = reader.ReadUInt32();
        var paletteFloatCount = reader.ReadUInt32();
        var channelDataOffset = reader.ReadUInt32();
        var paletteDataOffset = reader.ReadUInt32();
        var animationNameOffset = reader.ReadUInt32();
        var sourceNameOffset = reader.ReadUInt32();

        if (channelCount > 1_000_000 || paletteFloatCount > 10_000_000)
            throw new InvalidDataException("CLIP codec contains unreasonable channel data counts.");

        ValidateOffset(channelDataOffset, stream.Length, "channel data");
        if (paletteFloatCount > 0)
            ValidateOffset(paletteDataOffset, stream.Length, "F1 palette data");
        ValidateOffset(animationNameOffset, stream.Length, "animation name");
        ValidateOffset(sourceNameOffset, stream.Length, "source name");

        stream.Position = channelDataOffset;
        var channels = new List<ChannelInfo>(checked((int)channelCount));
        for (var i = 0u; i < channelCount; i++)
        {
            EnsureRemaining(stream, 20, $"channel header #{i}");
            var frameDataOffset = reader.ReadUInt32();
            var trackKey = reader.ReadUInt32();
            var offset = reader.ReadSingle();
            var scale = reader.ReadSingle();
            var frameCount = reader.ReadUInt16();
            var channelType = (ClipChannelType)reader.ReadByte();
            var subTarget = (ClipSubTarget)reader.ReadByte();

            if (frameCount > 0 && GetStorageWidth(channelType) > 0)
                ValidateOffset(frameDataOffset, stream.Length, $"frame data #{i}");

            channels.Add(new ChannelInfo(frameDataOffset, trackKey, offset, scale, frameCount, channelType, subTarget));
        }

        var palette = Array.Empty<float>();
        if (paletteFloatCount > 0)
        {
            stream.Position = paletteDataOffset;
            palette = new float[checked((int)paletteFloatCount)];
            for (var i = 0; i < palette.Length; i++)
            {
                EnsureRemaining(stream, 4, "F1 palette");
                palette[i] = reader.ReadSingle();
            }
        }

        var animationName = ReadZStringAt(reader, animationNameOffset);
        var sourceName = ReadZStringAt(reader, sourceNameOffset);

        var trackMap = new Dictionary<uint, List<AnimationCurve>>();
        foreach (var channel in channels)
        {
            AnimationCurve? decoded;
            try
            {
                decoded = DecodeChannel(reader, stream, channel, palette);
            }
            catch (NotSupportedException)
            {
                // Unknown/unsupported channel subtypes should not prevent the rest of a CLIP from loading.
                continue;
            }

            if (decoded is null)
                continue;

            if (!trackMap.TryGetValue(channel.TrackKey, out var list))
            {
                list = [];
                trackMap[channel.TrackKey] = list;
            }
            list.Add(decoded);
        }

        var tracks = trackMap.Select(pair => new AnimationTrack
        {
            TrackKey = pair.Key,
            Position = pair.Value.FirstOrDefault(curve => curve.Kind == AnimationCurveKind.Position),
            Orientation = pair.Value.FirstOrDefault(curve => curve.Kind == AnimationCurveKind.Orientation),
            Scale = pair.Value.FirstOrDefault(curve => curve.Kind == AnimationCurveKind.Scale)
        }).ToArray();

        return new CodecResult(codecVersion, frameDuration, maxFrameCount, animationName, sourceName, tracks);
    }

    private static AnimationCurve? DecodeChannel(BinaryReader reader, Stream stream, ChannelInfo info, float[] palette)
    {
        var kind = info.SubTarget switch
        {
            ClipSubTarget.Translation => AnimationCurveKind.Position,
            ClipSubTarget.Orientation => AnimationCurveKind.Orientation,
            ClipSubTarget.Scale => AnimationCurveKind.Scale,
            _ => AnimationCurveKind.Unknown
        };
        if (kind == AnimationCurveKind.Unknown)
            return null;

        var constant = GetConstantValues(info.ChannelType, kind);
        if (constant is not null)
        {
            return new AnimationCurve
            {
                Kind = kind,
                ChannelType = info.ChannelType,
                SubTarget = info.SubTarget,
                Frames = [new AnimationFrame(0, constant)]
            };
        }

        var componentCount = GetComponentCount(info.ChannelType);
        var width = GetStorageWidth(info.ChannelType);
        if (componentCount == 0 || width == 0 || info.FrameCount == 0)
            return null;

        stream.Position = info.FrameDataOffset;
        var frames = new List<AnimationFrame>(info.FrameCount);
        for (var frameNumber = 0; frameNumber < info.FrameCount; frameNumber++)
        {
            EnsureRemaining(stream, 4, "CLIP frame header");
            var frameIndex = reader.ReadUInt16();
            var frameFlags = reader.ReadUInt16();
            var values = ReadChannelValues(reader, stream, info, palette, componentCount, frameFlags);
            frames.Add(new AnimationFrame(frameIndex, values));
        }

        return new AnimationCurve
        {
            Kind = kind,
            ChannelType = info.ChannelType,
            SubTarget = info.SubTarget,
            Frames = frames
        };
    }

    private static float[] ReadChannelValues(
        BinaryReader reader,
        Stream stream,
        ChannelInfo info,
        float[] palette,
        int componentCount,
        ushort flags)
    {
        if (IsPaletteChannel(info.ChannelType))
            return ReadPaletteValues(reader, stream, palette, componentCount, flags, info.Offset, info.Scale);

        if (IsByteNormalized(info.ChannelType))
            return ReadByteNormalizedValues(reader, stream, componentCount, flags, info.Offset, info.Scale);

        if (info.ChannelType == ClipChannelType.F3HighPrecisionNormalized)
            return ReadPackedVector3(reader, stream, flags, info.Offset, info.Scale);

        if (info.ChannelType == ClipChannelType.F4SuperHighPrecisionQuaternion)
            return ReadPackedQuaternion12(reader, stream, flags, info.Offset, info.Scale);

        if (info.ChannelType is ClipChannelType.F4HighPrecisionNormalizedQuaternion
            or ClipChannelType.F3HighPrecisionNormalizedQuaternion)
        {
            return ReadPackedQuaternion3x10(reader, stream, flags, info.ChannelType, info.Offset, info.Scale);
        }

        throw new NotSupportedException($"Unsupported CLIP channel type {(byte)info.ChannelType} ({info.ChannelType}).");
    }

    private static float[] ReadPaletteValues(
        BinaryReader reader,
        Stream stream,
        float[] palette,
        int componentCount,
        ushort flags,
        float offset,
        float scale)
    {
        var values = new float[componentCount];
        EnsureRemaining(stream, componentCount * 2L, "palette frame values");
        for (var i = 0; i < componentCount; i++)
        {
            var paletteIndex = reader.ReadUInt16();
            if (paletteIndex >= palette.Length)
                throw new InvalidDataException("CLIP frame points outside the F1 palette.");

            var value = ApplySign(palette[paletteIndex], flags, i);
            values[i] = value * scale + offset;
        }

        // Width-2 TS4 frames are 32-bit aligned; F1/F3 therefore have two trailing bytes.
        if ((componentCount & 1) != 0)
        {
            EnsureRemaining(stream, 2, "palette frame padding");
            reader.ReadUInt16();
        }

        return values;
    }

    private static float[] ReadByteNormalizedValues(
        BinaryReader reader,
        Stream stream,
        int componentCount,
        ushort flags,
        float offset,
        float scale)
    {
        var values = new float[componentCount];
        EnsureRemaining(stream, 4, "normalized frame values");
        for (var i = 0; i < componentCount; i++)
        {
            var value = ApplySign(reader.ReadByte() / 255f, flags, i);
            values[i] = value * scale + offset;
        }

        for (var i = componentCount; i < 4; i++)
            reader.ReadByte();
        return values;
    }

    private static float[] ReadPackedVector3(BinaryReader reader, Stream stream, ushort flags, float offset, float scale)
    {
        EnsureRemaining(stream, 4, "packed Vector3 frame");
        var packed = reader.ReadUInt32();
        var values = new float[3];
        const uint max = 1023;
        for (var i = 0; i < 3; i++)
        {
            var value = ((packed >> (i * 10)) & max) / (float)max;
            value = ApplySign(value, flags, i);
            values[i] = value * scale + offset;
        }
        return values;
    }

    private static float[] ReadPackedQuaternion12(BinaryReader reader, Stream stream, ushort flags, float offset, float scale)
    {
        EnsureRemaining(stream, 8, "super-high-precision quaternion frame");
        var values = new float[4];
        const ushort max = 4095;
        for (var i = 0; i < 4; i++)
        {
            var value = (reader.ReadUInt16() & max) / (float)max;
            value = ApplySign(value, flags, i);
            values[i] = value * scale + offset;
        }
        return values;
    }

    private static float[] ReadPackedQuaternion3x10(
        BinaryReader reader,
        Stream stream,
        ushort flags,
        ClipChannelType channelType,
        float offset,
        float scale)
    {
        EnsureRemaining(stream, 4, "high-precision quaternion frame");
        var packed = reader.ReadUInt32();
        var values = new float[4];
        const uint max = 1023;

        for (var i = 0; i < 3; i++)
        {
            var value = ((packed >> (i * 10)) & max) / (float)max;
            value = ApplySign(value, flags, i);
            if (channelType != ClipChannelType.F3HighPrecisionNormalizedQuaternion)
                value = value * scale + offset;
            values[i] = value;
        }

        // This format stores three quaternion components. Reconstruct the fourth from unit length.
        var remaining = MathF.Max(0f, 1f - (values[0] * values[0] + values[1] * values[1] + values[2] * values[2]));
        values[3] = ApplySign(MathF.Sqrt(remaining), flags, 3);
        return values;
    }

    private static float ApplySign(float value, ushort flags, int component)
        => (flags & (1 << component)) != 0 ? -value : value;

    private static float[]? GetConstantValues(ClipChannelType type, AnimationCurveKind kind)
    {
        if (type is ClipChannelType.F1Zero or ClipChannelType.F2Zero or ClipChannelType.F3Zero or ClipChannelType.F4Zero)
        {
            if (kind == AnimationCurveKind.Orientation)
                return [0f, 0f, 0f, 1f];
            return new float[GetLogicalConstantCount(type)];
        }

        if (type is ClipChannelType.F1One or ClipChannelType.F2One or ClipChannelType.F3One or ClipChannelType.F4One)
            return Enumerable.Repeat(1f, GetLogicalConstantCount(type)).ToArray();

        if (type == ClipChannelType.F4QuaternionIdentity)
            return [0f, 0f, 0f, 1f];

        return null;
    }

    private static int GetLogicalConstantCount(ClipChannelType type) => type switch
    {
        ClipChannelType.F1Zero or ClipChannelType.F1One => 1,
        ClipChannelType.F2Zero or ClipChannelType.F2One => 2,
        ClipChannelType.F3Zero or ClipChannelType.F3One => 3,
        ClipChannelType.F4Zero or ClipChannelType.F4One => 4,
        _ => 0
    };

    private static int GetComponentCount(ClipChannelType type) => type switch
    {
        ClipChannelType.F1 or ClipChannelType.F1Normalized => 1,
        ClipChannelType.F2 or ClipChannelType.F2Normalized => 2,
        ClipChannelType.F3 or ClipChannelType.F3Normalized or ClipChannelType.F3HighPrecisionNormalized => 3,
        ClipChannelType.F4 or ClipChannelType.F4Normalized
            or ClipChannelType.F4HighPrecisionNormalizedQuaternion
            or ClipChannelType.F4SuperHighPrecisionQuaternion
            or ClipChannelType.F3HighPrecisionNormalizedQuaternion => 4,
        _ => 0
    };

    private static int GetStorageWidth(ClipChannelType type) => type switch
    {
        ClipChannelType.F1Normalized or ClipChannelType.F2Normalized
            or ClipChannelType.F3Normalized or ClipChannelType.F4Normalized => 1,
        ClipChannelType.F3HighPrecisionNormalized
            or ClipChannelType.F4HighPrecisionNormalizedQuaternion
            or ClipChannelType.F3HighPrecisionNormalizedQuaternion => 10,
        ClipChannelType.F1 or ClipChannelType.F2 or ClipChannelType.F3 or ClipChannelType.F4
            or ClipChannelType.F4SuperHighPrecisionQuaternion => 2,
        _ => 0
    };

    private static bool IsPaletteChannel(ClipChannelType type)
        => type is ClipChannelType.F1 or ClipChannelType.F2 or ClipChannelType.F3 or ClipChannelType.F4;

    private static bool IsByteNormalized(ClipChannelType type)
        => type is ClipChannelType.F1Normalized or ClipChannelType.F2Normalized
            or ClipChannelType.F3Normalized or ClipChannelType.F4Normalized;

    private static string ReadString32(BinaryReader reader)
    {
        EnsureRemaining(reader.BaseStream, 4, "string length");
        var length = reader.ReadInt32();
        if (length < 0 || length > 65_536 || reader.BaseStream.Position + length > reader.BaseStream.Length)
            throw new InvalidDataException("Invalid CLIP string length.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static string ReadZStringAt(BinaryReader reader, uint offset)
    {
        reader.BaseStream.Position = offset;
        var bytes = new List<byte>();
        while (reader.BaseStream.Position < reader.BaseStream.Length && bytes.Count < 65_536)
        {
            var value = reader.ReadByte();
            if (value == 0)
                break;
            bytes.Add(value);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static int FindSequence(byte[] data, byte[] sequence)
    {
        for (var i = 0; i <= data.Length - sequence.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < sequence.Length; j++)
            {
                if (data[i + j] == sequence[j])
                    continue;
                matches = false;
                break;
            }
            if (matches)
                return i;
        }
        return -1;
    }

    private static void ValidateOffset(uint offset, long length, string name)
    {
        if (offset >= length)
            throw new InvalidDataException($"CLIP {name} offset is outside the codec data.");
    }

    private static void EnsureRemaining(Stream stream, long count, string field)
    {
        if (count < 0 || stream.Position + count > stream.Length)
            throw new InvalidDataException($"CLIP ends unexpectedly while reading {field}.");
    }

    private sealed record ClipMetadata(uint Version, uint Flags, float Duration, Quaternion InitialRotation, Vector3 InitialTranslation, string Name, string RigNamespace);
    private sealed record ChannelInfo(uint FrameDataOffset, uint TrackKey, float Offset, float Scale, ushort FrameCount, ClipChannelType ChannelType, ClipSubTarget SubTarget);
    private sealed record CodecResult(uint CodecVersion, float FrameDuration, ushort MaxFrameCount, string AnimationName, string SourceName, IReadOnlyList<AnimationTrack> Tracks);
}
