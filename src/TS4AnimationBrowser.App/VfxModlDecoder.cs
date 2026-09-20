using System.Buffers.Binary;
using System.IO;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public enum VfxModelLodId : uint
{
    HighDetail = 0x00000000,
    MediumDetail = 0x00000001,
    LowDetail = 0x00000002,
    HighDetailShadow = 0x00010000,
    MediumDetailShadow = 0x00010001,
    LowDetailShadow = 0x00010002
}

public sealed record VfxModlLodEntry(
    uint ModelLodReference,
    uint Flags,
    VfxModelLodId Id,
    float MinZ,
    float MaxZ,
    ResourceKey? ModelLodKey);

public sealed record VfxDecodedModl(
    uint Version,
    IReadOnlyList<VfxModlLodEntry> Entries);

public static class VfxModlDecoder
{
    private const int MaximumLods = 256;

    public static VfxDecodedModl Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rcol = VfxRcolDecoder.Decode(data);
        var modlChunk = rcol.Chunks.FirstOrDefault(chunk => string.Equals(chunk.Tag, "MODL", StringComparison.Ordinal));
        if (modlChunk is null)
            throw new InvalidDataException("The model resource does not contain a MODL chunk.");

        var reader = new Reader(modlChunk.Data.Span);
        reader.ExpectTag("MODL");
        var version = reader.ReadUInt32();
        var count = reader.ReadInt32();
        if (count < 0 || count > MaximumLods)
            throw new InvalidDataException($"Invalid MODL LOD count {count}.");

        reader.Skip(24); // Bounds.
        if (version >= 258 && version < 0x300)
        {
            var extraBounds = reader.ReadInt32();
            if (extraBounds < 0 || extraBounds > 65_536)
                throw new InvalidDataException($"Invalid MODL extra-bounds count {extraBounds}.");
            reader.Skip(checked(extraBounds * 24));
            reader.Skip(8); // FadeType + CustomFadeDistance.
        }
        else if (version >= 0x300)
        {
            reader.Skip(20);
        }

        var entries = new VfxModlLodEntry[count];
        for (var index = 0; index < count; index++)
        {
            var reference = reader.ReadUInt32();
            var flags = reader.ReadUInt32();
            var id = (VfxModelLodId)reader.ReadUInt32();
            var minZ = reader.ReadSingle();
            var maxZ = reader.ReadSingle();
            entries[index] = new VfxModlLodEntry(
                reference,
                flags,
                id,
                minZ,
                maxZ,
                VfxRcolDecoder.ResolveReferenceKey(rcol, reference));
        }

        return new VfxDecodedModl(version, entries);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public Reader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public void ExpectTag(string tag)
        {
            Ensure(4);
            for (var index = 0; index < 4; index++)
            {
                if (_data[_position + index] != (byte)tag[index])
                    throw new InvalidDataException($"Expected {tag} chunk tag.");
            }
            _position += 4;
        }

        public void Skip(int count)
        {
            Ensure(count);
            _position += count;
        }

        private void Ensure(int count)
        {
            if (count < 0 || _position > _data.Length - count)
                throw new InvalidDataException("Unexpected end of MODL chunk.");
        }
    }
}
