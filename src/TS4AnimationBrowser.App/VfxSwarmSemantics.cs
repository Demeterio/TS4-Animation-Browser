namespace TS4AnimationBrowser.App;

public enum VfxParticleAlignment : byte
{
    Camera = 0,
    Ground = 1,
    DirectionX = 2,
    DirectionY = 3,
    DirectionZ = 4,
    Source = 5,
    ZPole = 6,
    SunPole = 7,
    CameraLocation = 8
}

public enum VfxBaseDrawMode : byte
{
    Decal = 0x00,
    DecalInvertDepth = 0x01,
    DecalIgnoreDepth = 0x02,
    DepthDecal = 0x03,
    Additive = 0x04,
    AdditiveInvertDepth = 0x05,
    AdditiveIgnoreDepth = 0x06,
    Modulate = 0x07,
    NormalMap = 0x08,
    DepthNormalMap = 0x09,
    AlphaTestDissolve = 0x0A,
    User1 = 0x0B,
    User2 = 0x0C,
    User3 = 0x0D,
    User4 = 0x0E,
    None = 0x0F
}

public static class VfxSwarmSemantics
{
    public const ushort TransformScale = 0x0001;
    public const ushort TransformRotate = 0x0002;
    public const ushort TransformOffset = 0x0004;

    public const uint VisualViewRelative = 0x00000001;
    public const uint VisualCameraFacing = 0x00000002;
    public const uint VisualCameraAttached = 0x00000004;
    public const uint VisualCameraAttachedRigid = 0x00000008;
    public const uint VisualNoAutoStop = 0x00000010;
    public const uint VisualHardStop = 0x00000020;
    public const uint VisualRigid = 0x00000040;
    public const uint VisualIgnoreParams = 0x00000080;
    public const uint VisualApplyCursor = 0x00000100;
    public const uint VisualIgnoreScale = 0x00000200;
    public const uint VisualIgnoreOrientation = 0x00000400;
    public const uint VisualOrientZPole = 0x00000800;
    public const uint VisualGameTime = 0x00001000;
    public const uint VisualRealTime = 0x00002000;
    public const uint VisualDetach = 0x00004000;
    public const uint VisualClampScreenSize = 0x00008000;

    public const ulong ParticleInject = 1UL << 0;
    public const ulong ParticleMaintain = 1UL << 1;
    public const ulong ParticleRateSustain = 1UL << 2;
    public const ulong ParticleEmitBase = 1UL << 3;
    public const ulong ParticleSourceRound = 1UL << 4;
    public const ulong ParticleMapEmitPinToSurface = 1UL << 5;
    public const ulong ParticleMapEmitHeightRange = 1UL << 6;
    public const ulong ParticleMapEmitDensity = 1UL << 7;
    public const ulong ParticleRateSizeScale = 1UL << 8;
    public const ulong ParticleRateAreaScale = 1UL << 9;
    public const ulong ParticleRateVolumeScale = 1UL << 10;
    public const ulong ParticleSourceScaleParticles = 1UL << 11;
    public const ulong ParticleSurfaces = 1UL << 12;
    public const ulong ParticleMapCollide = 1UL << 13;
    public const ulong ParticleMapRepel = 1UL << 14;
    public const ulong ParticleMapAdvect = 1UL << 15;
    public const ulong ParticleMapForce = 1UL << 16;
    public const ulong ParticleKillOutsideMap = 1UL << 17;
    public const ulong ParticleMapCollidePinToMap = 1UL << 18;
    public const ulong ParticleRandomWalk = 1UL << 19;
    public const ulong ParticleRandomWalkWait = 1UL << 20;
    public const ulong ParticleModel = 1UL << 21;
    public const ulong ParticleTextureAcceptComposite = 1UL << 22;
    public const ulong ParticleAttractor = 1UL << 23;
    public const ulong ParticleNotPresetAttractor = 1UL << 24;
    public const ulong ParticleEmitScaleExisting = 1UL << 25;
    public const ulong ParticleSourceResetIncoming = 1UL << 26;
    public const ulong ParticleRateKill = 1UL << 27;
    public const ulong ParticleRateHold = 1UL << 28;
    public const ulong ParticleWarpSpiral = 1UL << 29;
    public const ulong ParticleLoopBox = 1UL << 30;
    public const ulong ParticlePath = 1UL << 31;
    public const ulong ParticlePropagateAlways = 1UL << 32;
    public const ulong ParticlePropagateIfKilled = 1UL << 33;
    public const ulong ParticleFramesRelativeSpeed = 1UL << 34;
    public const ulong ParticleColorVaryRgb = 1UL << 35;

    public static bool UsesScale(VfxSwarmVisualBlock block)
        => (block.LocalTransformFlags & TransformScale) != 0;

