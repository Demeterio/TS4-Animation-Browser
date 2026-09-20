using System.Text;

namespace TS4AnimationBrowser.Core.Animation;

public static class ClipIdentityReader
{
    private static readonly byte[] CodecMagic = Encoding.ASCII.GetBytes("_pilC3S_");

    public static string ReadDisplayName(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 48)
            return string.Empty;

        try
        {
            var metadataName = ReadMetadataName(data);
            if (!string.IsNullOrWhiteSpace(metadataName))
                return metadataName;
        }
        catch (InvalidDataException)
        {
            // Some CLIP variants may have metadata we do not understand yet.
            // The codec animation name is still worth trying.
        }

        var codecOffset = FindSequence(data, CodecMagic);
        if (codecOffset < 0)
            return string.Empty;

        try
        {
            return ReadCodecAnimationName(data, codecOffset);
        }
        catch (InvalidDataException)
        {
            return string.Empty;
        }
    }

    private static string ReadMetadataName(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var version = reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadSingle();
        for (var i = 0; i < 7; i++)
            reader.ReadSingle();

        if (version >= 5)
            reader.ReadUInt32();
        if (version >= 10)
        {
            reader.ReadUInt32();
            reader.ReadUInt32();
        }
        if (version >= 11)
            reader.ReadUInt32();

        return version >= 7 ? ReadString32(reader) : string.Empty;
    }

    private static string ReadCodecAnimationName(byte[] data, int codecOffset)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        stream.Position = codecOffset;
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "_pilC3S_")
            throw new InvalidDataException("Invalid CLIP codec marker.");

        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadSingle();
        reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();
        var animationNameOffset = reader.ReadUInt32();
        reader.ReadUInt32();

        var absoluteOffset = codecOffset + (long)animationNameOffset;
        if (absoluteOffset < codecOffset || absoluteOffset >= stream.Length)
            throw new InvalidDataException("CLIP animation name offset is outside the resource.");

        stream.Position = absoluteOffset;
        var bytes = new List<byte>();
        while (stream.Position < stream.Length && bytes.Count < 65_536)
        {
            var value = reader.ReadByte();
            if (value == 0)
                break;
            bytes.Add(value);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string ReadString32(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > 65_536 || reader.BaseStream.Position + length > reader.BaseStream.Length)
            throw new InvalidDataException("Invalid CLIP string length.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
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
}
