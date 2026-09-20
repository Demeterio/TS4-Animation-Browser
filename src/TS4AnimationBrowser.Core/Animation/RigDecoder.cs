using System.Numerics;
using System.Text;

namespace TS4AnimationBrowser.Core.Animation;

public static class RigDecoder
{
    public static RigSkeleton Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 12)
            throw new InvalidDataException("RIG resource is too small.");

        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        if (major is not (3u or 4u) || minor is not (1u or 2u))
            throw new NotSupportedException("This RIG uses a Granny/binary format that is not decoded yet.");

        var boneCount = reader.ReadInt32();
        if (boneCount < 0 || boneCount > 100_000)
            throw new InvalidDataException("RIG contains an invalid bone count.");

        var bones = new List<RigBone>(boneCount);
        for (var index = 0; index < boneCount; index++)
        {
            EnsureRemaining(stream, 40, "bone transform");
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var orientation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            // The three scale floats are part of the RIG resource, but the reference Blender
            // importer reconstructs the bind armature from Translation * Rotation only.
            // Preserve that behaviour here so bind scale is not multiplied through the hierarchy.
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();
            var scale = Vector3.One;

            var boneName = ReadString32(reader);
            EnsureRemaining(stream, 16, "bone metadata");
            reader.ReadInt32();
            var parentIndex = reader.ReadInt32();
            var hash = reader.ReadUInt32();
            reader.ReadUInt32();

            bones.Add(new RigBone
            {
                Index = index,
                Name = boneName,
                ParentIndex = parentIndex,
                Hash = hash,
                Position = position,
                Orientation = orientation,
                Scale = scale
            });
        }

        var skeletonName = major >= 4 ? ReadString32(reader) : string.Empty;
        return new RigSkeleton
        {
            MajorVersion = major,
            MinorVersion = minor,
            Name = skeletonName,
            Bones = bones
        };
    }

    private static string ReadString32(BinaryReader reader)
    {
        EnsureRemaining(reader.BaseStream, 4, "string length");
        var length = reader.ReadInt32();
        if (length < 0 || length > 65_536 || reader.BaseStream.Position + length > reader.BaseStream.Length)
            throw new InvalidDataException("RIG contains an invalid string length.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static void EnsureRemaining(Stream stream, long count, string field)
    {
        if (stream.Position + count > stream.Length)
            throw new InvalidDataException($"RIG ends unexpectedly while reading {field}.");
    }
}
