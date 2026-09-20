namespace TS4AnimationBrowser.Core.ViewerV2;

/// <summary>
/// Bone naming semantics mirrored from Sims 4 Studio's animation/rig.py.
/// Keep this small and centralized: the pose evaluator and the preview renderer should agree
/// about which RIG nodes are structural and which are IK, slots, sliders or misc helpers.
/// </summary>
public static class StudioRigBoneSemanticsV2
{
    private static readonly string[] ContainerSlots = ["cntm", "container", "carry"];
    private static readonly string[] TargetSlots = ["target", "surface", "grip"];
    private static readonly string[] RouteSlots = ["route"];
    private static readonly string[] EffectSlots = ["fx"];
    private static readonly string[] Slots = ["slot", .. RouteSlots, .. TargetSlots, .. ContainerSlots, .. EffectSlots];

    private static readonly string[] IkBones =
    [
        "world", "world_offset", "info", "export", "rootoffset", "slotoffset", "footoffset"
    ];

    private static readonly string[] SliderBones =
    [
        "compress", "twist", "back", "chest", "shoulder", "wrist", "ulna", "radius", "hip",
        "bicep", "quadricep", "backcalf", "skirt", "sleeve"
    ];

    private static readonly string[] MiscBones = ["breast", "belly", "stomach", "rump"];

    public static bool IsRoot(string name)
    {
        var value = Normalize(name);
        return value.Contains("root") && !value.Contains("root_bind");
    }

    public static bool IsExportRoot(string name)
        => Normalize(name).Contains("export_root");

    public static bool IsRoute(string name)
        => ContainsAny(Normalize(name), RouteSlots);

    public static bool IsContainer(string name)
        => ContainsAny(Normalize(name), ContainerSlots);

    public static bool IsSlot(string name)
        => ContainsAny(Normalize(name), Slots);

    public static bool IsIk(string name)
        => ContainsAny(Normalize(name), IkBones);

    public static bool IsProp(string name)
        => Normalize(name).Contains("_prop_");

    public static bool IsSlider(string name)
        => ContainsAny(Normalize(name), SliderBones);

    public static bool IsTarget(string name)
        => ContainsAny(Normalize(name), TargetSlots);

    public static bool IsMisc(string name)
    {
        var value = Normalize(name);
        return ContainsAny(value, MiscBones) || value.Contains("_prop_");
    }

    public static bool IsSkeletal(string name)
        => !(IsRoot(name) || IsIk(name) || IsSlider(name) || IsSlot(name) || IsMisc(name));

    public static bool IsAnimated(string name)
        => !(IsIk(name) || IsSlider(name) || IsSlot(name) || IsMisc(name));

    private static string Normalize(string name)
        => name?.ToLowerInvariant() ?? string.Empty;

    private static bool ContainsAny(string value, IEnumerable<string> values)
        => values.Any(value.Contains);
}
