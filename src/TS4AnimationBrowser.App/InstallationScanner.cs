using System.IO;
using TS4AnimationBrowser.Core.Animation;
using TS4AnimationBrowser.Core.Dbpf;
using TS4AnimationBrowser.Core.ViewerV2;

namespace TS4AnimationBrowser.App;

public sealed record ScanProgress(
    string Stage,
    int Current,
    int Total,
    string Detail,
    int ResourceCount,
    int ErrorCount)
{
    public int CurrentPackage => Current;
    public int TotalPackages => Total;
    public string PackageName => Stage switch
    {
        "Scanning packages" => Detail,
        "Resolving patched resources" => $"patched resources — {Detail}",
        "Indexing VFX" => $"VFX {Current:N0}/{Total:N0} — {Detail}",
        "Classifying RIGs" => $"RIG {Current:N0}/{Total:N0} — {Detail}",
        "Classifying animations" => $"animation {Current:N0}/{Total:N0} — {Detail}",
        "Sorting library" => $"library — {Detail}",
        _ => $"{Stage} — {Detail}"
    };
}

public sealed record ScanResult(
    IReadOnlyList<ResourceRow> Resources,
    int PackageCount,
    int ErrorCount,
    int ShadowedResourceCount);

public static class InstallationScanner
{
    private sealed record RigScanProfile(
        ResourceRow Row,
        StudioRigV2 Rig,
        ResourceVisualCategory Category,
        HashSet<uint> BoneHashes);

    public static ScanResult Scan(string gameRoot, IProgress<ScanProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        if (!Directory.Exists(gameRoot))
            throw new DirectoryNotFoundException($"The Sims 4 folder was not found: {gameRoot}");

        var packages = FindClientPackages(gameRoot);
        if (packages.Count == 0)
            throw new InvalidOperationException(
                "No ClientFullBuild or ClientDeltaBuild package was found in the selected folder. Select the main The Sims 4 installation folder.");

        var discoveredResources = new List<ResourceRow>(16_384);
        var errors = 0;

        for (var index = 0; index < packages.Count; index++)
        {
            var packagePath = packages[index];
            try
            {
                var package = DbpfPackage.Open(packagePath, KnownResourceTypes.IsScanCandidate);
                var names = ReadPackageNameMap(package);

                foreach (var entry in package.Entries)
                {
                    if (!KnownResourceTypes.IsRelevant(entry.Key.Type))
                        continue;

                    names.TryGetValue(entry.Key.Instance, out var name);
                    var isRelevant = true;

                    if (entry.Key.Type == KnownResourceTypes.Clip)
                    {
                        try
                        {
                            var info = ClipLibraryAnalyzer.Analyze(package.ReadResource(entry));
                            if (string.IsNullOrWhiteSpace(name))
                                name = info.Name;
                            isRelevant = info.IsRelevant && !string.IsNullOrWhiteSpace(name);
                        }
                        catch
                        {
                            isRelevant = !string.IsNullOrWhiteSpace(name);
                        }
                    }

                    discoveredResources.Add(new ResourceRow(entry, packagePath, gameRoot, name, isRelevant));
                }
            }
            catch (Exception)
            {
                errors++;
            }

            progress?.Report(new ScanProgress(
                "Scanning packages",
                index + 1,
                packages.Count,
                Path.GetFileName(packagePath),
                discoveredResources.Count,
                errors));
        }

        progress?.Report(new ScanProgress(
            "Resolving patched resources",
            0,
            1,
            $"Comparing {discoveredResources.Count:N0} discovered resources…",
            discoveredResources.Count,
            errors));

        var effectiveResources = ResolveEffectiveResources(discoveredResources, gameRoot).ToArray();
        var shadowedResourceCount = discoveredResources.Count - effectiveResources.Length;

        progress?.Report(new ScanProgress(
            "Resolving patched resources",
            1,
            1,
            $"{effectiveResources.Length:N0} effective resources • {shadowedResourceCount:N0} older duplicate(s) hidden",
            effectiveResources.Length,
            errors));

        var libraryResources = ExpandVfxResources(effectiveResources, gameRoot, progress, errors);
        libraryResources = VfxLibraryNormalizer.PreferSplitVisualEffects(libraryResources);
        ClassifyResources(libraryResources, progress, errors);

        progress?.Report(new ScanProgress(
            "Sorting library",
            0,
            1,
            $"Sorting {libraryResources.Length:N0} library entries by name…",
            libraryResources.Length,
            errors));

        var sortedResources = libraryResources
            .OrderBy(row => string.IsNullOrWhiteSpace(row.Name))
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Entry.Key.Type)
            .ThenBy(row => row.Entry.Key.Instance)
            .ToArray();

