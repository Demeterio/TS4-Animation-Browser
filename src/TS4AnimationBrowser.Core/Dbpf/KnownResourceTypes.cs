namespace TS4AnimationBrowser.Core.Dbpf;

public static class KnownResourceTypes
{
    public const uint DdsImage = 0x00B2D882;
    public const uint Clip = 0x6B20C4F3;
    public const uint ClipHeader = 0xBC4A5044;
    public const uint Rig = 0x8EAF13DE;
    public const uint Jazz = 0x02D5DF13;
    public const uint NameMap = 0x0166038C;

    public const uint Model = 0x01661233;
    public const uint ModelLod = 0x01D10F34;
    public const uint Footprint = 0xD382BF57;
    public const uint Light = 0x03B4C61D;

    public const uint VisualEffects = 0x1B192049;
    public const uint VisualEffectsMerged = 0xEA5118B0;

    public static bool IsAnimation(uint type) => type is Clip or ClipHeader or Rig;

    public static bool IsVfx(uint type) => type is VisualEffects or VisualEffectsMerged;

    public static bool IsVfxModelResource(uint type) => type is Model or ModelLod;

    public static bool IsRelevant(uint type) => IsAnimation(type) || IsVfx(type);

    public static bool IsScanCandidate(uint type) => IsRelevant(type) || type == NameMap;

    public static string GetName(uint type) => type switch
    {
        DdsImage => "DDS Image",
        Clip => "CLIP",
        ClipHeader => "CLHD",
        Rig => "RIG",
        Jazz => "JAZZ",
        NameMap => "Name Map",
        Model => "Model",
        ModelLod => "ModelLOD",
        Footprint => "Footprint",
        Light => "Light",
        VisualEffects => "VFX",
        VisualEffectsMerged => "VFX Merged",
        _ => "Unknown"
    };
}
