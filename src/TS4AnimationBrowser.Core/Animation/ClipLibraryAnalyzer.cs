using System.Text;

namespace TS4AnimationBrowser.Core.Animation;

public sealed record ClipLibraryInfo(string Name, bool IsRelevant);

public static class ClipLibraryAnalyzer
{
    private static readonly byte[] CodecMagic = Encoding.ASCII.GetBytes("_pilC3S_");

    public static ClipLibraryInfo Analyze(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var name = ClipIdentityReader.ReadDisplayName(data);
        if (string.IsNullOrWhiteSpace(name))
            return new ClipLibraryInfo(string.Empty, false);

        var codecOffset = FindSequence(data, CodecMagic);
        if (codecOffset < 0)
            return new ClipLibraryInfo(name, true);

        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            stream.Position = codecOffset + 8;
            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadSingle();
            var maxFrameCount = reader.ReadUInt16();
            reader.ReadUInt16();
            var curveCount = reader.ReadUInt32();
            reader.ReadUInt32();
            var curveDataOffset = reader.ReadUInt32();

            if (maxFrameCount <= 1 || curveCount == 0)
                return new ClipLibraryInfo(name, false);

            var absoluteCurveOffset = codecOffset + (long)curveDataOffset;
            if (absoluteCurveOffset < codecOffset || absoluteCurveOffset >= stream.Length)
                return new ClipLibraryInfo(name, true);

            stream.Position = absoluteCurveOffset;
            var hasAnimatedCurve = false;
            for (var index = 0u; index < curveCount; index++)
            {
                if (stream.Position + 20 > stream.Length)
                    return new ClipLibraryInfo(name, true);

                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadSingle();
                reader.ReadSingle();
                var frameCount = reader.ReadUInt16();
                reader.ReadByte();
                reader.ReadByte();

                if (frameCount > 1)
                {
                    hasAnimatedCurve = true;
                    break;
                }
            }

            return new ClipLibraryInfo(name, hasAnimatedCurve);
        }
        catch
        {
            return new ClipLibraryInfo(name, true);
        }
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
