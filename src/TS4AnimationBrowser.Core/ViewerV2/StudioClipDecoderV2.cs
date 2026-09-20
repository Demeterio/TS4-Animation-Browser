using System.Numerics;
using System.Text;
using TS4AnimationBrowser.Core.Animation;

namespace TS4AnimationBrowser.Core.ViewerV2;

public enum StudioCurveTypeV2 : byte
{
    Position = 1,
    Orientation = 2,
    Morph = 7
}

public sealed record StudioFrameV2(ushort FrameIndex, float[] Values);
public sealed record StudioClipMetadataV2(uint ResourceVersion, string RigNamespace, ulong RigInstance);

public sealed class StudioCurveV2
{
    public required StudioCurveTypeV2 Type { get; init; }
    public required IReadOnlyList<StudioFrameV2> Frames { get; init; }
}

public sealed class StudioTrackV2
{
    public required uint TrackKey { get; init; }
    public StudioCurveV2? Position { get; init; }
    public StudioCurveV2? Orientation { get; init; }
}

public sealed class StudioClipV2
{
    public uint ResourceVersion { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string RigNamespace { get; init; } = string.Empty;
    public ulong RigInstance { get; init; }
    public float FrameDuration { get; init; } = 1f / 30f;
    public ushort MaxFrameCount { get; init; }
    public IReadOnlyList<StudioTrackV2> Tracks { get; init; } = Array.Empty<StudioTrackV2>();
    public double DurationSeconds => MaxFrameCount * FrameDuration;
    public double EffectiveDurationSeconds => DurationSeconds;
    public string CodecAnimationName => Name;
    public string CodecSourceName => SourceName;

    // MainWindow still keeps a legacy ClipAnimation field for UI state. The renderer no longer
    // consumes it; this conversion exists only while the rest of the browser is migrated cleanly.
    public static implicit operator ClipAnimation(StudioClipV2 clip) => new()
    {
        DurationSeconds = (float)clip.DurationSeconds,
        Name = clip.Name,
        RigNamespace = clip.RigNamespace,
        CodecVersion = 2,
        FrameDuration = clip.FrameDuration,
        MaxFrameCount = clip.MaxFrameCount,
        CodecAnimationName = clip.Name,
        CodecSourceName = clip.SourceName,
        Tracks = Array.Empty<AnimationTrack>()
    };
}

public static class StudioClipDecoderV2
{
    private const uint RigReferenceVersion = 0x11;
    private static readonly byte[] CodecMagic = Encoding.ASCII.GetBytes("_pilC3S_");

    public static StudioClipMetadataV2 ReadMetadata(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var envelope = ReadEnvelope(data);
        return new StudioClipMetadataV2(envelope.ResourceVersion, envelope.ActorName, envelope.RigInstance);
    }

    public static StudioClipV2 Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var envelope = ReadEnvelope(data);

        using var stream = new MemoryStream(data, envelope.CodecOffset, envelope.CodecLength, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != "_pilC3S_")
            throw new InvalidDataException("Invalid S3CLIP marker at the codec position declared by the CLIP resource.");

        var version = reader.ReadUInt32();
        if (version != 2)
            throw new NotSupportedException($"Viewer V2 expects S3CLIP codec v2. Found {version}.");

        reader.ReadUInt32();
        var frameDuration = reader.ReadSingle();
        var maxFrameCount = reader.ReadUInt16();
        reader.ReadUInt16();
        var curveCount = reader.ReadUInt32();
        var indexedFloatCount = reader.ReadUInt32();
        var curveDataOffset = reader.ReadUInt32();
        var indexedFloatOffset = reader.ReadUInt32();
        var animationNameOffset = reader.ReadUInt32();
        var sourceNameOffset = reader.ReadUInt32();

        if (curveCount > 1_000_000 || indexedFloatCount > 10_000_000)
            throw new InvalidDataException("Unreasonable S3CLIP counts.");

        ValidateOffset(curveDataOffset, stream.Length, "curve data");
        if (indexedFloatCount > 0)
            ValidateOffset(indexedFloatOffset, stream.Length, "indexed float data");
        ValidateOffset(animationNameOffset, stream.Length, "animation name");
        ValidateOffset(sourceNameOffset, stream.Length, "source name");

        stream.Position = curveDataOffset;
        var curveHeaders = new List<CurveHeader>(checked((int)curveCount));
        for (var i = 0; i < curveCount; i++)
        {
            EnsureRemaining(stream, 20, $"curve header #{i}");
            var frameDataOffset = reader.ReadUInt32();
            var trackKey = reader.ReadUInt32();
            var offset = reader.ReadSingle();
            var scale = reader.ReadSingle();
            var frameCount = reader.ReadUInt16();
            var flags = reader.ReadByte();
            var curveType = (StudioCurveTypeV2)reader.ReadByte();
            curveHeaders.Add(new CurveHeader(frameDataOffset, trackKey, offset, scale, frameCount, flags, curveType));
        }

