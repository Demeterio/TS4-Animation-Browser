using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record VfxTextureAsset(
    ulong Instance,
    uint Type,
    uint Group,
    string PackagePath,
    BitmapSource Image,
    int Width,
    int Height);

public sealed record VfxResourceCandidate(
    ulong Instance,
    uint Type,
    uint Group,
    string PackagePath,
    string? DecodeError)
{
    public bool IsImage => Type == KnownResourceTypes.DdsImage;
}

public sealed record VfxResourceProbe(
    ulong Instance,
    IReadOnlyList<VfxResourceCandidate> Candidates,
    VfxTextureAsset? Texture);

public static class VfxTextureResolver
{
    private static readonly ConcurrentDictionary<string, string[]> PackageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(string Root, ulong Instance), VfxResourceProbe> ProbeCache = new();

    public static bool IsResourceReference(ulong instance)
        => instance != 0 && instance != ulong.MaxValue;

    public static VfxTextureAsset? Resolve(ResourceRow sourceRow, ulong instance)
        => Probe(sourceRow, instance).Texture;

    public static VfxResourceProbe Probe(ResourceRow sourceRow, ulong instance)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        if (!IsResourceReference(instance) || string.IsNullOrWhiteSpace(sourceRow.GameRoot))
            return new VfxResourceProbe(instance, Array.Empty<VfxResourceCandidate>(), null);

        var probes = ProbeMany(sourceRow, new[] { instance });
        return probes.TryGetValue(instance, out var probe)
            ? probe
            : new VfxResourceProbe(instance, Array.Empty<VfxResourceCandidate>(), null);
    }

    public static IReadOnlyDictionary<ulong, VfxResourceProbe> ProbeMany(
        ResourceRow sourceRow,
        IEnumerable<ulong> instances)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        ArgumentNullException.ThrowIfNull(instances);

        var requested = instances
            .Where(IsResourceReference)
            .Distinct()
            .ToArray();
        if (requested.Length == 0 || string.IsNullOrWhiteSpace(sourceRow.GameRoot))
            return new Dictionary<ulong, VfxResourceProbe>();

        var root = Path.GetFullPath(sourceRow.GameRoot);
        var results = new Dictionary<ulong, VfxResourceProbe>();
        var unresolved = new HashSet<ulong>();
        foreach (var instance in requested)
        {
            if (ProbeCache.TryGetValue((root, instance), out var cached))
                results[instance] = cached;
            else
                unresolved.Add(instance);
        }

        if (unresolved.Count == 0)
            return results;

        var candidates = unresolved.ToDictionary(instance => instance, _ => new List<VfxResourceCandidate>());
        var textures = new Dictionary<ulong, VfxTextureAsset>();

        foreach (var packagePath in GetCandidatePackages(sourceRow))
        {
            try
            {
                var package = DbpfPackage.Open(packagePath);
                foreach (var entry in package.Entries)
                {
                    var instance = entry.Key.Instance;
                    if (!unresolved.Contains(instance))
                        continue;

                    string? decodeError = null;
                    if (entry.Key.Type == KnownResourceTypes.DdsImage && !textures.ContainsKey(instance))
                    {
                        try
                        {
                            var data = package.ReadResource(entry);
                            var image = DdsImageDecoder.Decode(data);
                            image.Freeze();
                            textures[instance] = new VfxTextureAsset(
                                instance,
                                entry.Key.Type,
                                entry.Key.Group,
                                packagePath,
                                image,
                                image.PixelWidth,
                                image.PixelHeight);
                        }
                        catch (InvalidDataException ex)
                        {
                            decodeError = ex.Message;
                        }
                        catch (NotSupportedException ex)
                        {
                            decodeError = ex.Message;
                        }
                    }

                    candidates[instance].Add(new VfxResourceCandidate(
                        instance,
                        entry.Key.Type,
                        entry.Key.Group,
                        packagePath,
                        decodeError));
                }
            }
            catch (InvalidDataException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        foreach (var instance in unresolved)
        {
            textures.TryGetValue(instance, out var texture);
            var probe = new VfxResourceProbe(instance, candidates[instance].ToArray(), texture);
            ProbeCache.TryAdd((root, instance), probe);
            results[instance] = probe;
        }

        return results;
    }

    private static IEnumerable<string> GetCandidatePackages(ResourceRow sourceRow)
    {
        yield return sourceRow.PackagePath;

        var root = Path.GetFullPath(sourceRow.GameRoot);
        string[] packages;
        try
        {
            packages = PackageCache.GetOrAdd(root, static gameRoot => Directory
                .EnumerateFiles(gameRoot, "*.package", SearchOption.AllDirectories)
                .OrderByDescending(path => GetPackagePriority(path, gameRoot))
                .ThenBy(path => Path.GetRelativePath(gameRoot, path), StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var packagePath in packages)
        {
            if (!string.Equals(packagePath, sourceRow.PackagePath, StringComparison.OrdinalIgnoreCase))
                yield return packagePath;
        }
    }

    private static int GetPackagePriority(string packagePath, string gameRoot)
    {
        var fileName = Path.GetFileName(packagePath);
        var relative = Path.GetRelativePath(gameRoot, packagePath);
        var priority = 0;

        if (relative.StartsWith($"Delta{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            priority += 500;
        if (fileName.StartsWith("ClientDeltaBuild", StringComparison.OrdinalIgnoreCase))
            priority += 400;
        else if (fileName.StartsWith("ClientFullBuild", StringComparison.OrdinalIgnoreCase))
            priority += 300;
        else if (fileName.Equals("UI.package", StringComparison.OrdinalIgnoreCase))
            priority += 200;

        return priority;
    }
}
