using System.Numerics;

namespace TS4AnimationBrowser.Core.ViewerV2;

public enum StudioPoseEvaluationModeV2
{
    Raw,
    BodyPreview
}

public sealed class StudioPoseFrameV2
{
    public required Matrix4x4[] WorldTransforms { get; init; }
    public required Vector3[] WorldPositions { get; init; }
}

public sealed class StudioPoseEvaluatorV2
{
    private const string RootBoneName = "b__ROOT__";

    private readonly StudioRigV2 _rig;
    private readonly StudioClipV2 _clip;
    private readonly IReadOnlyDictionary<uint, StudioTrackV2> _tracks;
    private readonly StudioPoseEvaluationModeV2 _mode;

    public StudioPoseEvaluatorV2(
        StudioRigV2 rig,
        StudioClipV2 clip,
        StudioPoseEvaluationModeV2 mode = StudioPoseEvaluationModeV2.Raw)
    {
        _rig = rig;
        _clip = clip;
        _mode = mode;
        _tracks = clip.Tracks.ToDictionary(track => track.TrackKey);
    }

    public StudioPoseFrameV2 Evaluate(double timeSeconds)
    {
        var targetFrame = _clip.FrameDuration > 0
            ? Math.Max(0, timeSeconds / _clip.FrameDuration)
            : 0;
        return EvaluateFrame(targetFrame, useAnimation: true);
    }

    public StudioPoseFrameV2 EvaluateBindPose() => EvaluateFrame(0, useAnimation: false);

    private StudioPoseFrameV2 EvaluateFrame(double targetFrame, bool useAnimation)
    {
        var world = new Matrix4x4[_rig.Bones.Count];
        var resolved = new bool[_rig.Bones.Count];
        var resolving = new bool[_rig.Bones.Count];

        for (var i = 0; i < _rig.Bones.Count; i++)
            ResolveWorld(i, targetFrame, useAnimation, world, resolved, resolving);

        var positions = new Vector3[world.Length];
        for (var i = 0; i < world.Length; i++)
            positions[i] = Vector3.Transform(Vector3.Zero, world[i]);

        return new StudioPoseFrameV2
        {
            WorldTransforms = world,
            WorldPositions = positions
        };
    }

    private Matrix4x4 ResolveWorld(
        int index,
        double targetFrame,
        bool useAnimation,
        Matrix4x4[] world,
        bool[] resolved,
        bool[] resolving)
    {
        if (resolved[index])
            return world[index];
        if (resolving[index])
            return Matrix4x4.Identity;

        resolving[index] = true;
        var bone = _rig.Bones[index];
        var translation = bone.Position;
        var rotation = NormalizeOrFallback(bone.Orientation, Quaternion.Identity);

        if (useAnimation && _tracks.TryGetValue(bone.Hash, out var track))
        {
            if (_mode == StudioPoseEvaluationModeV2.BodyPreview)
            {
                translation = SamplePositionInterpolated(track.Position, targetFrame, translation);
                rotation = SampleOrientationInterpolated(track.Orientation, targetFrame, rotation);
            }
            else
            {
                var sampledPosition = SampleLatest(track.Position, targetFrame, 3);
                if (sampledPosition is not null)
                    translation = new Vector3(sampledPosition[0], sampledPosition[1], sampledPosition[2]);

                var sampledOrientation = SampleLatest(track.Orientation, targetFrame, 4);
                if (sampledOrientation is not null)
                {
                    if (bone.Name.Equals(RootBoneName, StringComparison.OrdinalIgnoreCase))
                    {
                        rotation = Quaternion.Identity;
                    }
                    else
                    {
                        var value = new Quaternion(
                            sampledOrientation[0],
                            sampledOrientation[1],
                            sampledOrientation[2],
                            sampledOrientation[3]);
                        rotation = NormalizeOrFallback(value, rotation);
                    }
                }
            }
        }

        Matrix4x4 local;
        if (_mode == StudioPoseEvaluationModeV2.BodyPreview)
        {
            // Match the original Viewer V2 body resolver that produced the readable human armature:
            // RIG scale participates in the hierarchy, and CLIP position/orientation are evaluated
            // as local bone transforms. Object/raw viewing keeps the later S4Studio-oriented path.
            var scale = bone.Scale;
            if (!IsFinite(scale)
                || (Math.Abs(scale.X) < 0.000001f
                    && Math.Abs(scale.Y) < 0.000001f
                    && Math.Abs(scale.Z) < 0.000001f))
            {
                scale = Vector3.One;
            }

            local = Matrix4x4.CreateScale(scale)
                * Matrix4x4.CreateFromQuaternion(rotation)
                * Matrix4x4.CreateTranslation(translation);
        }
        else
        {
            local = Matrix4x4.CreateFromQuaternion(rotation)
                * Matrix4x4.CreateTranslation(translation);
        }

        if (bone.ParentIndex >= 0 && bone.ParentIndex < _rig.Bones.Count && bone.ParentIndex != index)
        {
            var parentWorld = ResolveWorld(bone.ParentIndex, targetFrame, useAnimation, world, resolved, resolving);
            world[index] = local * parentWorld;
        }
        else
        {
            world[index] = local;
        }

        resolving[index] = false;
        resolved[index] = true;
        return world[index];
    }