        var animationName = ReadZStringAt(reader, animationNameOffset);
        var sourceName = ReadZStringAt(reader, sourceNameOffset);

        var indexedFloats = Array.Empty<float>();
        if (indexedFloatCount > 0)
        {
            stream.Position = indexedFloatOffset;
            indexedFloats = new float[checked((int)indexedFloatCount)];
            for (var i = 0; i < indexedFloats.Length; i++)
                indexedFloats[i] = reader.ReadSingle();
        }

        var tracks = new Dictionary<uint, MutableTrack>();
        foreach (var header in curveHeaders)
        {
            if (header.CurveType is not (StudioCurveTypeV2.Position or StudioCurveTypeV2.Orientation))
                continue;

            var frames = ReadFrames(reader, stream, header, indexedFloats);
            if (!tracks.TryGetValue(header.TrackKey, out var track))
            {
                track = new MutableTrack(header.TrackKey);
                tracks.Add(header.TrackKey, track);
            }

            var curve = new StudioCurveV2 { Type = header.CurveType, Frames = frames };
            if (header.CurveType == StudioCurveTypeV2.Position)
                track.Position = curve;
            else
                track.Orientation = curve;
        }

        return new StudioClipV2
        {
            ResourceVersion = envelope.ResourceVersion,
            Name = animationName,
            SourceName = sourceName,
            RigNamespace = envelope.ActorName,
            RigInstance = envelope.RigInstance,
            FrameDuration = frameDuration > 0 ? frameDuration : 1f / 30f,
            MaxFrameCount = maxFrameCount,
            Tracks = tracks.Values.Select(track => track.ToImmutable()).ToArray()
        };
    }

    private static ClipEnvelope ReadEnvelope(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        EnsureRemaining(stream, 12, "CLIP resource header");
        var resourceVersion = reader.ReadUInt32();
        reader.ReadUInt32(); // u1
        reader.ReadSingle(); // duration

        // ClipResourceSims4 stores eight unknown floats followed by three hashes before the
        // p32 clip/actor strings. Keep this byte-for-byte with Studio rather than interpreting
        // those fields as transforms.
        EnsureRemaining(stream, 44, "CLIP resource metadata");
        for (var i = 0; i < 8; i++)
            reader.ReadSingle();
        for (var i = 0; i < 3; i++)
            reader.ReadUInt32();

        ReadString32(reader, "clip name");
        var actorName = ReadString32(reader, "actor name");

        var actorCount = ReadCount(reader, stream, "actor count", 4096);
        for (var i = 0; i < actorCount; i++)
            ReadString32(reader, $"actor #{i}");

        var ikTargetCount = ReadCount(reader, stream, "IK target count", 4096);
        for (var i = 0; i < ikTargetCount; i++)
        {
            EnsureRemaining(stream, 4, $"IK target #{i} indices");
            reader.ReadInt16();
            reader.ReadInt16();
            ReadString32(reader, $"IK target #{i} actor");
            ReadString32(reader, $"IK target #{i} name");
        }

        EnsureRemaining(stream, 4, "event count");
        var eventCount = reader.ReadUInt32();
        if (eventCount > 100_000)
            throw new InvalidDataException($"Unreasonable CLIP event count: {eventCount}.");

        for (var i = 0u; i < eventCount; i++)
        {
            EnsureRemaining(stream, 8, $"event #{i} header");
            reader.ReadUInt32(); // event type
            var length = reader.ReadUInt32();
            if (length > int.MaxValue || stream.Position + length > stream.Length)
                throw new InvalidDataException($"CLIP event #{i} extends past the resource.");
            stream.Position += length;
        }

        ulong rigInstance = 0;
        if (resourceVersion >= RigReferenceVersion)
        {
            EnsureRemaining(stream, 8, "CLIP rig instance");
            rigInstance = reader.ReadUInt64();
        }

        EnsureRemaining(stream, 4, "embedded S3CLIP size");
        var codecLength = reader.ReadUInt32();
        var codecOffset = stream.Position;
        if (codecLength < CodecMagic.Length || codecLength > int.MaxValue || codecOffset + codecLength > stream.Length)
            throw new InvalidDataException("Embedded S3CLIP size is invalid.");

        EnsureRemaining(stream, CodecMagic.Length, "embedded S3CLIP marker");
        var marker = reader.ReadBytes(CodecMagic.Length);
        if (!marker.SequenceEqual(CodecMagic))
            throw new InvalidDataException("CLIP envelope did not point to an _pilC3S_ codec block.");

        return new ClipEnvelope(
            resourceVersion,
            actorName,
            rigInstance,
            checked((int)codecOffset),
            checked((int)codecLength));
    }

