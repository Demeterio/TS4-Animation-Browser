using System.Numerics;

namespace TS4AnimationBrowser.Core.Animation;

public static class AnimationSampler
{
    public static Vector3 SamplePosition(AnimationCurve? curve, double timeSeconds, float frameDuration, Vector3 fallback)
        => SampleVector3(curve, timeSeconds, frameDuration, fallback);

    public static Vector3 SampleScale(AnimationCurve? curve, double timeSeconds, float frameDuration, Vector3 fallback)
        => SampleVector3(curve, timeSeconds, frameDuration, fallback);

    public static Quaternion SampleOrientation(AnimationCurve? curve, double timeSeconds, float frameDuration, Quaternion fallback)
    {
        if (curve is null || curve.Frames.Count == 0 || frameDuration <= 0)
            return fallback;

        var frame = FindFramePair(curve.Frames, timeSeconds, frameDuration);
        var aValues = frame.A.Values;
        var bValues = frame.B.Values;
        if (aValues.Length < 4 || bValues.Length < 4)
            return fallback;

        var aRaw = new Quaternion(aValues[0], aValues[1], aValues[2], aValues[3]);
        var bRaw = new Quaternion(bValues[0], bValues[1], bValues[2], bValues[3]);
        if (aRaw.LengthSquared() < 0.000001f || bRaw.LengthSquared() < 0.000001f)
            return fallback;

        var a = Quaternion.Normalize(aRaw);
        var b = Quaternion.Normalize(bRaw);
        var result = Quaternion.Slerp(a, b, frame.T);
        return result.LengthSquared() < 0.000001f ? fallback : Quaternion.Normalize(result);
    }

    private static Vector3 SampleVector3(AnimationCurve? curve, double timeSeconds, float frameDuration, Vector3 fallback)
    {
        if (curve is null || curve.Frames.Count == 0 || frameDuration <= 0)
            return fallback;

        var values = SampleValues(curve.Frames, timeSeconds, frameDuration);
        return values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : fallback;
    }

    private static float[] SampleValues(IReadOnlyList<AnimationFrame> frames, double timeSeconds, float frameDuration)
    {
        var frame = FindFramePair(frames, timeSeconds, frameDuration);
        var count = Math.Min(frame.A.Values.Length, frame.B.Values.Length);
        var values = new float[count];
        for (var i = 0; i < count; i++)
            values[i] = frame.A.Values[i] + (frame.B.Values[i] - frame.A.Values[i]) * frame.T;
        return values;
    }

    private static FramePair FindFramePair(IReadOnlyList<AnimationFrame> frames, double timeSeconds, float frameDuration)
    {
        if (frames.Count == 1)
            return new FramePair(frames[0], frames[0], 0);

        var targetFrame = Math.Max(0, timeSeconds / frameDuration);
        AnimationFrame a = frames[0];
        AnimationFrame b = frames[^1];

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

        var t = (float)Math.Clamp((targetFrame - a.FrameIndex) / (b.FrameIndex - a.FrameIndex), 0, 1);
        return new FramePair(a, b, t);
    }

    private readonly record struct FramePair(AnimationFrame A, AnimationFrame B, float T);
}
