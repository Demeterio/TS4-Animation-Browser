using System.Text;

namespace TS4AnimationBrowser.Core.Dbpf;

public sealed record DbpfHeader(
    uint MajorVersion,
    uint MinorVersion,
    uint UserMajorVersion,
    uint UserMinorVersion,
    uint IndexCount,
    ulong IndexOffset,
    uint IndexSize)
{
    public const int Size = 96;

    internal static DbpfHeader Read(BinaryReader reader, long fileLength)
    {
        if (fileLength < Size)
            throw new DbpfFormatException("The file is too small to contain a DBPF header.");

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != "DBPF")
            throw new DbpfFormatException("This file is not a DBPF package (missing DBPF signature).");

        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        var userMajor = reader.ReadUInt32();
        var userMinor = reader.ReadUInt32();

        reader.ReadUInt32(); // unused
        reader.ReadUInt32(); // creation time
        reader.ReadUInt32(); // update time
        reader.ReadUInt32(); // unused

        var indexCount = reader.ReadUInt32();
        var shortIndexOffset = reader.ReadUInt32();
        var indexSize = reader.ReadUInt32();

        reader.BaseStream.Seek(12, SeekOrigin.Current);
        reader.ReadUInt32(); // usually 3 / index version marker
        var longIndexOffset = reader.ReadUInt64();
        reader.BaseStream.Seek(24, SeekOrigin.Current);

        var indexOffset = shortIndexOffset != 0 ? shortIndexOffset : longIndexOffset;

        if (major != 2 || minor != 1)
            throw new DbpfFormatException($"Unsupported DBPF version {major}.{minor}. Expected 2.1.");

        if (indexCount > 0 && indexOffset == 0)
            throw new DbpfFormatException("The package reports resources but has no index offset.");

        if (indexOffset > (ulong)fileLength)
            throw new DbpfFormatException("The DBPF index offset is outside the file.");

        if (indexOffset + indexSize > (ulong)fileLength)
            throw new DbpfFormatException("The DBPF index extends beyond the file.");

        return new DbpfHeader(major, minor, userMajor, userMinor, indexCount, indexOffset, indexSize);
    }
}
