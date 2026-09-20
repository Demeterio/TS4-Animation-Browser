using System.Numerics;

namespace TS4AnimationBrowser.Core.Animation;

public sealed class RigBone
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required int ParentIndex { get; init; }
    public required uint Hash { get; init; }
    public required Vector3 Position { get; init; }
    public required Quaternion Orientation { get; init; }
    public required Vector3 Scale { get; init; }
}

public sealed class RigSkeleton
{
    public uint MajorVersion { get; init; }
    public uint MinorVersion { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<RigBone> Bones { get; init; } = Array.Empty<RigBone>();

    public IReadOnlyDictionary<uint, RigBone> BonesByHash => _bonesByHash ??= BuildBoneMap();
    private Dictionary<uint, RigBone>? _bonesByHash;

    private Dictionary<uint, RigBone> BuildBoneMap()
    {
        var map = new Dictionary<uint, RigBone>();
        foreach (var bone in Bones)
            map.TryAdd(bone.Hash, bone);
        return map;
    }
}
