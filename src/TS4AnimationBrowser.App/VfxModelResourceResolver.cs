using System.IO;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record VfxModelResourceAsset(
    ulong Instance,
    uint Type,
    uint Group,
    string PackagePath,
    byte[] Data,
    VfxDecodedModel? Model,
    string? DecodeError)
{
    public ResourceKey? RootModelKey { get; init; }
    public string? RootModelPackagePath { get; init; }
    public VfxDecodedModl? RootModel { get; init; }
    public VfxModlLodEntry? SelectedLod { get; init; }
}

public static class VfxModelResourceResolver
{
    private static readonly VfxModelLodId[] VisibleLodPreference =
    [
        VfxModelLodId.HighDetail,
        VfxModelLodId.MediumDetail,
        VfxModelLodId.LowDetail
    ];

    public static VfxModelResourceAsset? ResolveModelLod(VfxResourceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var modlResult = ResolveThroughModl(probe);
        if (modlResult is not null)
            return modlResult;

        // Some model particles reference an MLOD directly rather than a MODL root.
        // Keep that path as a compatibility fallback, but prefer a successfully decoded
        // candidate instead of returning after the first same-IID MLOD failure.
        VfxModelResourceAsset? firstFailure = null;
        foreach (var candidate in probe.Candidates.Where(candidate => candidate.Type == KnownResourceTypes.ModelLod))
        {
            var asset = ReadAndDecodeModelLod(candidate.PackagePath, new ResourceKey(candidate.Type, candidate.Group, candidate.Instance));
            if (asset is null)
                continue;
            if (asset.Model is not null)
                return asset;
            firstFailure ??= asset;
        }

        return firstFailure;
    }

    private static VfxModelResourceAsset? ResolveThroughModl(VfxResourceProbe probe)
    {
        VfxModelResourceAsset? firstFailure = null;
        foreach (var candidate in probe.Candidates.Where(candidate => candidate.Type == KnownResourceTypes.Model))
        {
            byte[] rootData;
            try
            {
                var package = DbpfPackage.Open(candidate.PackagePath, type => type == KnownResourceTypes.Model);
                var entry = package.Entries.FirstOrDefault(entry =>
                    entry.Key.Type == candidate.Type
                    && entry.Key.Group == candidate.Group
                    && entry.Key.Instance == candidate.Instance);
                if (entry is null)
                    continue;
                rootData = package.ReadResource(entry);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            VfxDecodedModl modl;
            try
            {
                modl = VfxModlDecoder.Decode(rootData);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
            {
                firstFailure ??= new VfxModelResourceAsset(
                    candidate.Instance,
                    candidate.Type,
                    candidate.Group,
                    candidate.PackagePath,
                    rootData,
                    null,
                    $"MODL decode failed: {ex.Message}")
                {
                    RootModelKey = new ResourceKey(candidate.Type, candidate.Group, candidate.Instance),
                    RootModelPackagePath = candidate.PackagePath
                };
                continue;
            }

            foreach (var lodId in VisibleLodPreference)
            {
                foreach (var lod in modl.Entries.Where(entry => entry.Id == lodId))
                {
                    if (lod.ModelLodKey is not { } lodKey)
                    {
                        firstFailure ??= CreateModlFailure(candidate, rootData, modl, lod,
                            $"MODL {lod.Id} reference 0x{lod.ModelLodReference:X8} could not be resolved to a resource key.");
                        continue;
                    }

                    VfxModelResourceAsset? asset;
                    var rootRcol = VfxRcolDecoder.Decode(rootData);
                    if (VfxRcolDecoder.ResolveChunkIndex(rootRcol, lod.ModelLodReference) >= 0)
                    {
                        asset = DecodeModelLod(
                            rootData,
                            lodKey,
                            candidate.PackagePath);
                    }
                    else
                    {
                        asset = ReadReferencedModelLod(probe, candidate.PackagePath, lodKey);
                    }

                    if (asset is null)
                    {
                        firstFailure ??= CreateModlFailure(candidate, rootData, modl, lod,
                            $"MODL {lod.Id} points to {FormatKey(lodKey)}, but that MLOD resource was not found in the probed packages.");
                        continue;
                    }

                    asset = asset with
                    {
                        RootModelKey = new ResourceKey(candidate.Type, candidate.Group, candidate.Instance),
                        RootModelPackagePath = candidate.PackagePath,
                        RootModel = modl,
                        SelectedLod = lod
                    };
                    if (asset.Model is not null)
                        return asset;
                    firstFailure ??= asset;
                }
            }
        }

        return firstFailure;
    }

    private static VfxModelResourceAsset? ReadReferencedModelLod(
        VfxResourceProbe probe,
        string rootPackagePath,
        ResourceKey key)
    {
        if (key.Type != KnownResourceTypes.ModelLod)
            return null;

        var packagePaths = new[] { rootPackagePath }
            .Concat(probe.Candidates.Select(candidate => candidate.PackagePath))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var packagePath in packagePaths)
        {
            var asset = ReadAndDecodeModelLod(packagePath, key);
            if (asset is not null)
                return asset;
        }

        return null;
    }

    private static VfxModelResourceAsset? ReadAndDecodeModelLod(string packagePath, ResourceKey key)
    {
        try
        {
            var package = DbpfPackage.Open(packagePath, type => type == KnownResourceTypes.ModelLod);
            var entry = package.Entries.FirstOrDefault(entry => entry.Key.Equals(key));
            if (entry is null)
                return null;
            return DecodeModelLod(package.ReadResource(entry), key, packagePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static VfxModelResourceAsset DecodeModelLod(byte[] data, ResourceKey key, string packagePath)
    {
        var placeholder = new VfxModelResourceAsset(
            key.Instance,
            key.Type,
            key.Group,
            packagePath,
            data,
            null,
            null);
        try
        {
            return placeholder with { Model = VfxModelMeshDecoder.Decode(placeholder) };
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            return placeholder with { DecodeError = ex.Message };
        }
    }

    private static VfxModelResourceAsset CreateModlFailure(
        VfxResourceCandidate candidate,
        byte[] rootData,
        VfxDecodedModl modl,
        VfxModlLodEntry lod,
        string error)
        => new(
            candidate.Instance,
            candidate.Type,
            candidate.Group,
            candidate.PackagePath,
            rootData,
            null,
            error)
        {
            RootModelKey = new ResourceKey(candidate.Type, candidate.Group, candidate.Instance),
            RootModelPackagePath = candidate.PackagePath,
            RootModel = modl,
            SelectedLod = lod
        };

    private static string FormatKey(ResourceKey key)
        => $"{key.Type:X8}:{key.Group:X8}:{key.Instance:X16}";
}
