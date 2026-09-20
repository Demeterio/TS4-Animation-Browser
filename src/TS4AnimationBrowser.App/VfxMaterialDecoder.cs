using System.Buffers.Binary;
using System.IO;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record VfxMaterialSet(
    int ChunkIndex,
    uint Version,
    uint NameHash,
    uint IndexReference,
    int IndexChunkIndex,
    IReadOnlyList<VfxMaterialSetEntry> Entries);

public sealed record VfxMaterialSetEntry(
    uint MaterialReference,
    int MaterialChunkIndex,
    uint MaterialState,
    uint MaterialVariant);

public sealed record VfxMaterialDefinition(
    int ChunkIndex,
    uint Version,
    uint MaterialNameHash,
    uint Shader,
    uint MtrlLength,
    bool IsVideoSurface,
    bool IsPaintingSurface,
    VfxMtrlData Mtrl);

public sealed record VfxMtrlData(
    uint Unknown1,
    ushort Unknown2,
    ushort Unknown3,
    IReadOnlyList<VfxShaderDataEntry> ShaderData);

public sealed record VfxShaderDataEntry(
    uint Field,
    uint DataType,
    int Count,
    uint Offset,
    string Value,
    ResourceKey? TextureKey);

public static class VfxMaterialDecoder
{
    private const int MaximumEntries = 4_096;
    private const uint DataTypeFloat = 1;
    private const uint DataTypeInt = 2;
    private const uint DataTypeTexture = 4;
    private const uint DataTypeImageMap = 0x00010004;

    public static VfxMaterialSet DecodeMaterialSet(VfxRcolResource rcol, uint reference)
    {
        ArgumentNullException.ThrowIfNull(rcol);
        var chunkIndex = VfxRcolDecoder.ResolveChunkIndex(rcol, reference);
        if (chunkIndex < 0)
            throw new InvalidDataException($"Material reference 0x{reference:X8} does not resolve to an internal MTST chunk.");

        var chunk = rcol.Chunks[chunkIndex];
        if (!string.Equals(chunk.Tag, "MTST", StringComparison.Ordinal))
            throw new InvalidDataException($"Material reference 0x{reference:X8} resolved to '{chunk.Tag}' instead of 'MTST'.");

        var reader = new Reader(chunk.Data.Span);
        reader.ExpectTag("MTST");
        var version = reader.ReadUInt32();
        var nameHash = reader.ReadUInt32();
        var indexReference = reader.ReadUInt32();
        var indexChunkIndex = VfxRcolDecoder.ResolveChunkIndex(rcol, indexReference);
        var count = reader.ReadCount("MTST material");
        var entries = new VfxMaterialSetEntry[count];
        for (var index = 0; index < count; index++)
        {
            var materialReference = reader.ReadUInt32();
            var state = reader.ReadUInt32();
            var variant = version < 0x00000300 ? 0u : reader.ReadUInt32();
            entries[index] = new VfxMaterialSetEntry(
                materialReference,
                VfxRcolDecoder.ResolveChunkIndex(rcol, materialReference),
                state,
                variant);
        }

        return new VfxMaterialSet(chunkIndex, version, nameHash, indexReference, indexChunkIndex, entries);
    }

