using System.Numerics;
using TS4AnimationBrowser.Core.ViewerV2;

namespace TS4AnimationBrowser.App;

public sealed record LimbLengthDiagnostic(
    string BoneName,
    string ParentName,
    float BindLength,
    float FirstLength,
    float MinLength,
    float MaxLength,
    ushort MaxFrame,
    bool HasPositionTrack)
{
    public float MaxRatio => BindLength > 0.000001f ? MaxLength / BindLength : 0;
    public float MinRatio => BindLength > 0.000001f ? MinLength / BindLength : 0;
    public bool IsSuspicious => BindLength > 0.000001f && (MaxRatio > 1.25f || MinRatio < 0.75f);
}

public static class AnimationPoseDiagnostics
{
    public static IReadOnlyList<LimbLengthDiagnostic> Analyze(StudioRigV2 rig, StudioClipV2 clip)
    {
        var tracks = clip.Tracks.ToDictionary(track => track.TrackKey);
        var result = new List<LimbLengthDiagnostic>();

        foreach (var bone in rig.Bones)
        {
            if (!IsMainLimbBone(bone.Name))
                continue;

            var parentName = bone.ParentIndex >= 0 && bone.ParentIndex < rig.Bones.Count
                ? rig.Bones[bone.ParentIndex].Name
                : "—";
            var bindLength = bone.Position.Length();
            var firstLength = bindLength;
            var minLength = bindLength;
            var maxLength = bindLength;
            ushort maxFrame = 0;
            var hasPositionTrack = false;

            if (tracks.TryGetValue(bone.Hash, out var track) && track.Position is { Frames.Count: > 0 } position)
            {
                hasPositionTrack = true;
                var firstResolved = false;
                foreach (var frame in position.Frames)
                {
                    if (frame.Values.Length < 3)
                        continue;

                    var value = new Vector3(frame.Values[0], frame.Values[1], frame.Values[2]);
                    if (!IsFinite(value))
                        continue;

                    var length = value.Length();
                    if (!firstResolved)
                    {
                        firstLength = length;
                        firstResolved = true;
                    }

                    if (length < minLength)
                        minLength = length;
                    if (length > maxLength)
                    {
                        maxLength = length;
                        maxFrame = frame.FrameIndex;
                    }
                }
            }

            result.Add(new LimbLengthDiagnostic(
                bone.Name,
                parentName,
                bindLength,
                firstLength,
                minLength,
                maxLength,
                maxFrame,
                hasPositionTrack));
        }

        return result
            .OrderByDescending(item => item.IsSuspicious)
            .ThenByDescending(item => Math.Max(item.MaxRatio, item.MinRatio > 0 ? 1f / item.MinRatio : 0))
            .ThenBy(item => item.BoneName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsMainLimbBone(string name)
    {
        var value = name.ToLowerInvariant();
        if (ContainsAny(value, "compress", "twist", "scale", "offset", "target", "slot", "export", "driver", "world", "subroot"))
            return false;

        return value.Contains("upperarm")
            || value.Contains("forearm")
            || IsMainHandBone(value)
            || value.Contains("thigh")
            || value.Contains("calf")
            || value.Contains("shin")
            || IsMainFootBone(value);
    }

    private static bool IsMainHandBone(string value)
        => value.Contains("hand") && !ContainsAny(value, "finger", "thumb", "index", "mid", "middle", "ring", "pinky", "prop");

    private static bool IsMainFootBone(string value)
        => value.Contains("foot") && !ContainsAny(value, "toe", "heel");

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool ContainsAny(string value, params string[] values)
        => values.Any(value.Contains);
}
