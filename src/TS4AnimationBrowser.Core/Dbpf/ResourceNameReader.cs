using System.Text;

namespace TS4AnimationBrowser.Core.Dbpf;

public static class ResourceNameReader
{
    private const string ClipMagic = "_pilC3S_";

    public static IReadOnlyDictionary<ulong, string> ReadNameMap(DbpfPackage package, ResourceEntry entry)
    {
        var data = package.ReadResource(entry);
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (stream.Length < 8)
            return new Dictionary<ulong, string>();

        var version = reader.ReadUInt32();
        if (version != 1)
            return new Dictionary<ulong, string>();

        var count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000)
            return new Dictionary<ulong, string>();

        var names = new Dictionary<ulong, string>(count);
        for (var i = 0; i < count; i++)
        {
            if (stream.Position + 12 > stream.Length)
                break;

            var instance = reader.ReadUInt64();
            var charCount = reader.ReadInt32();
            if (charCount < 0 || charCount > 16_384 || stream.Position + charCount > stream.Length)
                break;

            var name = new string(reader.ReadChars(charCount));
            if (!string.IsNullOrWhiteSpace(name))
                names[instance] = name;
        }

        return names;
    }

    public static string? ReadClipName(DbpfPackage package, ResourceEntry entry)
    {
        if (entry.Key.Type != KnownResourceTypes.Clip)
            return null;

        try
        {
            var data = package.ReadResource(entry);
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

            if (stream.Length < 48)
                return null;

            var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
            if (!string.Equals(magic, ClipMagic, StringComparison.Ordinal))
                return null;

            reader.ReadUInt32(); // version
            reader.ReadUInt32(); // unknown
            reader.ReadSingle(); // frame duration
            reader.ReadUInt16(); // max frame count
            reader.ReadUInt16(); // unknown
            reader.ReadUInt32(); // curve count
            reader.ReadUInt32(); // indexed float count
            reader.ReadUInt32(); // curve data offset
            reader.ReadUInt32(); // frame data offset
            var animNameOffset = reader.ReadUInt32();
            reader.ReadUInt32(); // source name offset

            if (animNameOffset >= stream.Length)
                return null;

            stream.Position = animNameOffset;
            var bytes = new List<byte>(64);
            while (stream.Position < stream.Length && bytes.Count < 4096)
            {
                var value = reader.ReadByte();
                if (value == 0)
                    break;
                bytes.Add(value);
            }

            return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