    public static VfxMaterialDefinition DecodeMaterialDefinition(VfxRcolResource rcol, int chunkIndex)
    {
        ArgumentNullException.ThrowIfNull(rcol);
        if ((uint)chunkIndex >= (uint)rcol.Chunks.Count)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex));

        var chunk = rcol.Chunks[chunkIndex];
        if (!string.Equals(chunk.Tag, "MATD", StringComparison.Ordinal))
            throw new InvalidDataException($"RCOL chunk [{chunkIndex}] is '{chunk.Tag}' instead of 'MATD'.");

        var data = chunk.Data.Span;
        var reader = new Reader(data);
        reader.ExpectTag("MATD");
        var version = reader.ReadUInt32();
        var materialNameHash = reader.ReadUInt32();
        var shader = reader.ReadUInt32();
        var mtrlLength = reader.ReadUInt32();
        var isVideoSurface = false;
        var isPaintingSurface = false;
        if (version >= 0x00000103)
        {
            isVideoSurface = reader.ReadInt32() != 0;
            isPaintingSurface = reader.ReadInt32() != 0;
        }

        var mtrlStart = reader.Position;
        var mtrlLengthInt = checked((int)mtrlLength);
        if (mtrlLengthInt < 0 || mtrlStart > data.Length - mtrlLengthInt)
            throw new InvalidDataException($"MATD chunk [{chunkIndex}] MTRL length {mtrlLength:N0} exceeds the chunk payload.");

        var mtrl = DecodeMtrl(data.Slice(mtrlStart, mtrlLengthInt));
        return new VfxMaterialDefinition(
            chunkIndex,
            version,
            materialNameHash,
            shader,
            mtrlLength,
            isVideoSurface,
            isPaintingSurface,
            mtrl);
    }

    private static VfxMtrlData DecodeMtrl(ReadOnlySpan<byte> data)
    {
        var reader = new Reader(data);
        reader.ExpectTag("MTRL");
        var unknown1 = reader.ReadUInt32();
        var unknown2 = reader.ReadUInt16();
        var unknown3 = reader.ReadUInt16();
        var count = reader.ReadCount("MTRL shader-data");
        var headers = new ShaderDataHeader[count];
        for (var index = 0; index < count; index++)
        {
            headers[index] = new ShaderDataHeader(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadUInt32());
        }

        var entries = new VfxShaderDataEntry[count];
        for (var index = 0; index < count; index++)
            entries[index] = DecodeShaderData(data, headers[index]);

        return new VfxMtrlData(unknown1, unknown2, unknown3, entries);
    }

    private static VfxShaderDataEntry DecodeShaderData(ReadOnlySpan<byte> mtrl, ShaderDataHeader header)
    {
        if (header.Count < 0 || header.Count > MaximumEntries)
            throw new InvalidDataException($"Invalid shader-data component count {header.Count} for field 0x{header.Field:X8}.");

        var byteCount = checked(header.Count * 4);
        var offset = checked((int)header.Offset);
        if (offset < 0 || offset > mtrl.Length - byteCount)
            throw new InvalidDataException($"Shader-data field 0x{header.Field:X8} points outside the MTRL (offset={header.Offset}, bytes={byteCount}).");

        var data = mtrl.Slice(offset, byteCount);
        ResourceKey? textureKey = null;
        string value;
        switch (header.DataType)
        {
            case DataTypeFloat when header.Count is >= 1 and <= 4:
                value = FormatFloats(data, header.Count);
                break;
            case DataTypeInt when header.Count == 1:
                value = BinaryPrimitives.ReadInt32LittleEndian(data).ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case DataTypeTexture when header.Count is 4 or 5:
                textureKey = ReadItgKey(data);
                value = FormatKey(textureKey.Value);
                if (header.Count == 5)
                    value += $" • extra 0x{BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4)):X8}";
                break;
            case DataTypeImageMap when header.Count == 4:
                textureKey = ReadItgKey(data);
                value = FormatKey(textureKey.Value);
                break;
            default:
                value = byteCount == 0 ? "—" : Convert.ToHexString(data);
                break;
        }

        return new VfxShaderDataEntry(
            header.Field,
            header.DataType,
            header.Count,
            header.Offset,
            value,
            textureKey);
    }

    private static string FormatFloats(ReadOnlySpan<byte> data, int count)
    {
        Span<float> values = stackalloc float[4];
        for (var index = 0; index < count; index++)
        {
            var bits = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(index * 4, 4));
            values[index] = BitConverter.Int32BitsToSingle(bits);
        }
        return count == 1
            ? values[0].ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
            : $"({string.Join(", ", values[..count].ToArray().Select(value => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)))})";
    }

    private static ResourceKey ReadItgKey(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
            throw new InvalidDataException("Texture shader data is shorter than an ITG resource key.");
        var instance = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(0, 8));
        var type = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
        var group = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
        return new ResourceKey(type, group, instance);
    }

    public static string FormatShader(uint shader)
        => shader switch
        {
            0x00000000 => "None",
            0x0B272CC5 => "Subtractive",
            0x0CB82EB8 => "Instanced",
            0x14FA335E => "FullBright",
            0x213D6300 => "PreviewWallsAndFloors",
            0x21FE207D => "ShadowMap",
            0x265FFAA1 => "GlassForRabbitHoles",
            0x277CF8EB => "ImpostorWater",
            0x2A72B9A1 => "Rug",
            0x3939E094 => "Trampoline",
            0x4549E22E => "Foliage",
            0x460E93F4 => "ParticleAnim",
            0x47C6638C => "SolidPhong",
            0x492ECA7C => "GlassForObjects",
            0x4CE2F497 => "Stairs",
            0x4D26BEC0 => "OutdoorProp",
            0x52986C62 => "GlassForFences",
            0x548394B9 => "SimSkin",
            0x5AF16731 => "Additive",
            0x5EDA9CDE => "SimGlass",
            0x67107FE8 => "Fence",
            0x68601DE3 => "LotImposter",
            0x6864A45E => "Blueprint",
            0x6AAD2AD5 => "BasinWater",
            0x70FDE012 => "StandingWater",
            0x7B036C01 => "BuildingWindow",
            0x7BD05F63 => "Roof",
            0x81DD204D => "GlassForPortals",
            0x849CF021 => "GlassForObjectsTranslucent",
            0x84FD7152 => "SimHair",
            0x8A60B969 => "Landmark",
            0x8D346BBC => "RabbitHoleHighDetail",
            0x94B9A835 => "CASRoom",
            0x9D9DA161 => "SimEyelashes",
            0xA063C1D0 => "Gemstones",
            0xA4172F62 => "Counters",
            0xA68D9E29 => "FlatMirror",
            0xAA495821 => "Painting",
            0xAEDE7105 => "RabbitHoleMediumDetail",
            0xB9105A6D => "Phong",
            0xBC84D000 => "Floors",
            0xC09C7582 => "DropShadow",
            0xCF8A70B4 => "SimEyes",
            0xDEF16564 => "Plumbob",
            0xE5D98507 => "SculptureIce",
            0xFC5FC212 => "PhongAlpha",
            0xFF5E6908 => "ParticleJet",
            _ => "Unknown"
        };

    public static string FormatMaterialState(uint state)
        => state switch
        {
            0x2EA8FB98 => "Default",
            0xEEAB4327 => "Dirty",
            0x2E5DF9BB => "VeryDirty",
            0xC3867C32 => "Burnt",
            0x257FB026 => "Clogged",
            0xE4AF52C1 => "carLightsOff",
            _ => "Unknown"
        };

    public static string FormatDataType(uint dataType)
        => dataType switch
        {
            DataTypeFloat => "Float",
            DataTypeInt => "Int",
            DataTypeTexture => "Texture",
            DataTypeImageMap => "ImageMap",
            _ => "Unknown"
        };

    public static string FormatField(uint field)
        => field switch
        {
            0x05D22FD3 => "Transparency",
            0x0995E96C => "BlendSourceMode",
            0x2D13B939 => "BlendOperation",
            0x406ADE00 => "FramesPerSecond",
            0x4168508B => "BloomFactor",
            0x490E6EB4 => "EmissiveBloomMultiplier",
            0x5E86DEA1 => "NoiseMapScale",
            0x7211F24F => "FramesRandomStartFactor",
            0x88C64AE2 => "NormalBumpScale",
            0x8EF71C85 => "EmissiveLightMultiplier",
            0x9BDECB37 => "BlendDestMode",
            0xA15E4594 => "LightingEnabled",
            0xB597FA7F => "UseDiffuseForAlphaTest",
            0xC45A5F41 => "DiffuseMapUVChannel",
            0xD600CB63 => "AnimSpeed",
            0xE77A2B60 => "AlphaMaskThreshold",
            0xEF270EE4 => "LightingDirectScale",
            0xF755F7FF => "Shininess",
            0x2D4E507E => "DiffuseUVScale",
            0x773CAB85 => "UVTiling",
            0xBA2D1AB9 => "NormalUVScale",
            0xF12E27C3 => "SpecularUVScale",
            0xF2EEA6EC => "UVScrollSpeed",
            0x2CE11842 => "Specular",
            0x3BD441A0 => "Emission",
            0x637DAA05 => "Diffuse",
            0x91EEBAFF => "DiffuseUVSelector",
            0x988403F9 => "Transparent",
            0xA2FD73CA => "VertexColorScale",
            0xB63546AC => "SpecularUVSelector",
            0xBC823DDC => "EmissionMapUVSelector",
            0x159BA53E => "UVScale",
            0x1E5B2324 => "FrameData",
            0x57582869 => "UVOffset",
            0x1D90C086 => "SparkleCube",
            0x22AD8507 => "DropShadowAtlas",
            0x48372E62 => "DirtOverlay",
            0x4DC0C8BC => "OverlayTexture",
            0x52CE211B => "JetTexture",
            0x581835D6 => "ColorRamp",
            0x6CC0FD85 => "DiffuseMap",
            0x6E067554 => "SelfIlluminationMap",
            0x6E56548A => "NormalMap",
            0x84F6E0FB => "HaloRamp",
            0x9205DAA8 => "DetailMap",
            0xAD528A60 => "SpecularMap",
            0xB01CBA60 => "AmbientOcclusionMap",
            0xC3FAAC4F => "AlphaMap",
            0xCD869A45 => "MultiplyMap",
            0xD652FADE => "SpecCompositeTexture",
            0xE19FD579 => "NoiseMap",
            0xE7CA9166 => "RoomLightMap",
            0xF303D152 => "EmissionMap",
            0xF3F22AC4 => "RevealMap",
            0x15C9D298 => "ImposterTextureAOandSI",
            0x56E1C6B2 => "ImpostorDetailTexture",
            0xBDCF71C5 => "ImposterTexture",
            0xBF3FB9FA => "ImposterTextureWater",
            _ => "Unknown"
        };

    public static string FormatKey(ResourceKey key)
        => $"{key.Type:X8}:{key.Group:X8}:{key.Instance:X16}";

    private readonly record struct ShaderDataHeader(uint Field, uint DataType, int Count, uint Offset);

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

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(Position, 2));
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

        public int ReadCount(string label)
        {
            var value = ReadInt32();
            if (value < 0 || value > MaximumEntries)
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

        private void Ensure(int count)
        {
            if (count < 0 || Position < 0 || Position > _data.Length - count)
                throw new InvalidDataException("Unexpected end of material chunk.");
        }
    }
}
