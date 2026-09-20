using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace TS4AnimationBrowser.App;

public enum VfxModelPrimitiveType : byte
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3,
    TriangleFan = 4,
    TriangleStrip = 5,
    RectList = 6,
    QuadList = 7,
    DisplayList = 8
}

public enum VfxVertexUsage : byte
{
    Position = 0,
    Normal = 1,
    Uv = 2,
    BlendIndex = 3,
    BlendWeight = 4,
    Tangent = 5,
    Colour = 6
}

public enum VfxVertexFormat : byte
{
    Float1 = 0,
    Float2 = 1,
    Float3 = 2,
    Float4 = 3,
    UByte4 = 4,
    ColorUByte4 = 5,
    Short2 = 6,
    Short4 = 7,
    UByte4N = 8,
    Short2N = 9,
    Short4N = 10,
    UShort2N = 11,
    UShort4N = 12,
    Dec3N = 13,
    UDec3N = 14,
    Float16_2 = 15,
    Float16_4 = 16,
    Short4DropShadow = 0xFF
}

public sealed record VfxVertexElement(
    VfxVertexUsage Usage,
    byte UsageIndex,
    VfxVertexFormat Format,
    byte Offset);

public sealed record VfxDecodedModelVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    Vector2 Uv1,
    Vector4 Color);

public sealed record VfxDecodedModelMesh(
    uint Name,
    VfxModelPrimitiveType PrimitiveType,
    uint MeshFlags,
    uint MaterialReference,
    IReadOnlyList<VfxDecodedModelVertex> Vertices,
    IReadOnlyList<int> Indices);

public sealed record VfxDecodedModel(
    uint Version,
    IReadOnlyList<VfxDecodedModelMesh> Meshes);

public static class VfxModelMeshDecoder
{
    private const int MaximumMeshes = 16_384;
    private const int MaximumVertices = 2_000_000;
    private const int MaximumIndices = 6_000_000;
    private const uint VbufDifferencedVertices = 0x2;
    private const uint VbufCollapsed = 0x4;
    private const uint IbufDifferencedIndices = 0x1;
    private const uint IbufUses32BitIndices = 0x2;
    private const uint IbufDisplayList = 0x4;

    public static VfxDecodedModel Decode(VfxModelResourceAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var rcol = VfxRcolDecoder.Decode(asset.Data);
        var mlodChunk = rcol.Chunks.FirstOrDefault(chunk => string.Equals(chunk.Tag, "MLOD", StringComparison.Ordinal));
        if (mlodChunk is null)
            throw new InvalidDataException("The model resource does not contain an MLOD chunk.");

        var reader = new Reader(mlodChunk.Data.Span);
        reader.ExpectTag("MLOD");
        var version = reader.ReadUInt32();
        var meshCount = reader.ReadCount(MaximumMeshes, "MLOD mesh");
        var meshes = new VfxDecodedModelMesh[meshCount];
        for (var index = 0; index < meshCount; index++)
            meshes[index] = ReadMesh(ref reader, rcol, version);
        return new VfxDecodedModel(version, meshes);
    }

