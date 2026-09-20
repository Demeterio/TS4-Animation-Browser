using System.Numerics;

namespace TS4AnimationBrowser.Core.Animation;

public enum AnimationCurveKind : byte
{
    Unknown = 0,
    Position = 1,
    Orientation = 2,
    Scale = 3
}

public sealed record AnimationFrame(ushort FrameIndex, float[] Values);

public sealed class AnimationCurve
{
    public required AnimationCurveKind Kind { get; init; }
    public required ClipChannelType ChannelType { get; init; }
    public required ClipSubTarget SubTarget { get; init; }
    public required IReadOnlyList<AnimationFrame> Frames { get; init; }
}

public sealed class AnimationTrack
{
    public required uint TrackKey { get; init; }
    public AnimationCurve? Position { get; init; }
    public AnimationCurve? Orientation { get; init; }
    public AnimationCurve? Scale { get; init; }
}

public sealed class ClipAnimation
{
    public uint Version { get; init; }
    public uint Flags { get; init; }
    public float DurationSeconds { get; init; }
    public Quaternion InitialRotation { get; init; }
    public Vector3 InitialTranslation { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RigNamespace { get; init; } = string.Empty;
    public uint CodecVersion { get; init; }
    public float FrameDuration { get; init; }
    public ushort MaxFrameCount { get; init; }
    public string CodecAnimationName { get; init; } = string.Empty;
    public string CodecSourceName { get; init; } = string.Empty;
    public IReadOnlyList<AnimationTrack> Tracks { get; init; } = Array.Empty<AnimationTrack>();

    public double EffectiveDurationSeconds => DurationSeconds > 0
        ? DurationSeconds
        : FrameDuration > 0 ? MaxFrameCount * FrameDuration : 0;
}