    public static bool UsesRotation(VfxSwarmVisualBlock block)
        => (block.LocalTransformFlags & TransformRotate) != 0;

    public static bool UsesOffset(VfxSwarmVisualBlock block)
        => (block.LocalTransformFlags & TransformOffset) != 0;

    public static bool IsModelParticle(VfxSwarmParticleEffect effect)
        => (effect.Flags & ParticleModel) != 0;

    public static bool HasAttractor(VfxSwarmParticleEffect effect)
        => (effect.Flags & ParticleAttractor) != 0;

    public static VfxParticleAlignment? GetAlignment(byte value)
        => value <= (byte)VfxParticleAlignment.CameraLocation
            ? (VfxParticleAlignment)value
            : null;

    public static string FormatAlignment(byte value)
        => GetAlignment(value) switch
        {
            VfxParticleAlignment.Camera => "camera",
            VfxParticleAlignment.Ground => "ground",
            VfxParticleAlignment.DirectionX => "dirX",
            VfxParticleAlignment.DirectionY => "dirY",
            VfxParticleAlignment.DirectionZ => "dirZ",
            VfxParticleAlignment.Source => "source",
            VfxParticleAlignment.ZPole => "zPole",
            VfxParticleAlignment.SunPole => "sunPole",
            VfxParticleAlignment.CameraLocation => "cameraLocation",
            _ => $"unknown({value})"
        };

    public static VfxBaseDrawMode GetBaseDrawMode(byte rawDrawMode)
        => (VfxBaseDrawMode)(rawDrawMode & 0x0F);

    public static string FormatDrawMode(byte rawDrawMode)
    {
        var baseMode = GetBaseDrawMode(rawDrawMode);
        var name = baseMode switch
        {
            VfxBaseDrawMode.Decal => "decal",
            VfxBaseDrawMode.DecalInvertDepth => "decalInvertDepth",
            VfxBaseDrawMode.DecalIgnoreDepth => "decalIgnoreDepth",
            VfxBaseDrawMode.DepthDecal => "depthDecal",
            VfxBaseDrawMode.Additive => "additive",
            VfxBaseDrawMode.AdditiveInvertDepth => "additiveInvertDepth",
            VfxBaseDrawMode.AdditiveIgnoreDepth => "additiveIgnoreDepth",
            VfxBaseDrawMode.Modulate => "modulate",
            VfxBaseDrawMode.NormalMap => "normalMap",
            VfxBaseDrawMode.DepthNormalMap => "depthNormalMap",
            VfxBaseDrawMode.AlphaTestDissolve => "alphaTestDissolve",
            VfxBaseDrawMode.User1 => "user1",
            VfxBaseDrawMode.User2 => "user2",
            VfxBaseDrawMode.User3 => "user3",
            VfxBaseDrawMode.User4 => "user4",
            VfxBaseDrawMode.None => "none",
            _ => "unknown"
        };
        return $"0x{rawDrawMode:X2} → 0x{(byte)baseMode:X2} {name}";
    }

    public static bool IsAdditive(byte rawDrawMode)
        => GetBaseDrawMode(rawDrawMode) is VfxBaseDrawMode.Additive
            or VfxBaseDrawMode.AdditiveInvertDepth
            or VfxBaseDrawMode.AdditiveIgnoreDepth;

    public static string FormatTransformFlags(ushort flags)
    {
        if (flags == 0)
            return "default";

        var values = new List<string>(3);
        if ((flags & TransformScale) != 0)
            values.Add("scale");
        if ((flags & TransformRotate) != 0)
            values.Add("rotate");
        if ((flags & TransformOffset) != 0)
            values.Add("offset");

        var unknown = flags & ~(TransformScale | TransformRotate | TransformOffset);
        if (unknown != 0)
            values.Add($"unknown:0x{unknown:X4}");
        return string.Join("+", values);
    }

