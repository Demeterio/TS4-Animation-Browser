using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace TS4AnimationBrowser.App;

public enum VfxSwarmBlockType : byte
{
    VisualEffect = 0,
    ParticleEffect = 1,
    MetaparticleEffect = 2,
    DecalEffect = 3,
    SequenceEffect = 4,
    SoundEffect = 5,
    ShakeEffect = 6,
    CameraEffect = 7,
    ModelEffect = 8,
    ScreenEffect = 9,
    GameEffect = 11,
    FastParticleEffect = 12,
    DistributeEffect = 13,
    RibbonEffect = 14,
    SpriteEffect = 15
}

public sealed record VfxSwarmEffectIdentity(
    uint VisualEffectIndex,
    ulong EffectInstance,
    string Name);

public sealed record VfxSwarmVisualBlock(
    VfxSwarmBlockType BlockType,
    uint BlockIndex,
    string? ReferencedEffectName,
    uint Flags,
    ushort LocalTransformFlags,
    float LocalScale,
    float Orientation11,
    float Orientation12,
    float Orientation13,
    float Orientation21,
    float Orientation22,
    float Orientation23,
    float Orientation31,
    float Orientation32,
    float Orientation33,
    float PositionX,
    float PositionY,
    float PositionZ,
    float TimeScale,
    byte LodBegin,
    byte LodEnd);

public sealed record VfxSwarmVisualEffect(
    uint Index,
    uint Flags,
    uint Seed,
    IReadOnlyList<VfxSwarmVisualBlock> Blocks);

public sealed record VfxSwarmResource(
    ushort LibraryVersionMajor,
    byte LibraryVersionMinor,
    ushort VisualEffectVersion,
    IReadOnlyList<VfxSwarmEffectIdentity> Effects,
    IReadOnlyList<VfxSwarmVisualEffect> VisualEffects);

public static class VfxSwarmDecoder
{
    private const int MaximumEffectCount = 65_536;
    private const int MaximumBlocksPerEffect = 16_384;
    private const int MaximumCurveValues = 65_536;
    private const int MaximumLodValues = 4_096;
    private const int MaximumNameLength = 512;
    private const int SearchTailBytes = 512 * 1024;
    private const uint EndMarker = uint.MaxValue;

    public static VfxSwarmResource DecodeModern(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 32)
            throw new InvalidDataException("The Swarm VFX resource is too small.");

        var versionMajor = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var versionMinor = data[2];
        var tail = FindValidatedTailTables(data);
        var expectedVisualCount = checked((int)(tail.Identities.Max(identity => identity.VisualEffectIndex) + 1));
        if (expectedVisualCount != tail.Identities.Count)
            throw new InvalidDataException("The Swarm effect index table is not contiguous.");

        if (!TryFindVisualEffectTable(data, tail.EffectIdTableOffset, expectedVisualCount, out var visualVersion, out var visualEffects))
            throw new InvalidDataException("The Swarm VisualEffect table could not be located or validated.");

        var namesByIndex = tail.Identities.ToDictionary(identity => identity.VisualEffectIndex, identity => identity.Name);
        var resolvedEffects = visualEffects
            .Select(effect => new VfxSwarmVisualEffect(
                effect.Index,
                effect.Flags,
                effect.Seed,
                effect.Blocks.Select(block => block with
                {
                    ReferencedEffectName = block.BlockType == VfxSwarmBlockType.VisualEffect
                        && namesByIndex.TryGetValue(block.BlockIndex, out var referencedName)
                            ? referencedName
                            : null
                }).ToArray()))
            .ToArray();

