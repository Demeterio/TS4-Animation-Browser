using System.Globalization;
using System.IO;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public enum VfxModelRenderMode
{
    OpaqueCutout,
    AlphaBlend,
    Additive
}

public sealed record VfxResolvedMaterialReference(
    int TargetChunkIndex,
    string TargetTag,
    VfxMaterialSet? MaterialSet,
    IReadOnlyList<VfxMaterialDefinition> Materials);

public sealed record VfxModelRenderMaterial(
    ulong DiffuseTextureInstance,
    ulong AlphaTextureInstance,
    int DiffuseUvChannel,
    float AlphaMaskThreshold,
    uint Shader = 0,
    VfxModelRenderMode RenderMode = VfxModelRenderMode.OpaqueCutout);

public static class VfxModelMaterialResolver
{
    private const uint AlphaMapField = 0xC3FAAC4F;
    private const uint AlphaMaskThresholdField = 0xE77A2B60;
    private const uint DiffuseMapField = 0x6CC0FD85;
    private const uint DiffuseMapUvChannelField = 0xC45A5F41;

    private const uint ShaderAdditive = 0x5AF16731;
    private const uint ShaderPhongAlpha = 0xFC5FC212;
    private const uint ShaderGlassForRabbitHoles = 0x265FFAA1;
    private const uint ShaderGlassForObjects = 0x492ECA7C;
    private const uint ShaderGlassForFences = 0x52986C62;
    private const uint ShaderSimGlass = 0x5EDA9CDE;
    private const uint ShaderBuildingWindow = 0x7B036C01;
    private const uint ShaderGlassForPortals = 0x81DD204D;
    private const uint ShaderGlassForObjectsTranslucent = 0x849CF021;

    public static VfxResolvedMaterialReference Resolve(VfxRcolResource rcol, uint reference)
    {
        ArgumentNullException.ThrowIfNull(rcol);
        if (reference == 0)
            throw new InvalidDataException("The mesh has no material reference.");

        var chunkIndex = VfxRcolDecoder.ResolveChunkIndex(rcol, reference);
        if (chunkIndex < 0)
            throw new InvalidDataException($"Material reference 0x{reference:X8} does not resolve to an internal RCOL chunk.");

        var chunk = rcol.Chunks[chunkIndex];
        if (string.Equals(chunk.Tag, "MATD", StringComparison.Ordinal))
        {
            var material = VfxMaterialDecoder.DecodeMaterialDefinition(rcol, chunkIndex);
            return new VfxResolvedMaterialReference(
                chunkIndex,
                chunk.Tag,
                null,
                new[] { material });
        }

        if (!string.Equals(chunk.Tag, "MTST", StringComparison.Ordinal))
            throw new InvalidDataException($"Material reference 0x{reference:X8} resolved to unsupported chunk '{chunk.Tag}'.");

        var materialSet = VfxMaterialDecoder.DecodeMaterialSet(rcol, reference);
        var materials = new List<VfxMaterialDefinition>();
        foreach (var entry in materialSet.Entries)
        {
            if (entry.MaterialChunkIndex < 0)
                continue;
            var materialChunk = rcol.Chunks[entry.MaterialChunkIndex];
            if (!string.Equals(materialChunk.Tag, "MATD", StringComparison.Ordinal))
                continue;
            materials.Add(VfxMaterialDecoder.DecodeMaterialDefinition(rcol, entry.MaterialChunkIndex));
        }

        return new VfxResolvedMaterialReference(
            chunkIndex,
            chunk.Tag,
            materialSet,
            materials);
    }

    public static VfxModelRenderMaterial ResolveRenderMaterial(VfxModelResourceAsset asset, VfxDecodedModelMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.MaterialReference == 0)
            return new VfxModelRenderMaterial(0, 0, 0, 0);

        var rcol = VfxRcolDecoder.Decode(asset.Data);
        var resolved = Resolve(rcol, mesh.MaterialReference);
        var material = SelectRenderMaterial(rcol, resolved);
        if (material is null)
            return new VfxModelRenderMaterial(0, 0, 0, 0);

