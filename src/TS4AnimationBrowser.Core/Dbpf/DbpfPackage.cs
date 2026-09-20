using System.IO.Compression;

namespace TS4AnimationBrowser.Core.Dbpf;

public sealed class DbpfPackage
{
    private const uint ConstantTypeFlag = 0x01;
    private const uint ConstantGroupFlag = 0x02;
    private const uint ConstantInstanceHighFlag = 0x04;
    private const uint ExtendedEntryFlag = 0x80000000;

    private DbpfPackage(string path, DbpfHeader header, IReadOnlyList<ResourceEntry> entries)
    {
        Path = path;
        Header = header;
        Entries = entries;
    }

    public string Path { get; }
    public DbpfHeader Header { get; }
    public IReadOnlyList<ResourceEntry> Entries { get; }

    public static DbpfPackage Open(string path, Func<uint, bool>? typeFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        var header = DbpfHeader.Read(reader, stream.Length);
        var entries = ReadIndex(reader, header, stream.Length, typeFilter);
        return new DbpfPackage(path, header, entries);
    }

    public byte[] ReadResource(ResourceEntry entry) => ReadResource(Path, entry);

    public static byte[] ReadResource(string path, ResourceEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var end = (ulong)entry.Offset + entry.CompressedSize;
        if (end > (ulong)stream.Length)
            throw new InvalidDataException($"Resource {entry.Key} extends beyond the end of its package.");

        stream.Position = entry.Offset;
        var raw = new byte[checked((int)entry.CompressedSize)];
        stream.ReadExactly(raw);

        return entry.Compression switch
        {
            DbpfCompressionType.None => raw,
            DbpfCompressionType.Zlib => InflateZlib(raw, entry.UncompressedSize),
            DbpfCompressionType.Internal => InflateRefPack(raw, entry.UncompressedSize),
            DbpfCompressionType.Deleted => Array.Empty<byte>(),
            _ => throw new NotSupportedException(
                $"Compression 0x{(ushort)entry.Compression:X4} is not supported yet for {entry.Key}.")
        };
    }

    private static List<ResourceEntry> ReadIndex(
        BinaryReader reader,
        DbpfHeader header,
        long fileLength,
        Func<uint, bool>? typeFilter)
    {
        var capacity = typeFilter is null ? checked((int)header.IndexCount) : 1024;
        var entries = new List<ResourceEntry>(capacity);
        if (header.IndexCount == 0)
            return entries;

        reader.BaseStream.Position = checked((long)header.IndexOffset);
        var flags = reader.ReadUInt32();

        uint constantType = 0;
        uint constantGroup = 0;
        uint constantInstanceHigh = 0;

        if ((flags & ConstantTypeFlag) != 0)
            constantType = reader.ReadUInt32();
        if ((flags & ConstantGroupFlag) != 0)
            constantGroup = reader.ReadUInt32();
        if ((flags & ConstantInstanceHighFlag) != 0)
            constantInstanceHigh = reader.ReadUInt32();

        for (var i = 0u; i < header.IndexCount; i++)
        {
            var type = (flags & ConstantTypeFlag) != 0 ? constantType : reader.ReadUInt32();
            var group = (flags & ConstantGroupFlag) != 0 ? constantGroup : reader.ReadUInt32();
            var instanceHigh = (flags & ConstantInstanceHighFlag) != 0
                ? constantInstanceHigh
                : reader.ReadUInt32();

            var instanceLow = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            var sizeAndFlags = reader.ReadUInt32();
            var uncompressedSize = reader.ReadUInt32();

            var hasExtendedMetadata = (sizeAndFlags & ExtendedEntryFlag) != 0;
            var compressedSize = sizeAndFlags & ~ExtendedEntryFlag;

            DbpfCompressionType compression = DbpfCompressionType.None;
            ushort committed = 1;
            if (hasExtendedMetadata)
            {
                compression = (DbpfCompressionType)reader.ReadUInt16();
                committed = reader.ReadUInt16();
            }

            if (typeFilter is not null && !typeFilter(type))
                continue;

            var end = (ulong)offset + compressedSize;
            if (end > (ulong)fileLength)
                throw new DbpfFormatException($"Resource #{i} extends beyond the end of the package.");

            var instance = ((ulong)instanceHigh << 32) | instanceLow;
            entries.Add(new ResourceEntry(
                new ResourceKey(type, group, instance),
                offset,
                compressedSize,
                uncompressedSize,
                compression,
                committed,
                hasExtendedMetadata));
        }

        return entries;
    }

