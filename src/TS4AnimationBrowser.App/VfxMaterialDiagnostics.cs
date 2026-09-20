using System.IO;
using System.Text;

namespace TS4AnimationBrowser.App;

internal static class VfxMaterialDiagnostics
{
    public static void Append(StringBuilder builder, VfxModelResourceAsset asset)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Model is null)
            return;

        builder.AppendLine("MATERIAL DIAGNOSTICS");
        try
        {
            var rcol = VfxRcolDecoder.Decode(asset.Data);
            for (var meshIndex = 0; meshIndex < asset.Model.Meshes.Count; meshIndex++)
            {
                var mesh = asset.Model.Meshes[meshIndex];
                builder.AppendLine($"Mesh [{meshIndex}] • material ref 0x{mesh.MaterialReference:X8}");
                if (mesh.MaterialReference == 0)
                {
                    builder.AppendLine("    no material reference");
                    continue;
                }

                VfxResolvedMaterialReference resolved;
                try
                {
                    resolved = VfxModelMaterialResolver.Resolve(rcol, mesh.MaterialReference);
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException)
                {
                    builder.AppendLine($"    material decode failed: {ex.Message}");
                    continue;
                }

                builder.AppendLine($"    target chunk [{resolved.TargetChunkIndex}] {resolved.TargetTag}");
                if (resolved.MaterialSet is { } materialSet)
                {
                    builder.AppendLine($"    MTST version 0x{materialSet.Version:X8} • name hash 0x{materialSet.NameHash:X8}");
                    builder.AppendLine($"    index ref 0x{materialSet.IndexReference:X8} → chunk {FormatChunkIndex(materialSet.IndexChunkIndex)} • entries {materialSet.Entries.Count:N0}");
                    for (var entryIndex = 0; entryIndex < materialSet.Entries.Count; entryIndex++)
                    {
                        var entry = materialSet.Entries[entryIndex];
                        builder.AppendLine($"    entry [{entryIndex}] state {VfxMaterialDecoder.FormatMaterialState(entry.MaterialState)} (0x{entry.MaterialState:X8}) • variant 0x{entry.MaterialVariant:X8} • MATD ref 0x{entry.MaterialReference:X8} → chunk {FormatChunkIndex(entry.MaterialChunkIndex)}");
                        var material = resolved.Materials.FirstOrDefault(candidate => candidate.ChunkIndex == entry.MaterialChunkIndex);
                        if (material is not null)
                            AppendMaterialDefinition(builder, material, 8);
                    }
                }
                else
                {
                    builder.AppendLine("    direct MATD material");
                    foreach (var material in resolved.Materials)
                        AppendMaterialDefinition(builder, material, 4);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            builder.AppendLine($"Material diagnostics failed: {ex.Message}");
        }
    }

    private static void AppendMaterialDefinition(StringBuilder builder, VfxMaterialDefinition material, int indent)
    {
        var pad = new string(' ', indent);
        builder.AppendLine($"{pad}MATD chunk [{material.ChunkIndex}] • version 0x{material.Version:X8} • material name hash 0x{material.MaterialNameHash:X8}");
        builder.AppendLine($"{pad}shader {VfxMaterialDecoder.FormatShader(material.Shader)} (0x{material.Shader:X8}) • MTRL bytes {material.MtrlLength:N0}");
        if (material.Version >= 0x00000103)
            builder.AppendLine($"{pad}video surface {material.IsVideoSurface} • painting surface {material.IsPaintingSurface}");
        builder.AppendLine($"{pad}MTRL unknowns 0x{material.Mtrl.Unknown1:X8} / 0x{material.Mtrl.Unknown2:X4} / 0x{material.Mtrl.Unknown3:X4} • shader data {material.Mtrl.ShaderData.Count:N0}");

        for (var fieldIndex = 0; fieldIndex < material.Mtrl.ShaderData.Count; fieldIndex++)
        {
            var field = material.Mtrl.ShaderData[fieldIndex];
            var fieldName = VfxMaterialDecoder.FormatField(field.Field);
            var dataType = VfxMaterialDecoder.FormatDataType(field.DataType);
            builder.AppendLine($"{pad}    [{fieldIndex}] {fieldName} (0x{field.Field:X8}) • {dataType} (0x{field.DataType:X8}) ×{field.Count} • offset 0x{field.Offset:X8} • {field.Value}");
        }
    }

    private static string FormatChunkIndex(int chunkIndex)
        => chunkIndex < 0 ? "unresolved" : $"[{chunkIndex}]";
}