        var diffuse = FindTextureInstance(material, DiffuseMapField);
        var alpha = FindTextureInstance(material, AlphaMapField);
        var uvChannel = Math.Clamp((int)Math.Round(FindFloat(material, DiffuseMapUvChannelField, 0)), 0, 1);
        var rawThreshold = FindFloat(material, AlphaMaskThresholdField, 0);
        var alphaThreshold = rawThreshold <= 0
            ? 0
            : rawThreshold <= 1
                ? rawThreshold
                : Math.Clamp(rawThreshold / 255f, 0, 1);

        return new VfxModelRenderMaterial(
            diffuse,
            alpha,
            uvChannel,
            alphaThreshold,
            material.Shader,
            GetRenderMode(material.Shader));
    }

    public static IReadOnlyList<ResourceKey> CollectTextureKeys(VfxModelResourceAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Model is null)
            return Array.Empty<ResourceKey>();

        VfxRcolResource rcol;
        try
        {
            rcol = VfxRcolDecoder.Decode(asset.Data);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            return Array.Empty<ResourceKey>();
        }

        var keys = new HashSet<ResourceKey>();
        foreach (var mesh in asset.Model.Meshes)
        {
            if (mesh.MaterialReference == 0)
                continue;

            VfxResolvedMaterialReference resolved;
            try
            {
                resolved = Resolve(rcol, mesh.MaterialReference);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException)
            {
                continue;
            }

            foreach (var material in resolved.Materials)
            {
                foreach (var field in material.Mtrl.ShaderData)
                {
                    if (field.TextureKey is not { } key)
                        continue;
                    if (key.Type != KnownResourceTypes.DdsImage || !VfxTextureResolver.IsResourceReference(key.Instance))
                        continue;
                    keys.Add(key);
                }
            }
        }

        return keys.ToArray();
    }

    private static VfxMaterialDefinition? SelectRenderMaterial(VfxRcolResource rcol, VfxResolvedMaterialReference resolved)
    {
        if (resolved.MaterialSet is null)
            return resolved.Materials.FirstOrDefault();

        var entries = resolved.MaterialSet.Entries;
        var selected = entries.FirstOrDefault(entry => entry.MaterialVariant == 0 && entry.MaterialChunkIndex >= 0)
            ?? entries.FirstOrDefault(entry => entry.MaterialChunkIndex >= 0);
        if (selected is null)
            return null;

        var chunk = rcol.Chunks[selected.MaterialChunkIndex];
        return string.Equals(chunk.Tag, "MATD", StringComparison.Ordinal)
            ? VfxMaterialDecoder.DecodeMaterialDefinition(rcol, selected.MaterialChunkIndex)
            : null;
    }

    private static VfxModelRenderMode GetRenderMode(uint shader)
        => shader switch
        {
            ShaderAdditive => VfxModelRenderMode.Additive,
            ShaderPhongAlpha or ShaderGlassForRabbitHoles or ShaderGlassForObjects or ShaderGlassForFences
                or ShaderSimGlass or ShaderBuildingWindow or ShaderGlassForPortals or ShaderGlassForObjectsTranslucent
                => VfxModelRenderMode.AlphaBlend,
            _ => VfxModelRenderMode.OpaqueCutout
        };

    private static ulong FindTextureInstance(VfxMaterialDefinition material, uint fieldHash)
        => material.Mtrl.ShaderData
            .FirstOrDefault(field => field.Field == fieldHash && field.TextureKey is not null)
            ?.TextureKey?.Instance ?? 0;

    private static float FindFloat(VfxMaterialDefinition material, uint fieldHash, float fallback)
    {
        var field = material.Mtrl.ShaderData.FirstOrDefault(candidate => candidate.Field == fieldHash);
        if (field is null || field.Count != 1)
            return fallback;
        return float.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
            ? value
            : fallback;
    }
}