    public static string FormatParticleFlags(ulong flags)
    {
        if (flags == 0)
            return "none";

        var values = new List<string>();
        AddFlag(values, flags, ParticleInject, "inject");
        AddFlag(values, flags, ParticleMaintain, "maintain");
        AddFlag(values, flags, ParticleRateSustain, "rateSustain");
        AddFlag(values, flags, ParticleEmitBase, "emitBase");
        AddFlag(values, flags, ParticleSourceRound, "sourceRound");
        AddFlag(values, flags, ParticleMapEmitPinToSurface, "mapEmitPinToSurface");
        AddFlag(values, flags, ParticleMapEmitHeightRange, "mapEmitHeightRange");
        AddFlag(values, flags, ParticleMapEmitDensity, "mapEmitDensity");
        AddFlag(values, flags, ParticleRateSizeScale, "rateSizeScale");
        AddFlag(values, flags, ParticleRateAreaScale, "rateAreaScale");
        AddFlag(values, flags, ParticleRateVolumeScale, "rateVolumeScale");
        AddFlag(values, flags, ParticleSourceScaleParticles, "sourceScaleParticles");
        AddFlag(values, flags, ParticleSurfaces, "surfaces");
        AddFlag(values, flags, ParticleMapCollide, "mapCollide");
        AddFlag(values, flags, ParticleMapRepel, "mapRepel");
        AddFlag(values, flags, ParticleMapAdvect, "mapAdvect");
        AddFlag(values, flags, ParticleMapForce, "mapForce");
        AddFlag(values, flags, ParticleKillOutsideMap, "killOutsideMap");
        AddFlag(values, flags, ParticleMapCollidePinToMap, "mapCollidePinToMap");
        AddFlag(values, flags, ParticleRandomWalk, "randomWalk");
        AddFlag(values, flags, ParticleRandomWalkWait, "randomWalkWait");
        AddFlag(values, flags, ParticleModel, "model");
        AddFlag(values, flags, ParticleTextureAcceptComposite, "textureAcceptComposite");
        AddFlag(values, flags, ParticleAttractor, "attractor");
        AddFlag(values, flags, ParticleNotPresetAttractor, "notPresetAttractor");
        AddFlag(values, flags, ParticleEmitScaleExisting, "emitScaleExisting");
        AddFlag(values, flags, ParticleSourceResetIncoming, "sourceResetIncoming");
        AddFlag(values, flags, ParticleRateKill, "rateKill");
        AddFlag(values, flags, ParticleRateHold, "rateHold");
        AddFlag(values, flags, ParticleWarpSpiral, "warpSpiral");
        AddFlag(values, flags, ParticleLoopBox, "loopBox");
        AddFlag(values, flags, ParticlePath, "path");
        AddFlag(values, flags, ParticlePropagateAlways, "propagateAlways");
        AddFlag(values, flags, ParticlePropagateIfKilled, "propagateIfKilled");
        AddFlag(values, flags, ParticleFramesRelativeSpeed, "framesRelativeSpeed");
        AddFlag(values, flags, ParticleColorVaryRgb, "colorVaryRgb");

        const ulong known = (1UL << 36) - 1;
        var unknown = flags & ~known;
        if (unknown != 0)
            values.Add($"unknown:0x{unknown:X16}");
        return string.Join(", ", values);
    }

    public static string FormatVisualEffectFlags(uint flags)
    {
        if (flags == 0)
            return "none";

        var values = new List<string>();
        AddFlag(values, flags, VisualViewRelative, "viewRelative");
        AddFlag(values, flags, VisualCameraFacing, "cameraFacing");
        AddFlag(values, flags, VisualCameraAttached, "cameraAttached");
        AddFlag(values, flags, VisualCameraAttachedRigid, "cameraAttachedRigid");
        AddFlag(values, flags, VisualNoAutoStop, "noAutoStop");
        AddFlag(values, flags, VisualHardStop, "hardStop");
        AddFlag(values, flags, VisualRigid, "rigid");
        AddFlag(values, flags, VisualIgnoreParams, "ignoreParams");
        AddFlag(values, flags, VisualApplyCursor, "applyCursor");
        AddFlag(values, flags, VisualIgnoreScale, "ignoreScale");
        AddFlag(values, flags, VisualIgnoreOrientation, "ignoreOrientation");
        AddFlag(values, flags, VisualOrientZPole, "orientZPole");
        AddFlag(values, flags, VisualGameTime, "gameTime");
        AddFlag(values, flags, VisualRealTime, "realTime");
        AddFlag(values, flags, VisualDetach, "detach");
        AddFlag(values, flags, VisualClampScreenSize, "clampScreenSize");

        const uint known = VisualViewRelative | VisualCameraFacing | VisualCameraAttached
            | VisualCameraAttachedRigid | VisualNoAutoStop | VisualHardStop | VisualRigid
            | VisualIgnoreParams | VisualApplyCursor | VisualIgnoreScale | VisualIgnoreOrientation
            | VisualOrientZPole | VisualGameTime | VisualRealTime | VisualDetach | VisualClampScreenSize;
        var unknown = flags & ~known;
        if (unknown != 0)
            values.Add($"unknown:0x{unknown:X8}");
        return string.Join(", ", values);
    }

    private static void AddFlag(List<string> values, uint flags, uint mask, string name)
    {
        if ((flags & mask) != 0)
            values.Add(name);
    }

    private static void AddFlag(List<string> values, ulong flags, ulong mask, string name)
    {
        if ((flags & mask) != 0)
            values.Add(name);
    }
}
