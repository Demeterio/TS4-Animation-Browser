using System.IO;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record VfxLoadResult(
    string EffectName,
    IReadOnlyList<string> ResourceEffectNames,
    int PayloadSize,
    ushort LibraryVersionMajor,
    byte LibraryVersionMinor,
    ushort VisualEffectVersion,
    uint VisualEffectIndex,
    ulong EffectInstance,
    uint VisualEffectFlags,
    uint VisualEffectSeed,
    IReadOnlyList<VfxSwarmVisualBlock> Blocks,
    ushort ParticleEffectVersion,
    IReadOnlyList<VfxSwarmParticleEffect> ParticleEffects,
    IReadOnlyDictionary<ulong, VfxTextureAsset> Textures,
    IReadOnlyDictionary<ulong, VfxModelResourceAsset> Models,
    IReadOnlyDictionary<ulong, VfxResourceProbe> ResourceProbes);

public static class VfxResourceLoader
{
    public static VfxLoadResult Load(ResourceRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!KnownResourceTypes.IsVfx(row.Entry.Key.Type))
            throw new ArgumentException("The selected resource is not a VFX resource.", nameof(row));
        if (string.IsNullOrWhiteSpace(row.Name))
            throw new InvalidDataException("The selected VFX entry has no effect name.");

        var data = DbpfPackage.ReadResource(row.PackagePath, row.Entry);
        var names = VfxLibraryAnalyzer.ReadEffectNames(data);
        if (names.Count == 0)
            throw new InvalidDataException("No VFX name table could be decoded from this resource.");

        var effectName = names.FirstOrDefault(name =>
            string.Equals(name, row.Name, StringComparison.OrdinalIgnoreCase));
        if (effectName is null)
            throw new InvalidDataException($"Effect '{row.Name}' was not found in the decoded VFX name table.");

        if (row.Entry.Key.Type != KnownResourceTypes.VisualEffects)
        {
            return new VfxLoadResult(
                effectName,
                names,
                data.Length,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<VfxSwarmVisualBlock>(),
                0,
                Array.Empty<VfxSwarmParticleEffect>(),
                new Dictionary<ulong, VfxTextureAsset>(),
                new Dictionary<ulong, VfxModelResourceAsset>(),
                new Dictionary<ulong, VfxResourceProbe>());
        }

        var swarm = VfxSwarmDecoder.DecodeModern(data);
        var particleTable = VfxParticleDecoder.DecodeModern(data);
        var identity = swarm.Effects.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, effectName, StringComparison.OrdinalIgnoreCase));
        if (identity is null)
            throw new InvalidDataException($"Effect '{effectName}' was present in the name table but could not be resolved to a VisualEffect index.");
        if (identity.VisualEffectIndex >= swarm.VisualEffects.Count)
            throw new InvalidDataException($"Effect '{effectName}' references VisualEffect index {identity.VisualEffectIndex:N0}, outside the decoded table.");

        var visualEffect = swarm.VisualEffects[(int)identity.VisualEffectIndex];
        var referencedParticles = new List<VfxSwarmParticleEffect>();
        foreach (var block in visualEffect.Blocks.Where(block => block.BlockType == VfxSwarmBlockType.ParticleEffect))
        {
            if (block.BlockIndex >= particleTable.Effects.Count)
            {
                throw new InvalidDataException(
                    $"VisualEffect #{identity.VisualEffectIndex:N0} references ParticleEffect #{block.BlockIndex:N0}, outside the decoded ParticleEffect table ({particleTable.Effects.Count:N0}).");
            }
            referencedParticles.Add(particleTable.Effects[(int)block.BlockIndex]);
        }

        var references = referencedParticles
            .SelectMany(particle => new[] { particle.TextureInstance, particle.SecondaryTextureInstance })
            .Where(VfxTextureResolver.IsResourceReference)
            .Distinct()
            .ToArray();
        var probes = VfxTextureResolver.ProbeMany(row, references).ToDictionary(pair => pair.Key, pair => pair.Value);

        var modelInstances = referencedParticles
            .Where(VfxSwarmSemantics.IsModelParticle)
            .Select(particle => particle.TextureInstance)
            .Where(VfxTextureResolver.IsResourceReference)
            .Distinct()
            .ToArray();
        var models = new Dictionary<ulong, VfxModelResourceAsset>();
        foreach (var instance in modelInstances)
        {
            if (!probes.TryGetValue(instance, out var probe))
                continue;
            var model = VfxModelResourceResolver.ResolveModelLod(probe);
            if (model is not null)
                models[instance] = model;
        }

        var materialTextureInstances = models.Values
            .SelectMany(VfxModelMaterialResolver.CollectTextureKeys)
            .Where(key => key.Type == KnownResourceTypes.DdsImage)
            .Select(key => key.Instance)
            .Where(VfxTextureResolver.IsResourceReference)
            .Distinct()
            .ToArray();
        if (materialTextureInstances.Length > 0)
        {
            var materialProbes = VfxTextureResolver.ProbeMany(row, materialTextureInstances);
            foreach (var pair in materialProbes)
                probes[pair.Key] = pair.Value;
        }

        var textures = probes.Values
            .Where(probe => probe.Texture is not null)
            .ToDictionary(probe => probe.Instance, probe => probe.Texture!);

        return new VfxLoadResult(
            effectName,
            names,
            data.Length,
            swarm.LibraryVersionMajor,
            swarm.LibraryVersionMinor,
            swarm.VisualEffectVersion,
            identity.VisualEffectIndex,
            identity.EffectInstance,
            visualEffect.Flags,
            visualEffect.Seed,
            visualEffect.Blocks,
            particleTable.Version,
            referencedParticles,
            textures,
            models,
            probes);
    }
}