        progress?.Report(new ScanProgress(
            "Sorting library",
            1,
            1,
            $"Library ready • {sortedResources.Length:N0} entries",
            sortedResources.Length,
            errors));

        return new ScanResult(sortedResources, packages.Count, errors, shadowedResourceCount);
    }

    private static ResourceRow[] ExpandVfxResources(
        IReadOnlyList<ResourceRow> resources,
        string gameRoot,
        IProgress<ScanProgress>? progress,
        int errors)
    {
        var vfxRows = resources.Where(row => KnownResourceTypes.IsVfx(row.Entry.Key.Type)).ToArray();
        if (vfxRows.Length == 0)
            return resources.ToArray();

        var expanded = new List<ResourceRow>(resources.Count + vfxRows.Length);
        expanded.AddRange(resources.Where(row => !KnownResourceTypes.IsVfx(row.Entry.Key.Type)));

        for (var index = 0; index < vfxRows.Length; index++)
        {
            var row = vfxRows[index];
            IReadOnlyList<string> effectNames = Array.Empty<string>();
            try
            {
                effectNames = VfxLibraryAnalyzer.ReadEffectNames(
                    DbpfPackage.ReadResource(row.PackagePath, row.Entry));
            }
            catch (InvalidDataException)
            {
                // Leave this resource as one unnamed VFX entry if its payload cannot be read.
            }

            if (effectNames.Count == 0)
            {
                expanded.Add(row);
            }
            else
            {
                foreach (var effectName in effectNames)
                    expanded.Add(new ResourceRow(row.Entry, row.PackagePath, gameRoot, effectName, isRelevant: true));
            }

            progress?.Report(new ScanProgress(
                "Indexing VFX",
                index + 1,
                Math.Max(1, vfxRows.Length),
                effectNames.Count == 0
                    ? row.Instance
                    : $"{effectNames.Count:N0} effect name(s)",
                expanded.Count,
                errors));
        }

        return expanded.ToArray();
    }

    private static void ClassifyResources(
        IReadOnlyList<ResourceRow> resources,
        IProgress<ScanProgress>? progress,
        int errors)
    {
        var profiles = new List<RigScanProfile>();
        var rigsByInstance = new Dictionary<ulong, RigScanProfile>();
        var rigs = resources.Where(row => row.Entry.Key.Type == KnownResourceTypes.Rig).ToArray();

        for (var index = 0; index < rigs.Length; index++)
        {
            var rigRow = rigs[index];
            try
            {
                var rig = StudioRigDecoderV2.Decode(DbpfPackage.ReadResource(rigRow.PackagePath, rigRow.Entry));
                var category = AnimationResourceLoader.ClassifyRig(rig);
                var profile = new RigScanProfile(
                    rigRow,
                    rig,
                    category,
                    rig.Bones.Select(bone => bone.Hash).ToHashSet());

                rigRow.SetCategory(category);
                profiles.Add(profile);
                rigsByInstance[rigRow.Entry.Key.Instance] = profile;
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidDataException)
            {
            }

            progress?.Report(new ScanProgress(
                "Classifying RIGs",
                index + 1,
                Math.Max(1, rigs.Length),
                string.IsNullOrWhiteSpace(rigRow.Name) ? rigRow.Instance : rigRow.Name,
                resources.Count,
                errors));
        }

        var clips = resources.Where(row => row.Entry.Key.Type == KnownResourceTypes.Clip).ToArray();
        for (var index = 0; index < clips.Length; index++)
        {
            var clipRow = clips[index];
            try
            {
                var data = DbpfPackage.ReadResource(clipRow.PackagePath, clipRow.Entry);
                var metadata = StudioClipDecoderV2.ReadMetadata(data);
                StudioClipV2? decodedClip = null;

                if (string.IsNullOrWhiteSpace(clipRow.Name) || metadata.RigInstance == 0)
                {
                    decodedClip = StudioClipDecoderV2.Decode(data);
                    if (string.IsNullOrWhiteSpace(clipRow.Name))
                    {
                        var decodedName = FirstNonEmpty(decodedClip.Name, decodedClip.CodecAnimationName);
                        if (!string.IsNullOrWhiteSpace(decodedName))
                            clipRow.SetName(decodedName);
                    }
                }

                if (metadata.RigInstance != 0)
                {
                    if (!string.IsNullOrWhiteSpace(clipRow.Name)
                        && rigsByInstance.TryGetValue(metadata.RigInstance, out var exactProfile))
                    {
                        clipRow.SetCategory(exactProfile.Category);
                    }
                }
                else if (decodedClip is not null && !string.IsNullOrWhiteSpace(clipRow.Name))
                {
                    var trackKeys = decodedClip.Tracks.Select(track => track.TrackKey).ToHashSet();
                    if (trackKeys.Count > 0)
                    {
                        var candidates = profiles
                            .OrderByDescending(profile => string.Equals(
                                profile.Row.PackagePath,
                                clipRow.PackagePath,
                                StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(profile => string.Equals(
                                profile.Row.Pack,
                                clipRow.Pack,
                                StringComparison.OrdinalIgnoreCase))
                            .ThenBy(profile => profile.Row.Package, StringComparer.OrdinalIgnoreCase);

                        RigScanProfile? best = null;
                        var bestScore = 0;
                        foreach (var candidate in candidates)
                        {
                            var score = AnimationResourceLoader.CountMatchingTracks(candidate.BoneHashes, trackKeys);
                            if (score <= bestScore)
                                continue;

                            bestScore = score;
                            best = candidate;
                            if (score == trackKeys.Count)
                                break;
                        }

                        if (best is not null
                            && AnimationResourceLoader.IsReliableRigMatch(bestScore, trackKeys.Count))
                        {
                            clipRow.SetCategory(best.Category);
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidDataException)
            {
            }

            progress?.Report(new ScanProgress(
                "Classifying animations",
                index + 1,
                Math.Max(1, clips.Length),
                string.IsNullOrWhiteSpace(clipRow.Name) ? clipRow.Instance : clipRow.Name,
                resources.Count,
                errors));
        }
    }

    private static IReadOnlyList<ResourceRow> ResolveEffectiveResources(
        IReadOnlyList<ResourceRow> discoveredResources,
        string gameRoot)
    {
        return discoveredResources
            .GroupBy(row => row.Entry.Key)
            .Select(group => group
                .OrderByDescending(row => GetPackageLayer(row.PackagePath))
                .ThenByDescending(row => GetDeltaSpecificity(row.PackagePath, gameRoot))
                .ThenBy(row => Path.GetRelativePath(gameRoot, row.PackagePath), StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static int GetPackageLayer(string packagePath)
    {
        var fileName = Path.GetFileName(packagePath);
        if (fileName.StartsWith("ClientDeltaBuild", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (fileName.StartsWith("ClientFullBuild", StringComparison.OrdinalIgnoreCase))
            return 1;
        return 0;
    }

    private static int GetDeltaSpecificity(string packagePath, string gameRoot)
    {
        if (GetPackageLayer(packagePath) != 2)
            return 0;

        var parts = Path.GetRelativePath(gameRoot, packagePath)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (parts.Length >= 2
            && parts[0].Equals("Delta", StringComparison.OrdinalIgnoreCase)
            && IsPackFolder(parts[1]))
        {
            return 1;
        }

        return 0;
    }

    private static bool IsPackFolder(string value)
    {
        if (value.Length < 3)
            return false;

        return (value.StartsWith("EP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("GP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("SP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("FP", StringComparison.OrdinalIgnoreCase))
            && value[2..].All(char.IsDigit);
    }

    private static Dictionary<ulong, string> ReadPackageNameMap(DbpfPackage package)
    {
        var names = new Dictionary<ulong, string>();
        foreach (var entry in package.Entries.Where(entry => entry.Key.Type == KnownResourceTypes.NameMap))
        {
            try
            {
                foreach (var pair in ResourceNameReader.ReadNameMap(package, entry))
                    names[pair.Key] = pair.Value;
            }
            catch
            {
            }
        }

        return names;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static List<string> FindClientPackages(string gameRoot)
    {
        return Directory
            .EnumerateFiles(gameRoot, "Client*Build*.package", SearchOption.AllDirectories)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName.StartsWith("ClientFullBuild", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("ClientDeltaBuild", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