    private static VfxDecodedModelMesh ReadMesh(ref Reader reader, VfxRcolResource rcol, uint mlodVersion)
    {
        var expectedSize = checked((int)reader.ReadUInt32());
        var start = reader.Position;
        var name = reader.ReadUInt32();
        var materialReference = reader.ReadUInt32();
        var vertexFormatReference = reader.ReadUInt32();
        var vertexBufferReference = reader.ReadUInt32();
        var indexBufferReference = reader.ReadUInt32();
        var primitiveAndFlags = reader.ReadUInt32();
        var primitiveType = (VfxModelPrimitiveType)(primitiveAndFlags & 0xFF);
        var meshFlags = primitiveAndFlags >> 8;
        var streamOffset = reader.ReadUInt32();
        reader.ReadInt32(); // StartVertex is retained by the format but VBUF addressing uses StreamOffset.
        var startIndex = reader.ReadInt32();
        reader.ReadInt32(); // MinVertexIndex.
        var vertexCount = reader.ReadCount(MaximumVertices, "mesh vertex");
        var primitiveCount = reader.ReadCount(MaximumIndices, "mesh primitive");
        reader.Skip(24); // BoundingBox: min XYZ + max XYZ.
        reader.ReadUInt32(); // SkinControllerIndex.
        SkipUIntList(ref reader, "joint reference");
        reader.ReadUInt32(); // ScaleOffsetIndex.
        SkipGeometryStates(ref reader);
        if (mlodVersion > 0x00000201)
        {
            reader.ReadUInt32(); // ParentName.
            reader.Skip(16); // MirrorPlane Vector4.
        }
        if (mlodVersion > 0x00000203)
            reader.ReadUInt32();

        var consumed = reader.Position - start;
        if (consumed != expectedSize)
            throw new InvalidDataException($"MLOD mesh length mismatch: expected {expectedSize} bytes, decoded {consumed}.");

        if (primitiveType != VfxModelPrimitiveType.TriangleList)
            throw new NotSupportedException($"MLOD primitive type {primitiveType} is not rendered yet.");

        var vrtf = ResolveChunk(rcol, vertexFormatReference, "VRTF");
        var vbuf = ResolveChunk(rcol, vertexBufferReference, "VBUF");
        var ibuf = ResolveChunk(rcol, indexBufferReference, "IBUF");
        var format = ReadVertexFormat(vrtf.Data.Span);
        var vertexData = ReadVertexBuffer(vbuf.Data.Span);
        var indexData = ReadIndexBuffer(ibuf.Data.Span);
        var vertices = DecodeVertices(vertexData, format, streamOffset, vertexCount);
        var indexCount = checked(primitiveCount * 3);
        if (startIndex < 0 || indexCount < 0 || startIndex > indexData.Length - indexCount)
            throw new InvalidDataException("MLOD mesh index range extends outside the IBUF.");
        var indices = new int[indexCount];
        Array.Copy(indexData, startIndex, indices, 0, indexCount);

        return new VfxDecodedModelMesh(name, primitiveType, meshFlags, materialReference, vertices, indices);
    }

    private static VfxRcolChunk ResolveChunk(VfxRcolResource rcol, uint reference, string tag)
    {
        var index = VfxRcolDecoder.ResolveChunkIndex(rcol, reference);
        if (index < 0)
            throw new InvalidDataException($"MLOD {tag} reference 0x{reference:X8} does not resolve to an internal chunk.");
        var chunk = rcol.Chunks[index];
        if (!string.Equals(chunk.Tag, tag, StringComparison.Ordinal))
            throw new InvalidDataException($"MLOD reference 0x{reference:X8} resolved to '{chunk.Tag}' instead of '{tag}'.");
        return chunk;
    }

    private static VertexFormatInfo ReadVertexFormat(ReadOnlySpan<byte> data)
    {
        var reader = new Reader(data);
        reader.ExpectTag("VRTF");
        reader.ReadUInt32(); // Version.
        var stride = reader.ReadInt32();
        if (stride <= 0 || stride > 4096)
            throw new InvalidDataException($"Invalid VRTF stride {stride}.");
        var count = reader.ReadCount(256, "VRTF element");
        reader.ReadUInt32(); // ExtendedFormat.
        var elements = new VfxVertexElement[count];
        for (var index = 0; index < count; index++)
        {
            elements[index] = new VfxVertexElement(
                (VfxVertexUsage)reader.ReadByte(),
                reader.ReadByte(),
                (VfxVertexFormat)reader.ReadByte(),
                reader.ReadByte());
        }
        return new VertexFormatInfo(stride, elements);
    }

    private static byte[] ReadVertexBuffer(ReadOnlySpan<byte> data)
    {
        var reader = new Reader(data);
        reader.ExpectTag("VBUF");
        reader.ReadUInt32(); // Version.
        var flags = reader.ReadUInt32();
        reader.ReadUInt32(); // SwizzleInfo ChunkReference.
        if ((flags & VbufDifferencedVertices) != 0)
            throw new NotSupportedException("Differenced VBUF vertices are not decoded yet.");
        if ((flags & VbufCollapsed) != 0)
            throw new NotSupportedException("Collapsed VBUF vertices are not decoded yet.");
        return reader.ReadRemaining().ToArray();
    }

    private static int[] ReadIndexBuffer(ReadOnlySpan<byte> data)
    {
        var reader = new Reader(data);
        reader.ExpectTag("IBUF");
        reader.ReadUInt32(); // Version.
        var flags = reader.ReadUInt32();
        reader.ReadUInt32(); // DisplayListUsage.
        if ((flags & IbufDisplayList) != 0)
            throw new NotSupportedException("IBUF display lists are not decoded yet.");

        var is32Bit = (flags & IbufUses32BitIndices) != 0;
        var stride = is32Bit ? 4 : 2;
        if (reader.Remaining % stride != 0)
            throw new InvalidDataException("IBUF byte length is not aligned to its index size.");
        var count = reader.Remaining / stride;
        if (count > MaximumIndices)
            throw new InvalidDataException($"IBUF contains too many indices ({count:N0}).");

        var result = new int[count];
        var last = 0;
        var differenced = (flags & IbufDifferencedIndices) != 0;
        for (var index = 0; index < count; index++)
        {
            int current;
            if (is32Bit)
                current = reader.ReadInt32();
            else if (differenced)
                current = reader.ReadInt16();
            else
                current = reader.ReadUInt16();
            if (differenced)
                current += last;
            result[index] = current;
            last = current;
        }
        return result;
    }