    private static byte[] InflateZlib(byte[] compressed, uint expectedSize)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = expectedSize > 0 && expectedSize <= int.MaxValue
            ? new MemoryStream((int)expectedSize)
            : new MemoryStream();

        zlib.CopyTo(output);
        var result = output.ToArray();
        ValidateInflatedSize(result, expectedSize, "zlib");
        return result;
    }

    private static byte[] InflateRefPack(byte[] compressed, uint expectedSize)
    {
        if (compressed.Length < 5)
            throw new InvalidDataException("RefPack resource is too small.");

        var input = 0;
        var flags = compressed[input++];
        if (compressed[input++] != 0xFB)
            throw new InvalidDataException("Invalid RefPack header.");

        int declaredSize;
        if ((flags & 0x80) != 0)
        {
            EnsureInput(compressed, input, 4);
            declaredSize = (compressed[input] << 24)
                | (compressed[input + 1] << 16)
                | (compressed[input + 2] << 8)
                | compressed[input + 3];
            input += 4;
        }
        else
        {
            EnsureInput(compressed, input, 3);
            declaredSize = (compressed[input] << 16)
                | (compressed[input + 1] << 8)
                | compressed[input + 2];
            input += 3;
        }

        if (declaredSize < 0)
            throw new InvalidDataException("Invalid RefPack output size.");

        var output = new List<byte>(declaredSize > 0 ? declaredSize : checked((int)expectedSize));
        while (input < compressed.Length && output.Count < declaredSize)
        {
            var control = compressed[input++];
            int literalCount;
            int copyCount = 0;
            int copyOffset = 0;

            if (control <= 0x7F)
            {
                EnsureInput(compressed, input, 1);
                var b1 = compressed[input++];
                literalCount = control & 0x03;
                copyCount = ((control & 0x1C) >> 2) + 3;
                copyOffset = ((control & 0x60) << 3) + b1;
            }
            else if (control <= 0xBF)
            {
                EnsureInput(compressed, input, 2);
                var b1 = compressed[input++];
                var b2 = compressed[input++];
                literalCount = (b1 & 0xC0) >> 6;
                copyCount = (control & 0x3F) + 4;
                copyOffset = ((b1 & 0x3F) << 8) + b2;
            }
            else if (control <= 0xDF)
            {
                EnsureInput(compressed, input, 3);
                var b1 = compressed[input++];
                var b2 = compressed[input++];
                var b3 = compressed[input++];
                literalCount = control & 0x03;
                copyCount = ((control & 0x0C) << 6) + b3 + 5;
                copyOffset = ((control & 0x10) << 12) + (b1 << 8) + b2;
            }
            else if (control <= 0xFB)
            {
                literalCount = ((control & 0x1F) << 2) + 4;
            }
            else
            {
                literalCount = control & 0x03;
            }

            EnsureInput(compressed, input, literalCount);
            for (var i = 0; i < literalCount; i++)
                output.Add(compressed[input++]);

            if (copyCount == 0)
                continue;

            var source = output.Count - copyOffset - 1;
            if (source < 0)
                throw new InvalidDataException("RefPack back-reference points before the output buffer.");

            for (var i = 0; i < copyCount; i++)
            {
                if (source + i >= output.Count)
                {
                    var overlapIndex = source + i;
                    if (overlapIndex < 0 || overlapIndex >= output.Count)
                        throw new InvalidDataException("Invalid RefPack overlapping back-reference.");
                }
                output.Add(output[source + i]);
                if (output.Count > declaredSize)
                    throw new InvalidDataException("RefPack produced more data than declared.");
            }
        }

        var result = output.ToArray();
        if (declaredSize != 0 && result.Length != declaredSize)
            throw new InvalidDataException($"RefPack size mismatch: expected {declaredSize:N0}, got {result.Length:N0} bytes.");
        ValidateInflatedSize(result, expectedSize, "RefPack");
        return result;
    }

    private static void EnsureInput(byte[] data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > data.Length)
            throw new InvalidDataException("RefPack stream ends unexpectedly.");
    }

    private static void ValidateInflatedSize(byte[] result, uint expectedSize, string codec)
    {
        if (expectedSize != 0 && result.LongLength != expectedSize)
            throw new InvalidDataException($"{codec} size mismatch: expected {expectedSize:N0}, got {result.LongLength:N0} bytes.");
    }
}
