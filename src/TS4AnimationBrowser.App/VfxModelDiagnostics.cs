using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;

namespace TS4AnimationBrowser.App;

internal static class VfxModelDiagnostics
{
    public static void Append(StringBuilder builder, VfxModelResourceAsset asset)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            var rcol = VfxRcolDecoder.Decode(asset.Data);
            var vrtfChunks = rcol.Chunks.Where(chunk => string.Equals(chunk.Tag, "VRTF", StringComparison.Ordinal)).ToArray();
            builder.AppendLine($"VRTF chunks: {vrtfChunks.Length:N0}");
            foreach (var chunk in vrtfChunks)
                AppendVertexFormat(builder, chunk);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            builder.AppendLine($"VRTF diagnostics failed: {ex.Message}");
        }

        if (asset.Model is null)
            return;

        for (var meshIndex = 0; meshIndex < asset.Model.Meshes.Count; meshIndex++)
        {
            var mesh = asset.Model.Meshes[meshIndex];
            builder.AppendLine($"Mesh [{meshIndex}] decoded geometry:");
            builder.AppendLine($"    vertices {mesh.Vertices.Count:N0} • indices {mesh.Indices.Count:N0} • triangles {mesh.Indices.Count / 3:N0}");

            if (mesh.Indices.Count > 0)
                builder.AppendLine($"    index range {mesh.Indices.Min():N0}..{mesh.Indices.Max():N0}");

            if (mesh.Vertices.Count == 0)
                continue;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (var vertex in mesh.Vertices)
            {
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }
            builder.AppendLine($"    position bounds min {Format(min)} • max {Format(max)} • extent {Format(max - min)}");

            var sampleCount = Math.Min(5, mesh.Vertices.Count);
            for (var vertexIndex = 0; vertexIndex < sampleCount; vertexIndex++)
            {
                var vertex = mesh.Vertices[vertexIndex];
                builder.AppendLine($"    vertex [{vertexIndex}] pos {Format(vertex.Position)} • normal {Format(vertex.Normal)} • uv0 ({vertex.Uv.X:0.######}, {vertex.Uv.Y:0.######}) • uv1 ({vertex.Uv1.X:0.######}, {vertex.Uv1.Y:0.######}) • color ({vertex.Color.X:0.######}, {vertex.Color.Y:0.######}, {vertex.Color.Z:0.######}, {vertex.Color.W:0.######})");
            }

            AppendNormalValidation(builder, mesh);
        }

        VfxMaterialDiagnostics.Append(builder, asset);
    }

    private static void AppendNormalValidation(StringBuilder builder, VfxDecodedModelMesh mesh)
    {
        double currentTotal = 0;
        double signedTotal = 0;
        var samples = 0;

        for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
        {
            var i0 = mesh.Indices[index];
            var i1 = mesh.Indices[index + 1];
            var i2 = mesh.Indices[index + 2];
            if ((uint)i0 >= (uint)mesh.Vertices.Count || (uint)i1 >= (uint)mesh.Vertices.Count || (uint)i2 >= (uint)mesh.Vertices.Count)
                continue;

            var v0 = mesh.Vertices[i0];
            var v1 = mesh.Vertices[i1];
            var v2 = mesh.Vertices[i2];
            var face = Vector3.Cross(v1.Position - v0.Position, v2.Position - v0.Position);
            if (!IsFinite(face) || face.LengthSquared() < 0.0000000001f)
                continue;
            face = Vector3.Normalize(face);

            Accumulate(v0.Normal, face, ref currentTotal, ref signedTotal, ref samples);
            Accumulate(v1.Normal, face, ref currentTotal, ref signedTotal, ref samples);
            Accumulate(v2.Normal, face, ref currentTotal, ref signedTotal, ref samples);
        }

        if (samples == 0)
        {
            builder.AppendLine("    normal validation: no non-degenerate triangle samples");
            return;
        }

        var currentAverage = currentTotal / samples;
        var signedAverage = signedTotal / samples;
        var recommendation = signedAverage > currentAverage + 0.05
            ? "signed remap (2*n-1) aligns better"
            : currentAverage > signedAverage + 0.05
                ? "current decoded normals align better"
                : "inconclusive";
        builder.AppendLine($"    normal validation: {samples:N0} vertex/face samples • current |dot| avg {currentAverage:0.###} • signed-remap |dot| avg {signedAverage:0.###} • {recommendation}");
    }

    private static void Accumulate(Vector3 decoded, Vector3 face, ref double currentTotal, ref double signedTotal, ref int samples)
    {
        if (!IsFinite(decoded) || decoded.LengthSquared() < 0.0000001f)
            return;

        var current = Vector3.Normalize(decoded);
        var signed = decoded * 2f - Vector3.One;
        if (!IsFinite(signed) || signed.LengthSquared() < 0.0000001f)
            return;
        signed = Vector3.Normalize(signed);

        currentTotal += Math.Abs(Vector3.Dot(current, face));
        signedTotal += Math.Abs(Vector3.Dot(signed, face));
        samples++;
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void AppendVertexFormat(StringBuilder builder, VfxRcolChunk chunk)
    {
        var data = chunk.Data.Span;
        if (data.Length < 20 || data[0] != (byte)'V' || data[1] != (byte)'R' || data[2] != (byte)'T' || data[3] != (byte)'F')
        {
            builder.AppendLine($"    chunk [{chunk.Index}] invalid VRTF payload");
            return;
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        var stride = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        var count = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
        var extended = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        builder.AppendLine($"    chunk [{chunk.Index}] version 0x{version:X8} • stride {stride} • elements {count} • extended 0x{extended:X8}");

        if (count < 0 || count > 256 || 20 + (count * 4) > data.Length)
        {
            builder.AppendLine("        invalid element table");
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var offset = 20 + (index * 4);
            var usage = (VfxVertexUsage)data[offset];
            var usageIndex = data[offset + 1];
            var format = (VfxVertexFormat)data[offset + 2];
            var byteOffset = data[offset + 3];
            builder.AppendLine($"        [{index}] usage {usage} • usage index {usageIndex} • format {format} (0x{(byte)format:X2}) • offset {byteOffset} • size {GetByteSize(format)}");
        }
    }

    private static string GetByteSize(VfxVertexFormat format)
        => format switch
        {
            VfxVertexFormat.Float1 or VfxVertexFormat.UByte4 or VfxVertexFormat.ColorUByte4
                or VfxVertexFormat.UByte4N or VfxVertexFormat.UShort2N or VfxVertexFormat.Short2 => "4",
            VfxVertexFormat.UShort4N or VfxVertexFormat.Float2 or VfxVertexFormat.Short4
                or VfxVertexFormat.Short4N or VfxVertexFormat.Short4DropShadow => "8",
            VfxVertexFormat.Float3 => "12",
            VfxVertexFormat.Float4 => "16",
            _ => "unknown"
        };

    private static string Format(Vector3 value)
        => $"({value.X:0.######}, {value.Y:0.######}, {value.Z:0.######})";
}