    private static VfxDecodedModelVertex[] DecodeVertices(
        byte[] buffer,
        VertexFormatInfo format,
        uint streamOffset,
        int vertexCount)
    {
        var start = checked((int)streamOffset);
        var byteCount = checked(vertexCount * format.Stride);
        if (start < 0 || start > buffer.Length - byteCount)
            throw new InvalidDataException("MLOD vertex range extends outside the VBUF.");

        var position = format.Elements.FirstOrDefault(element => element.Usage == VfxVertexUsage.Position);
        if (position is null)
            throw new InvalidDataException("VRTF has no position element.");
        var normal = format.Elements.FirstOrDefault(element => element.Usage == VfxVertexUsage.Normal);
        var uv0 = format.Elements.FirstOrDefault(element => element.Usage == VfxVertexUsage.Uv && element.UsageIndex == 0);
        var uv1 = format.Elements.FirstOrDefault(element => element.Usage == VfxVertexUsage.Uv && element.UsageIndex == 1);
        var color = format.Elements.FirstOrDefault(element => element.Usage == VfxVertexUsage.Colour);

        var vertices = new VfxDecodedModelVertex[vertexCount];
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var vertex = buffer.AsSpan(start + (vertexIndex * format.Stride), format.Stride);
            var p = ReadElement(vertex, position);
            var n = normal is null ? new Vector4(0, 1, 0, 0) : ReadElement(vertex, normal);
            var t0 = uv0 is null ? Vector4.Zero : ReadElement(vertex, uv0);
            var t1 = uv1 is null ? Vector4.Zero : ReadElement(vertex, uv1);
            var c = color is null ? Vector4.One : ReadElement(vertex, color);
            var normalValue = new Vector3(n.X, n.Y, n.Z);
            if (normalValue.LengthSquared() < 0.000001f)
                normalValue = Vector3.UnitY;
            else
                normalValue = Vector3.Normalize(normalValue);
            vertices[vertexIndex] = new VfxDecodedModelVertex(
                new Vector3(p.X, p.Y, p.Z),
                normalValue,
                new Vector2(t0.X, t0.Y),
                new Vector2(t1.X, t1.Y),
                c);
        }
        return vertices;
    }

    private static Vector4 ReadElement(ReadOnlySpan<byte> vertex, VfxVertexElement element)
    {
        var size = ByteSize(element.Format);
        if (element.Offset > vertex.Length - size)
            throw new InvalidDataException($"VRTF element {element.Usage} extends outside the vertex stride.");
        var data = vertex.Slice(element.Offset, size);
        return element.Format switch
        {
            VfxVertexFormat.Float1 => new Vector4(ReadSingle(data, 0), 0, 0, 1),
            VfxVertexFormat.Float2 => new Vector4(ReadSingle(data, 0), ReadSingle(data, 4), 0, 1),
            VfxVertexFormat.Float3 => new Vector4(ReadSingle(data, 0), ReadSingle(data, 4), ReadSingle(data, 8), 1),
            VfxVertexFormat.Float4 => new Vector4(ReadSingle(data, 0), ReadSingle(data, 4), ReadSingle(data, 8), ReadSingle(data, 12)),
            VfxVertexFormat.UByte4N => new Vector4(data[0] / 255f, data[1] / 255f, data[2] / 255f, data[3] / 255f),
            VfxVertexFormat.ColorUByte4 when element.Usage is VfxVertexUsage.Normal or VfxVertexUsage.Tangent
                => DecodePackedNormal(data),
            VfxVertexFormat.ColorUByte4 => new Vector4(data[0] / 255f, data[1] / 255f, data[2] / 255f, data[3] / 255f),
            VfxVertexFormat.Short2 => new Vector4(ReadInt16(data, 0) / 32767f, ReadInt16(data, 2) / 32767f, 0, 1),
            VfxVertexFormat.Short4 => DecodeShort4(data),
            VfxVertexFormat.UShort4N => DecodeUShort4N(data),
            VfxVertexFormat.Short4DropShadow => DecodeDropShadow(data),
            _ => throw new NotSupportedException($"VRTF element format {element.Format} is not decoded yet.")
        };
    }

    private static Vector4 DecodePackedNormal(ReadOnlySpan<byte> data)
    {
        static float Decode(byte value) => value == 0 ? -1f : ((value + 1) / 128f) - 1f;
        var w = data[3] switch { 0 => -1f, 127 => 0f, 255 => 1f, _ => 0f };
        return new Vector4(Decode(data[2]), Decode(data[1]), Decode(data[0]), w);
    }

    private static Vector4 DecodeShort4(ReadOnlySpan<byte> data)
    {
        var scalar = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        var divisor = scalar == 0 ? (float)short.MaxValue : scalar;
        return new Vector4(
            ReadInt16(data, 0) / divisor,
            ReadInt16(data, 2) / divisor,
            ReadInt16(data, 4) / divisor,
            ReadInt16(data, 6) / divisor);
    }

    private static Vector4 DecodeUShort4N(ReadOnlySpan<byte> data)
    {
        var scalar = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        var divisor = scalar == 0 ? 511f : scalar;
        return new Vector4(
            ReadInt16(data, 0) / divisor,
            ReadInt16(data, 2) / divisor,
            ReadInt16(data, 4) / divisor,
            ReadInt16(data, 6) / divisor);
    }

    private static Vector4 DecodeDropShadow(ReadOnlySpan<byte> data)
        => new(
            ReadInt16(data, 0) / 32767f,
            ReadInt16(data, 2) / 32767f,
            ReadInt16(data, 4) / 32767f,
            ReadInt16(data, 6) / 511f);

    private static int ByteSize(VfxVertexFormat format)
        => format switch
        {
            VfxVertexFormat.Float1 or VfxVertexFormat.UByte4 or VfxVertexFormat.ColorUByte4
                or VfxVertexFormat.UByte4N or VfxVertexFormat.UShort2N or VfxVertexFormat.Short2 => 4,
            VfxVertexFormat.UShort4N or VfxVertexFormat.Float2 or VfxVertexFormat.Short4
                or VfxVertexFormat.Short4N or VfxVertexFormat.Short4DropShadow => 8,
            VfxVertexFormat.Float3 => 12,
            VfxVertexFormat.Float4 => 16,
            _ => throw new NotSupportedException($"VRTF element format {format} has no supported byte size yet.")
        };

    private static float ReadSingle(ReadOnlySpan<byte> data, int offset)
        => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)));

    private static short ReadInt16(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, 2));

    private static void SkipUIntList(ref Reader reader, string label)
    {
        var count = reader.ReadCount(65_536, label);
        reader.Skip(checked(count * 4));
    }

    private static void SkipGeometryStates(ref Reader reader)
    {
        var count = reader.ReadCount(65_536, "geometry state");
        reader.Skip(checked(count * 20));
    }

    private sealed record VertexFormatInfo(int Stride, IReadOnlyList<VfxVertexElement> Elements);

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _data;

        public Reader(ReadOnlySpan<byte> data)
        {
            _data = data;
            Position = 0;
        }

        public int Position { get; private set; }
        public int Remaining => _data.Length - Position;

        public byte ReadByte()
        {
            Ensure(1);
            return _data[Position++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(Position, 2));
            Position += 2;
            return value;
        }

        public short ReadInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(Position, 2));
            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(Position, 4));
            Position += 4;
            return value;
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public int ReadCount(int maximum, string label)
        {
            var value = ReadInt32();
            if (value < 0 || value > maximum)
                throw new InvalidDataException($"Invalid {label} count {value}.");
            return value;
        }

        public void ExpectTag(string tag)
        {
            if (tag.Length != 4)
                throw new ArgumentException("A chunk tag must contain four characters.", nameof(tag));
            Ensure(4);
            for (var index = 0; index < 4; index++)
            {
                if (_data[Position + index] != (byte)tag[index])
                    throw new InvalidDataException($"Expected {tag} chunk tag.");
            }
            Position += 4;
        }

        public void Skip(int count)
        {
            Ensure(count);
            Position += count;
        }

        public ReadOnlySpan<byte> ReadRemaining()
        {
            var result = _data[Position..];
            Position = _data.Length;
            return result;
        }

        private void Ensure(int count)
        {
            if (count < 0 || Position < 0 || Position > _data.Length - count)
                throw new InvalidDataException("Unexpected end of mesh chunk.");
        }
    }
}