        return new VfxSwarmResource(
            versionMajor,
            versionMinor,
            visualVersion,
            tail.Identities.OrderBy(identity => identity.VisualEffectIndex).ToArray(),
            resolvedEffects);
    }

    private static TailTables FindValidatedTailTables(byte[] data)
    {
        var searchStart = Math.Max(3, data.Length - SearchTailBytes);
        for (var start = searchStart; start <= data.Length - 9; start++)
        {
            if (!TryReadEffectNames(data, start, out var names, out var nameTableEnd))
                continue;
            if (!HasOnlyTrailingPadding(data, nameTableEnd))
                continue;
            if (names.Count == 0 || names.Count > MaximumEffectCount)
                continue;

            var indexes = names.Select(record => record.Index).ToArray();
            if (indexes.Distinct().Count() != indexes.Length)
                continue;
            if (indexes.Max() + 1 != indexes.Length)
                continue;

            var effectIdTableLength = checked((names.Count * 12) + 4);
            var effectIdStart = start - effectIdTableLength;
            if (effectIdStart < 3)
                continue;
            if (!TryReadEffectIds(data, effectIdStart, names.Count, start, out var ids))
                continue;

            var idsByIndex = ids.ToDictionary(record => record.Index);
            if (idsByIndex.Count != names.Count || names.Any(name => !idsByIndex.ContainsKey(name.Index)))
                continue;

            var identities = names
                .Select(name => new VfxSwarmEffectIdentity(name.Index, idsByIndex[name.Index].Instance, name.Name))
                .ToArray();
            return new TailTables(effectIdStart, identities);
        }

        throw new InvalidDataException("No validated Swarm effect name/instance tables were found.");
    }

    private static bool TryReadEffectNames(
        byte[] data,
        int start,
        out List<EffectNameRecord> names,
        out int endOffset)
    {
        names = new List<EffectNameRecord>();
        endOffset = start;
        var offset = start;

        for (var count = 0; count < MaximumEffectCount; count++)
        {
            if (offset + 4 > data.Length)
                return false;

            var index = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            offset += 4;
            if (index == EndMarker)
            {
                if (names.Count == 0)
                    return false;
                endOffset = offset;
                return true;
            }

            if (!TryReadAsciiZString(data, ref offset, out var name))
                return false;
            names.Add(new EffectNameRecord(index, name));
        }

        return false;
    }

    private static bool TryReadEffectIds(
        byte[] data,
        int start,
        int expectedCount,
        int expectedEnd,
        out List<EffectIdRecord> ids)
    {
        ids = new List<EffectIdRecord>(expectedCount);
        var offset = start;
        for (var index = 0; index < expectedCount; index++)
        {
            if (offset + 12 > data.Length)
                return false;

            var effectIndex = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            var instance = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset + 4, 8));
            offset += 12;
            if (effectIndex == EndMarker)
                return false;
            ids.Add(new EffectIdRecord(effectIndex, instance));
        }

        if (offset + 4 != expectedEnd)
            return false;
        return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)) == EndMarker;
    }

    private static bool TryFindVisualEffectTable(
        byte[] data,
        int expectedEnd,
        int expectedCount,
        out ushort version,
        out VfxSwarmVisualEffect[] effects)
    {
        version = 0;
        effects = Array.Empty<VfxSwarmVisualEffect>();
        if (expectedCount <= 0 || expectedCount > MaximumEffectCount)
            return false;

        for (var start = expectedEnd - 6; start >= 3; start--)
        {
            var candidateVersion = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start, 2));
            if (candidateVersion is < 1 or > 16)
                continue;

            var candidateCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 2, 4));
            if (candidateCount != expectedCount)
                continue;

            try
            {
                var reader = new SwarmReader(data, start + 6, expectedEnd);
                var parsed = new VfxSwarmVisualEffect[expectedCount];
                for (var effectIndex = 0; effectIndex < expectedCount; effectIndex++)
                    parsed[effectIndex] = ReadVisualEffect(ref reader, candidateVersion, (uint)effectIndex);

                if (reader.Position != expectedEnd)
                    continue;

                version = candidateVersion;
                effects = parsed;
                return true;
            }
            catch (InvalidDataException)
            {
            }
            catch (OverflowException)
            {
            }
        }

        return false;
    }

    private static VfxSwarmVisualEffect ReadVisualEffect(ref SwarmReader reader, ushort version, uint effectIndex)
    {
        var flags = reader.ReadUInt32BigEndian();
        reader.Skip(4); // ComponentAppFlagsMask, little-endian in TS4VFXTool.
        reader.Skip(4); // NotifyMessageID, little-endian.
        reader.Skip(4); // ScreenSizeRange0, little-endian float.
        reader.Skip(4); // ScreenSizeRange1, little-endian float.
        reader.Skip(4); // CursorActiveDistance, little-endian float.
        reader.Skip(1); // CursorButton.
        if (version >= 15)
            reader.Skip(4);

        var lodDistanceCount = reader.ReadCountBigEndian(MaximumLodValues, "VisualEffect LOD distance");
        reader.SkipChecked(lodDistanceCount, 4);
        reader.Skip(12); // ExtendedLODWeights[3], little-endian floats.
        var seed = reader.ReadUInt32LittleEndian();

        var blockCount = reader.ReadCountBigEndian(MaximumBlocksPerEffect, "VisualEffect block");
        var blocks = new VfxSwarmVisualBlock[blockCount];
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
            blocks[blockIndex] = ReadVisualBlock(ref reader, version);

        return new VfxSwarmVisualEffect(effectIndex, flags, seed, blocks);
    }

    private static VfxSwarmVisualBlock ReadVisualBlock(ref SwarmReader reader, ushort version)
    {
        var blockType = (VfxSwarmBlockType)reader.ReadByte();
        var flags = reader.ReadUInt32BigEndian();
        var localTransformFlags = reader.ReadUInt16BigEndian();
        var localScale = reader.ReadSingleBigEndian();
        var orientation11 = reader.ReadSingleLittleEndian();
        var orientation12 = reader.ReadSingleLittleEndian();
        var orientation13 = reader.ReadSingleLittleEndian();
        var orientation21 = reader.ReadSingleLittleEndian();
        var orientation22 = reader.ReadSingleLittleEndian();
        var orientation23 = reader.ReadSingleLittleEndian();
        var orientation31 = reader.ReadSingleLittleEndian();
        var orientation32 = reader.ReadSingleLittleEndian();
        var orientation33 = reader.ReadSingleLittleEndian();
        var positionX = reader.ReadSingleLittleEndian();
        var positionY = reader.ReadSingleLittleEndian();
        var positionZ = reader.ReadSingleLittleEndian();
        var lodBegin = reader.ReadByte();
        var lodEnd = reader.ReadByte();

        var lodScaleCount = reader.ReadCountBigEndian(MaximumLodValues, "VisualEffect block LOD scale");
        reader.SkipChecked(lodScaleCount, 12);
        reader.Skip(4 + 4 + 4 + 4); // Unknown20..23.
        var referencedBlockIndex = reader.ReadUInt32BigEndian();
        reader.Skip(2); // Unknown1, Unknown2.

        SkipFloatCurveLittleEndian(ref reader, "VisualEffect block Unknown3");
        reader.Skip(8); // IID, big-endian.
        var timeScale = reader.ReadSingleBigEndian();
        reader.Skip(4 + 4); // Unknown5, Unknown6.

        for (var curve = 0; curve < 8; curve++)
            SkipFloatCurveBigEndian(ref reader, "VisualEffect block curve"); // Unknown7..14.
        if (version >= 13)
        {
            SkipFloatCurveBigEndian(ref reader, "VisualEffect block Unknown15");
            SkipFloatCurveBigEndian(ref reader, "VisualEffect block Unknown16");
        }
        if (version >= 14)
            SkipFloatCurveBigEndian(ref reader, "VisualEffect block Unknown17");
        if (version >= 16)
            reader.Skip(8); // Unknown24, Unknown25.

        return new VfxSwarmVisualBlock(
            blockType,
            referencedBlockIndex,
            null,
            flags,
            localTransformFlags,
            localScale,
            orientation11,
            orientation12,
            orientation13,
            orientation21,
            orientation22,
            orientation23,
            orientation31,
            orientation32,
            orientation33,
            positionX,
            positionY,
            positionZ,
            timeScale,
            lodBegin,
            lodEnd);
    }

    private static void SkipFloatCurveLittleEndian(ref SwarmReader reader, string label)
    {
        var count = reader.ReadCountBigEndian(MaximumCurveValues, label);
        reader.SkipChecked(count, 4);
    }

    private static void SkipFloatCurveBigEndian(ref SwarmReader reader, string label)
    {
        var count = reader.ReadCountBigEndian(MaximumCurveValues, label);
        reader.SkipChecked(count, 4);
    }

    private static bool TryReadAsciiZString(byte[] data, ref int offset, out string value)
    {
        value = string.Empty;
        if (offset < 0 || offset >= data.Length)
            return false;

        var end = offset;
        var limit = Math.Min(data.Length, offset + MaximumNameLength);
        while (end < limit && data[end] != 0)
        {
            var current = data[end];
            if (current is < 0x20 or > 0x7E)
                return false;
            end++;
        }

        if (end == offset || end >= data.Length || data[end] != 0)
            return false;

        value = Encoding.ASCII.GetString(data, offset, end - offset);
        offset = end + 1;
        return true;
    }

    private static bool HasOnlyTrailingPadding(byte[] data, int offset)
    {
        if (offset == data.Length)
            return true;
        if (data.Length - offset > 16)
            return false;

        for (var index = offset; index < data.Length; index++)
        {
            if (data[index] != 0)
                return false;
        }
        return true;
    }

    private sealed record EffectNameRecord(uint Index, string Name);
    private sealed record EffectIdRecord(uint Index, ulong Instance);
    private sealed record TailTables(int EffectIdTableOffset, IReadOnlyList<VfxSwarmEffectIdentity> Identities);

    private ref struct SwarmReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly int _end;

        public SwarmReader(byte[] data, int position, int end)
        {
            if (position < 0 || end < position || end > data.Length)
                throw new InvalidDataException("Invalid Swarm reader bounds.");
            _data = data;
            Position = position;
            _end = end;
        }

        public int Position { get; private set; }

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _data[Position++];
        }

        public ushort ReadUInt16LittleEndian()
        {
            EnsureAvailable(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(Position, 2));
            Position += 2;
            return value;
        }

        public ushort ReadUInt16BigEndian()
        {
            EnsureAvailable(2);
            var value = BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(Position, 2));
            Position += 2;
            return value;
        }

        public uint ReadUInt32LittleEndian()
        {
            EnsureAvailable(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(Position, 4));
            Position += 4;
            return value;
        }

        public uint ReadUInt32BigEndian()
        {
            EnsureAvailable(4);
            var value = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(Position, 4));
            Position += 4;
            return value;
        }

        public float ReadSingleLittleEndian()
        {
            EnsureAvailable(4);
            var bits = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(Position, 4));
            Position += 4;
            return BitConverter.Int32BitsToSingle(bits);
        }

        public float ReadSingleBigEndian()
        {
            EnsureAvailable(4);
            var bits = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(Position, 4));
            Position += 4;
            return BitConverter.Int32BitsToSingle(bits);
        }

        public int ReadCountBigEndian(int maximum, string label)
        {
            var value = ReadUInt32BigEndian();
            if (value > maximum || value > int.MaxValue)
                throw new InvalidDataException($"{label} count {value:N0} exceeds the supported validation limit.");
            return (int)value;
        }

        public void Skip(int count)
        {
            if (count < 0)
                throw new InvalidDataException("Negative Swarm skip length.");
            EnsureAvailable(count);
            Position += count;
        }

        public void SkipChecked(int itemCount, int itemSize)
        {
            var byteCount = checked(itemCount * itemSize);
            Skip(byteCount);
        }

        private void EnsureAvailable(int count)
        {
            if (count < 0 || Position > _end - count)
                throw new InvalidDataException("The Swarm VFX resource ended unexpectedly while decoding VisualEffect data.");
        }
    }
}