    private static Vector3 SamplePositionInterpolated(StudioCurveV2? curve, double targetFrame, Vector3 fallback)
    {
        if (curve is null || curve.Frames.Count == 0)
            return fallback;

        var pair = FindFramePair(curve.Frames, targetFrame);
        if (pair.A.Values.Length < 3 || pair.B.Values.Length < 3)
            return fallback;

        return new Vector3(
            Lerp(pair.A.Values[0], pair.B.Values[0], pair.T),
            Lerp(pair.A.Values[1], pair.B.Values[1], pair.T),
            Lerp(pair.A.Values[2], pair.B.Values[2], pair.T));
    }

    private static Quaternion SampleOrientationInterpolated(StudioCurveV2? curve, double targetFrame, Quaternion fallback)
    {
        if (curve is null || curve.Frames.Count == 0)
            return fallback;

        var pair = FindFramePair(curve.Frames, targetFrame);
        if (pair.A.Values.Length < 4 || pair.B.Values.Length < 4)
            return fallback;

        var a = NormalizeOrFallback(
            new Quaternion(pair.A.Values[0], pair.A.Values[1], pair.A.Values[2], pair.A.Values[3]),
            fallback);
        var b = NormalizeOrFallback(
            new Quaternion(pair.B.Values[0], pair.B.Values[1], pair.B.Values[2], pair.B.Values[3]),
            a);
        return NormalizeOrFallback(Quaternion.Slerp(a, b, pair.T), a);
    }

    private static FramePair FindFramePair(IReadOnlyList<StudioFrameV2> frames, double targetFrame)
    {
        if (frames.Count == 1)
            return new FramePair(frames[0], frames[0], 0);

        var a = frames[0];
        var b = frames[^1];
        for (var i = 1; i < frames.Count; i++)
        {
            if (frames[i].FrameIndex < targetFrame)
            {
                a = frames[i];
                continue;
            }

            b = frames[i];
            break;
        }

        if (b.FrameIndex <= a.FrameIndex)
            return new FramePair(a, b, 0);

        var t = (float)Math.Clamp(
            (targetFrame - a.FrameIndex) / (b.FrameIndex - a.FrameIndex),
            0,
            1);
        return new FramePair(a, b, t);
    }

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    private static float[]? SampleLatest(StudioCurveV2? curve, double targetFrame, int requiredValues)
    {
        if (curve is null || curve.Frames.Count == 0)
            return null;

        StudioFrameV2? selected = null;
        foreach (var frame in curve.Frames)
        {
            if (frame.FrameIndex > targetFrame)
                break;
            selected = frame;
        }

        return selected is not null && selected.Values.Length >= requiredValues ? selected.Values : null;
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static Quaternion NormalizeOrFallback(Quaternion value, Quaternion fallback)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
            return fallback;
        return value.LengthSquared() < 0.000001f ? fallback : Quaternion.Normalize(value);
    }

    private readonly record struct FramePair(StudioFrameV2 A, StudioFrameV2 B, float T);
}
