using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public static class VfxLibraryNormalizer
{
    public static ResourceRow[] PreferSplitVisualEffects(IEnumerable<ResourceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var snapshot = rows as ResourceRow[] ?? rows.ToArray();
        var splitEffectNames = snapshot
            .Where(row => row.Entry.Key.Type == KnownResourceTypes.VisualEffects
                && !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => row.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (splitEffectNames.Count == 0)
            return snapshot;

        // EA's current VFX layout uses 0x1B192049 split resources plus the global instance map.
        // The old 0xEA5118B0 merged library may still contain copies of the same named effects.
        // Keep merged-only names for compatibility, but do not show a legacy duplicate when the
        // same effect name is already present in the current split resource set.
        return snapshot
            .Where(row => row.Entry.Key.Type != KnownResourceTypes.VisualEffectsMerged
                || string.IsNullOrWhiteSpace(row.Name)
                || !splitEffectNames.Contains(row.Name))
            .ToArray();
    }
}
