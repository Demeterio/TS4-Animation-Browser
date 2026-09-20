using System.Buffers.Binary;
using System.IO;
using System.Text;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record VfxRcolChunk(
    int Index,
    ResourceKey Key,
    int Offset,
    int Length,
    string Tag,
    ReadOnlyMemory<byte> Data);

public sealed record VfxRcolResource(
    uint Version,
    int PublicChunkCount,
    IReadOnlyList<ResourceKey> ExternalResources,
    IReadOnlyList<VfxRcolChunk> Chunks);

public enum VfxRcolReferenceType : byte
{
    Public = 0,
    Private = 1,
    Delayed = 3
}

public static class VfxRcolDecoder
{
    private const int MaximumEntries = 65_536;

    public static VfxRcolResource Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 20)
            throw new InvalidDataException("The RCOL resource is too small.");

        var reader = new Reader(data);
        var version = reader.ReadUInt32();
        var publicChunkCount = reader.ReadInt32();
        reader.ReadUInt32(); // Reserved field.
        var resourceCount = reader.ReadCount("resource");
        var chunkCount = reader.ReadCount("chunk");
        if (publicChunkCount < 0 || publicChunkCount > chunkCount)
            throw new InvalidDataException($"Invalid RCOL public chunk count {publicChunkCount} for {chunkCount} chunks.");

        var chunkKeys = new ResourceKey[chunkCount];
        for (var index = 0; index < chunkCount; index++)
            chunkKeys[index] = reader.ReadItgKey();

        var resources = new ResourceKey[resourceCount];
        for (var index = 0; index < resourceCount; index++)
            resources[index] = reader.ReadItgKey();

        var offsets = new int[chunkCount];
        var lengths = new int[chunkCount];
        for (var index = 0; index < chunkCount; index++)
        {
            offsets[index] = checked((int)reader.ReadUInt32());
            lengths[index] = reader.ReadInt32();
        }

        // Single-chunk RCOL resources can use an implicit payload offset instead of a useful index entry.
        if (chunkCount == 1)
        {
            offsets[0] = checked(0x2C + (resourceCount * 16));
            lengths[0] = data.Length - offsets[0];
        }

        var chunks = new VfxRcolChunk[chunkCount];
        for (var index = 0; index < chunkCount; index++)
        {
            var offset = offsets[index];
            var length = lengths[index];
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException($"RCOL chunk #{index} points outside the resource (offset={offset}, length={length}).");

            var payload = data.AsMemory(offset, length);
            var tag = ReadFourCc(payload.Span);
            chunks[index] = new VfxRcolChunk(index, chunkKeys[index], offset, length, tag, payload);
        }

        return new VfxRcolResource(version, publicChunkCount, resources, chunks);
    }

    public static VfxRcolReferenceType? GetReferenceType(uint reference)
    {
        if (reference == 0)
            return null;

        return (reference >> 28) switch
        {
            0 => VfxRcolReferenceType.Public,
            1 => VfxRcolReferenceType.Private,
            3 => VfxRcolReferenceType.Delayed,
            _ => null
        };
    }

    public static int GetReferenceIndex(uint reference)
        => reference == 0 ? -1 : checked((int)(reference & 0x0FFFFFFF)) - 1;

    public static int ResolveChunkIndex(VfxRcolResource resource, uint reference)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var index = GetReferenceIndex(reference);
        if (index < 0)
            return -1;

        switch (GetReferenceType(reference))
        {
            case VfxRcolReferenceType.Public:
                break;
            case VfxRcolReferenceType.Private:
                index += resource.PublicChunkCount;
                break;
            default:
                return -1;
        }

        return index >= 0 && index < resource.Chunks.Count ? index : -1;
    }

    public static ResourceKey? ResolveReferenceKey(VfxRcolResource resource, uint reference)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var index = GetReferenceIndex(reference);
        if (index < 0)
            return null;

        switch (GetReferenceType(reference))
        {
            case VfxRcolReferenceType.Public:
                return index < resource.Chunks.Count ? resource.Chunks[index].Key : null;
            case VfxRcolReferenceType.Private:
                index += resource.PublicChunkCount;
                return index < resource.Chunks.Count ? resource.Chunks[index].Key : null;
            case VfxRcolReferenceType.Delayed:
                return index < resource.ExternalResources.Count ? resource.ExternalResources[index] : null;
            default:
                return null;
        }
    }

    private static string ReadFourCc(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return string.Empty;
        for (var index = 0; index < 4; index++)
        {
            if (data[index] is < 0x20 or > 0x7E)
                return string.Empty;
        }
        return Encoding.ASCII.GetString(data[..4]);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public Reader(byte[] data)
        {
            _data = data;
            _position = 0;
        }

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public int ReadCount(string label)
        {
            var value = ReadInt32();
            if (value < 0 || value > MaximumEntries)
                throw new InvalidDataException($"Invalid RCOL {label} count {value}.");
            return value;
        }

        public ResourceKey ReadItgKey()
        {
            Ensure(16);
            var instance = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_position, 8));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position + 8, 4));
            var group = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position + 12, 4));
            _position += 16;
            return new ResourceKey(type, group, instance);
        }

        private void Ensure(int count)
        {
            if (count < 0 || _position < 0 || _position > _data.Length - count)
                throw new InvalidDataException("Unexpected end of RCOL resource.");
        }
    }
}
