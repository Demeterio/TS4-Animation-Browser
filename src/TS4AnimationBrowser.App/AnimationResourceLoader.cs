using System.IO;
using TS4AnimationBrowser.Core.Dbpf;
using TS4AnimationBrowser.Core.ViewerV2;

namespace TS4AnimationBrowser.App;

public enum RigResolutionMode
{
    None,
    ExactReference,
    LegacyFallback
}

public sealed record AnimationLoadProgress(string Stage, int Current, int Total);

public sealed record AnimationLoadResult(
    StudioClipV2 Clip,
    StudioRigV2? Rig,
    ResourceRow? RigRow,
    int MatchingTracks,
    ResourceVisualCategory Category,
    RigResolutionMode RigResolutionMode,
    IReadOnlyList<LimbLengthDiagnostic> LimbDiagnostics);

public static class AnimationResourceLoader
{
    public static AnimationLoadResult LoadClip(
        ResourceRow clipRow,
        IReadOnlyCollection<ResourceRow> resources,
        IProgress<AnimationLoadProgress>? progress = null)
    {
        progress?.Report(new AnimationLoadProgress("Decoding CLIP…", 0, 0));
        var clip = DecodeClip(clipRow);
        var trackKeys = clip.Tracks.Select(track => track.TrackKey).ToHashSet();
        if (trackKeys.Count == 0)
            return EmptyResult(clip, RigResolutionMode.None);

        var rigCandidates = resources
            .Where(row => row.Entry.Key.Type == KnownResourceTypes.Rig)
            .OrderByDescending(row => string.Equals(row.PackagePath, clipRow.PackagePath, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(row => string.Equals(row.Pack, clipRow.Pack, StringComparison.OrdinalIgnoreCase))
            .ThenBy(row => row.Package, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clip.RigInstance != 0)
            return LoadReferencedRig(clip, trackKeys, rigCandidates, progress);

        return LoadFallbackRig(clip, trackKeys, rigCandidates, progress);
    }

    private static AnimationLoadResult LoadReferencedRig(
        StudioClipV2 clip,
        HashSet<uint> trackKeys,
        IReadOnlyList<ResourceRow> rigCandidates,
        IProgress<AnimationLoadProgress>? progress)
    {
        var exactCandidates = rigCandidates
            .Where(row => row.Entry.Key.Instance == clip.RigInstance)
            .ToArray();

        progress?.Report(new AnimationLoadProgress(
            "Loading referenced RIG…",
            exactCandidates.Length > 0 ? 1 : 0,
            Math.Max(1, exactCandidates.Length)));

        foreach (var rigRow in exactCandidates)
        {
            try
            {
                var rig = DecodeRig(rigRow);
                var score = CountMatchingTracks(rig, trackKeys);
                return BuildResult(clip, rig, rigRow, score, RigResolutionMode.ExactReference);
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidDataException)
            {
            }
        }

        return EmptyResult(clip, RigResolutionMode.ExactReference);
    }

    private static AnimationLoadResult LoadFallbackRig(
        StudioClipV2 clip,
        HashSet<uint> trackKeys,
        IReadOnlyList<ResourceRow> rigCandidates,
        IProgress<AnimationLoadProgress>? progress)
    {
        StudioRigV2? bestRig = null;
        ResourceRow? bestRow = null;
        var bestScore = 0;

        for (var index = 0; index < rigCandidates.Count; index++)
        {
            if (index == 0 || index % 20 == 0 || index == rigCandidates.Count - 1)
            {
                progress?.Report(new AnimationLoadProgress(
                    "Finding compatible legacy RIG…",
                    index + 1,
                    rigCandidates.Count));
            }

            var rigRow = rigCandidates[index];
            try
            {
                var rig = DecodeRig(rigRow);
                var score = CountMatchingTracks(rig, trackKeys);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestRig = rig;
                bestRow = rigRow;

                if (score == trackKeys.Count)
                    break;
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidDataException)
            {
            }
        }

        if (bestRig is not null && !IsReliableRigMatch(bestScore, trackKeys.Count))
        {
            bestRig = null;
            bestRow = null;
        }

        return bestRig is null || bestRow is null
            ? EmptyResult(clip, RigResolutionMode.LegacyFallback, bestScore)
            : BuildResult(clip, bestRig, bestRow, bestScore, RigResolutionMode.LegacyFallback);
    }

    private static AnimationLoadResult BuildResult(
        StudioClipV2 clip,
        StudioRigV2 rig,
        ResourceRow rigRow,
        int matchingTracks,
        RigResolutionMode mode)
    {
        var diagnostics = AnimationPoseDiagnostics.Analyze(rig, clip);
        return new AnimationLoadResult(
            clip,
            rig,
            rigRow,
            matchingTracks,
            ClassifyRig(rig),
            mode,
            diagnostics);
    }

    private static AnimationLoadResult EmptyResult(
        StudioClipV2 clip,
        RigResolutionMode mode,
        int matchingTracks = 0)
        => new(
            clip,
            null,
            null,
            matchingTracks,
            ResourceVisualCategory.Unknown,
            mode,
            Array.Empty<LimbLengthDiagnostic>());

    public static int CountMatchingTracks(StudioRigV2 rig, IReadOnlySet<uint> trackKeys)
    {
        var rigHashes = rig.Bones.Select(bone => bone.Hash).ToHashSet();
        return trackKeys.Count(rigHashes.Contains);
    }

    public static int CountMatchingTracks(IReadOnlySet<uint> rigHashes, IReadOnlySet<uint> trackKeys)
        => trackKeys.Count(rigHashes.Contains);

    public static StudioClipV2 DecodeClip(ResourceRow row)
    {
        if (row.Entry.Key.Type != KnownResourceTypes.Clip)
            throw new InvalidDataException("The selected resource is not a CLIP.");
        return StudioClipDecoderV2.Decode(DbpfPackage.ReadResource(row.PackagePath, row.Entry));
    }

    public static StudioRigV2 DecodeRig(ResourceRow row)
    {
        if (row.Entry.Key.Type != KnownResourceTypes.Rig)
            throw new InvalidDataException("The selected resource is not a RIG.");
        return StudioRigDecoderV2.Decode(DbpfPackage.ReadResource(row.PackagePath, row.Entry));
    }

    public static bool IsReliableRigMatch(int matchingTracks, int totalTracks)
    {
        if (totalTracks <= 0 || matchingTracks <= 0)
            return false;
        if (matchingTracks == totalTracks)
            return true;
        if (totalTracks <= 4)
            return false;
        if (totalTracks <= 12)
            return matchingTracks >= 3 && matchingTracks / (double)totalTracks >= 0.60;
        return matchingTracks >= 6 && matchingTracks / (double)totalTracks >= 0.35;
    }

    public static ResourceVisualCategory ClassifyRig(StudioRigV2 rig)
    {
        var names = rig.Bones
            .Select(bone => bone.Name.ToLowerInvariant())
            .Where(StudioRigBoneSemanticsV2.IsSkeletal)
            .ToArray();

        var hasTorso = names.Any(name => ContainsAny(name, "pelvis", "spine", "torso"));
        var hasHead = names.Any(name => ContainsAny(name, "head", "neck", "skull"));
        var hasFrontLimb = names.Any(name => ContainsAny(name,
            "upperarm", "forearm", "lowerarm", "hand",
            "frontleg", "front_leg", "foreleg", "fore_leg", "frontpaw", "front_paw", "paw"));
        var hasRearLimb = names.Any(name => ContainsAny(name,
            "thigh", "calf", "shin", "knee", "foot", "ankle", "hock",
            "hindleg", "hind_leg", "rearleg", "rear_leg", "hindpaw", "hind_paw", "hoof"));
        var hasAnimalBodySignal = names.Any(name => ContainsAny(name,
            "paw", "hoof", "tail", "muzzle", "snout", "ear"));

        var bodyFamilies = 0;
        if (hasTorso) bodyFamilies++;
        if (hasHead) bodyFamilies++;
        if (hasFrontLimb) bodyFamilies++;
        if (hasRearLimb) bodyFamilies++;

        if (bodyFamilies >= 3 || (hasTorso && hasHead && hasAnimalBodySignal))
            return ResourceVisualCategory.SimAnimation;

        return ResourceVisualCategory.ObjectAnimation;
    }

    private static bool ContainsAny(string value, params string[] values)
        => values.Any(value.Contains);
}
