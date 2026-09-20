using System.Numerics;
using System.Text;

namespace TS4AnimationBrowser.Core.ViewerV2;

public sealed record StudioRigBoneV2(
    int Index,
    string Name,
    int ParentIndex,
    uint Hash,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 Scale);

public sealed class StudioRigV2
{
    public uint MajorVersion { get; init; }
    public uint MinorVersion { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<StudioRigBoneV2> Bones { get; init; } = Array.Empty<StudioRigBoneV2>();
}

public static class StudioRigDecoderV2
{
    public static StudioRigV2 Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        EnsureRemaining(stream, 12, "RIG header");
        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        if (major is not (3u or 4u) || minor is not (1u or 2u))
            throw new NotSupportedException($"Viewer V2 supports clear Sims 4 RIG v3/v4 only. Found {major}.{minor}.");

        var boneCount = reader.ReadInt32();
        if (boneCount < 0 || boneCount > 100_000)
            throw new InvalidDataException($"Invalid RIG bone count: {boneCount}.");

        var bones = new List<StudioRigBoneV2>(boneCount);
        for (var index = 0; index < boneCount; index++)
        {
            EnsureRemaining(stream, 40, $"bone #{index} transform");
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var orientation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var name = ReadString32(reader);

            EnsureRemaining(stream, 16, $"bone #{index} metadata");
            reader.ReadInt32(); // opposite bone index
            var parentIndex = reader.ReadInt32();
            var hash = reader.ReadUInt32();
            reader.ReadUInt32(); // flags

            bones.Add(new StudioRigBoneV2(index, name, parentIndex, hash, position, NormalizeOrIdentity(orientation), scale));
        }

        var nameValue = major >= 4 ? ReadString32(reader) : string.Empty;
        return new StudioRigV2
        {
            MajorVersion = major,
            MinorVersion = minor,
            Name = nameValue,
            Bones = bones
        };
    }

    private static Quaternion NormalizeOrIdentity(Quaternion value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
            return Quaternion.Identity;
        return value.LengthSquared() < 0.000001f ? Quaternion.Identity : Quaternion.Normalize(value);
    }

    private static string ReadString32(BinaryReader reader)
    {
        EnsureRemaining(reader.BaseStream, 4, "string length");
        var length = reader.ReadInt32();
        if (length < 0 || length > 65_536 || reader.BaseStream.Position + length > reader.BaseStream.Length)
            throw new InvalidDataException("Invalid RIG string length.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static void EnsureRemaining(Stream stream, long bytes, string field)
    {
        if (stream.Position + bytes > stream.Length)
            throw new InvalidDataException($"RIG ended while reading {field}.");
    }
}