    private static int ReadCount(BinaryReader reader, Stream stream, string field, int maximum)
    {
        EnsureRemaining(stream, 4, field);
        var count = reader.ReadInt32();
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"Invalid {field}: {count}.");
        return count;
    }

    private static string ReadString32(BinaryReader reader, string field)
    {
        EnsureRemaining(reader.BaseStream, 4, $"{field} length");
        var length = reader.ReadInt32();
        if (length < 0 || length > 65_536 || reader.BaseStream.Position + length > reader.BaseStream.Length)
            throw new InvalidDataException($"Invalid CLIP {field} length.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static IReadOnlyList<StudioFrameV2> ReadFrames(BinaryReader reader, Stream stream, CurveHeader header, float[] indexedFloats)
    {
        if (header.FrameCount == 0)
            return Array.Empty<StudioFrameV2>();

        ValidateOffset(header.FrameDataOffset, stream.Length, "frame data");
        stream.Position = header.FrameDataOffset;
        var frames = new List<StudioFrameV2>(header.FrameCount);
        var dataType = header.Flags & 0x07;

        for (var i = 0; i < header.FrameCount; i++)
        {
            EnsureRemaining(stream, 4, "frame header");
            var frameIndex = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            float[] values;

            switch (dataType)
            {
                case 2:
                    EnsureRemaining(stream, 4, "packed Vector3");
                    values = DecodePackedVector3(reader.ReadUInt32(), flags, header.Offset, header.Scale);
                    break;
                case 3:
                    values = DecodeIndexedVector3(reader, stream, indexedFloats, flags, header.Offset, header.Scale);
                    break;
                case 4:
                    EnsureRemaining(stream, 8, "packed Quaternion");
                    values = new float[4];
                    for (var component = 0; component < 4; component++)
                    {
                        var packed = reader.ReadUInt16();
                        var value = (packed & 0x0FFF) / 4095f;
                        if ((flags & (1 << component)) != 0)
                            value = -value;
                        values[component] = value * header.Scale + header.Offset;
                    }
                    break;
                case 5:
                    EnsureRemaining(stream, 2, "scalar frame");
                    var scalar = reader.ReadUInt16() / 65535f;
                    if ((flags & 1) != 0)
                        scalar = -scalar;
                    values = [scalar * header.Scale + header.Offset];
                    break;
                default:
                    throw new NotSupportedException($"Unsupported Studio curve data type {dataType}.");
            }

            frames.Add(new StudioFrameV2(frameIndex, values));
        }

        return frames;
    }

    private static float[] DecodePackedVector3(uint packed, ushort flags, float offset, float scale)
    {
        var values = new float[3];
        for (var component = 0; component < 3; component++)
        {
            var value = ((packed >> (component * 10)) & 0x3FF) / 1023f;
            if ((flags & (1 << component)) != 0)
                value = -value;
            values[component] = value * scale + offset;
        }
        return values;
    }

    private static float[] DecodeIndexedVector3(BinaryReader reader, Stream stream, float[] indexedFloats, ushort flags, float offset, float scale)
    {
        var values = new float[3];
        EnsureRemaining(stream, 6, "indexed Vector3");
        for (var component = 0; component < 3; component++)
        {
            var index = reader.ReadUInt16();
            if (index >= indexedFloats.Length)
                throw new InvalidDataException("Indexed S3CLIP frame points outside the float table.");
            var value = indexedFloats[index];
            if ((flags & (1 << component)) != 0)
                value = -value;
            values[component] = value * scale + offset;
        }
        return values;
    }

    private static string ReadZStringAt(BinaryReader reader, uint offset)
    {
        if (offset >= reader.BaseStream.Length)
            return string.Empty;
        reader.BaseStream.Position = offset;
        var bytes = new List<byte>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var value = reader.ReadByte();
            if (value == 0)
                break;
            bytes.Add(value);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static void ValidateOffset(uint offset, long length, string name)
    {
        if (offset >= length)
            throw new InvalidDataException($"S3CLIP {name} offset is outside the codec data.");
    }

    private static void EnsureRemaining(Stream stream, long bytes, string field)
    {
        if (stream.Position + bytes > stream.Length)
            throw new InvalidDataException($"S3CLIP ended while reading {field}.");
    }

    private sealed record ClipEnvelope(uint ResourceVersion, string ActorName, ulong RigInstance, int CodecOffset, int CodecLength);
    private sealed record CurveHeader(uint FrameDataOffset, uint TrackKey, float Offset, float Scale, ushort FrameCount, byte Flags, StudioCurveTypeV2 CurveType);

    private sealed class MutableTrack(uint trackKey)
    {
        public uint TrackKey { get; } = trackKey;
        public StudioCurveV2? Position { get; set; }
        public StudioCurveV2? Orientation { get; set; }

        public StudioTrackV2 ToImmutable() => new()
        {
            TrackKey = TrackKey,
            Position = Position,
            Orientation = Orientation
        };
    }
}
