using System.Buffers.Binary;
using System.IO;

namespace TS4AnimationBrowser.App;

public sealed record VfxParticleColor(float Red, float Green, float Blue);

public sealed record VfxSwarmParticleEffect(
    uint Index,
    ulong Flags,
    float ParticleLifetimeMin,
    float ParticleLifetimeMax,
    float EmitDelayMin,
    float EmitDelayMax,
    float EmitRetriggerMin,
    float EmitRetriggerMax,
    float EmitDirectionMinX,
    float EmitDirectionMinY,
    float EmitDirectionMinZ,
    float EmitDirectionMaxX,
    float EmitDirectionMaxY,
    float EmitDirectionMaxZ,
    float EmitSpeedMin,
    float EmitSpeedMax,
    float EmitVolumeMinX,
    float EmitVolumeMinY,
    float EmitVolumeMinZ,
    float EmitVolumeMaxX,
    float EmitVolumeMaxY,
    float EmitVolumeMaxZ,
    IReadOnlyList<float> EmitRateCurve,
    float EmitRateCurveTime,
    IReadOnlyList<float> SizeCurve,
    float SizeVary,
    IReadOnlyList<float> AspectRatioCurve,
    float AspectRatioVary,
    float RotationVary,
    float RotationOffset,
    IReadOnlyList<float> RotationCurve,
    IReadOnlyList<float> AlphaCurve,
    float AlphaVary,
    IReadOnlyList<VfxParticleColor> ColorCurve,
    float ColorVaryRed,
    float ColorVaryGreen,
    float ColorVaryBlue,
    ulong TextureInstance,
    byte Format,
    byte DrawMode,
    uint DrawValue,
    ushort DrawFlags,
    byte Buffer,
    ushort Layer,
    float SortOffset,
    ulong SecondaryTextureInstance,
    byte PhysicsType,
    byte OverrideSet,
    byte TileCountU,
    byte TileCountV,
    byte AlignMode,
    float FrameSpeed,
    byte FrameStart,
    byte FrameCount,
    byte FrameRandom,
    float DirectionalForceX,
    float DirectionalForceY,
    float DirectionalForceZ,
    float WindStrength,
    float GravityStrength,
    float Drag,
    float VelocityStretch);

public sealed record VfxParticleTable(
    ushort Version,
    IReadOnlyList<VfxSwarmParticleEffect> Effects);

public static class VfxParticleDecoder
{
    private const int MaximumParticles = 65_536;
    private const int MaximumCurveValues = 65_536;
    private const int MaximumSurfaces = 16_384;
    private const int MaximumSurfacePoints = 65_536;
    private const int MaximumStringLength = 4_096;

    public static VfxParticleTable DecodeModern(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 12)
            throw new InvalidDataException("The Swarm VFX resource is too small for a ParticleEffect table.");

        var reader = new Reader(data, 3, data.Length);
        var blockType = reader.ReadUInt16BigEndian();
        if (blockType != (ushort)VfxSwarmBlockType.ParticleEffect)
            throw new InvalidDataException($"Expected ParticleEffect block type 1 at the start of the modern Swarm table, found {blockType}.");

        var version = reader.ReadUInt16BigEndian();
        if (version is < 1 or > 27)
            throw new InvalidDataException($"Unsupported ParticleEffect version {version}.");

        var count = reader.ReadCountBigEndian(MaximumParticles, "ParticleEffect");
        var particles = new VfxSwarmParticleEffect[count];
        for (var index = 0; index < count; index++)
            particles[index] = ReadParticle(ref reader, version, (uint)index);

        if (reader.Remaining >= 2)
        {
            var nextBlock = reader.PeekUInt16BigEndian();
            if (nextBlock is not 2 and not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9
                and not 11 and not 12 and not 13 and not 14 and not 15 and not ushort.MaxValue)
            {
                throw new InvalidDataException($"ParticleEffect table ended at an invalid next block marker 0x{nextBlock:X4}.");
            }
        }

        return new VfxParticleTable(version, particles);
    }

    private static VfxSwarmParticleEffect ReadParticle(ref Reader reader, ushort version, uint index)
    {
        var flags = reader.ReadUInt64BigEndian();
        var lifetime1 = reader.ReadSingleLittleEndian();
        var lifetime2 = reader.ReadSingleLittleEndian();
        reader.ReadSingleBigEndian();
        var emitDelay1 = reader.ReadSingleLittleEndian();
        var emitDelay2 = reader.ReadSingleLittleEndian();
        var emitRetrigger1 = reader.ReadSingleLittleEndian();
        var emitRetrigger2 = reader.ReadSingleLittleEndian();
        var directionMinX = reader.ReadSingleLittleEndian();
        var directionMinY = reader.ReadSingleLittleEndian();
        var directionMinZ = reader.ReadSingleLittleEndian();
        var directionMaxX = reader.ReadSingleLittleEndian();
        var directionMaxY = reader.ReadSingleLittleEndian();
        var directionMaxZ = reader.ReadSingleLittleEndian();
        var speed1 = reader.ReadSingleLittleEndian();
        var speed2 = reader.ReadSingleLittleEndian();
        reader.ReadSingleBigEndian();
        reader.ReadSingleBigEndian();
        var volumeMinX = reader.ReadSingleLittleEndian();
        var volumeMinY = reader.ReadSingleLittleEndian();
        var volumeMinZ = reader.ReadSingleLittleEndian();
        var volumeMaxX = reader.ReadSingleLittleEndian();
        var volumeMaxY = reader.ReadSingleLittleEndian();
        var volumeMaxZ = reader.ReadSingleLittleEndian();
        reader.ReadSingleBigEndian();

        var emitRateCurve = ReadBigEndianFloatCurve(ref reader, "ParticleEffect emit rate");
        var emitRateCurveTime = reader.ReadSingleBigEndian();
        reader.ReadUInt16BigEndian();
        reader.ReadSingleBigEndian();
        reader.ReadSingleBigEndian();
        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown3");
        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown5");
        reader.Skip(12);
        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown9");
        SkipLittleEndianFloatCurve(ref reader, "ParticleEffect Unknown1");
        reader.Skip(4);
        if (version >= 24)
        {
            SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown10");
            SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown11");
        }
        if (version >= 27)
            SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown12");

        var sizeCurve = ReadBigEndianFloatCurve(ref reader, "ParticleEffect size");
        var sizeVary = reader.ReadSingleBigEndian();
        var aspectRatioCurve = ReadBigEndianFloatCurve(ref reader, "ParticleEffect aspect ratio");
        var aspectRatioVary = reader.ReadSingleBigEndian();
        var rotationVary = reader.ReadSingleBigEndian();
        var rotationOffset = reader.ReadSingleBigEndian();
        var rotationCurve = ReadBigEndianFloatCurve(ref reader, "ParticleEffect rotation");
        var alphaCurve = ReadBigEndianFloatCurve(ref reader, "ParticleEffect alpha");
        var alphaVary = reader.ReadSingleBigEndian();

        var colorCount = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect color");
        var colorCurve = new VfxParticleColor[colorCount];
        for (var colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            colorCurve[colorIndex] = new VfxParticleColor(
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian());
        }

        var colorVaryRed = reader.ReadSingleLittleEndian();
        var colorVaryGreen = reader.ReadSingleLittleEndian();
        var colorVaryBlue = reader.ReadSingleLittleEndian();
        var textureInstance = reader.ReadUInt64BigEndian();
        var format = reader.ReadByte();
        var drawMode = reader.ReadByte();
        var drawValue = reader.ReadUInt32BigEndian();
        var drawFlags = reader.ReadUInt16BigEndian();
        var buffer = reader.ReadByte();
        var layer = reader.ReadUInt16BigEndian();
        var sortOffset = reader.ReadSingleBigEndian();
        var secondaryTextureInstance = reader.ReadUInt64BigEndian();
        var physicsType = reader.ReadByte();
        var overrideSet = reader.ReadByte();
        var tileCountU = reader.ReadByte();
        var tileCountV = reader.ReadByte();
        var alignMode = reader.ReadByte();
        var frameSpeed = reader.ReadSingleBigEndian();
        var frameStart = reader.ReadByte();
        var frameCount = reader.ReadByte();
        var frameRandom = reader.ReadByte();
        var directionalForceX = reader.ReadSingleLittleEndian();
        var directionalForceY = reader.ReadSingleLittleEndian();
        var directionalForceZ = reader.ReadSingleLittleEndian();
        var windStrength = reader.ReadSingleBigEndian();
        var gravityStrength = reader.ReadSingleBigEndian();
        reader.ReadSingleBigEndian();
        reader.Skip(12);
        var drag = reader.ReadSingleBigEndian();
        var velocityStretch = reader.ReadSingleBigEndian();
        reader.ReadSingleBigEndian();

        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown80");
        reader.Skip(4);
        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown82");
        reader.Skip(4);

        var wiggleCount = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect wiggle");
        reader.SkipChecked(wiggleCount, 28);
        reader.Skip(4);

        var loopColorCount = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect loop color");
        reader.SkipChecked(loopColorCount, 12);
        SkipLittleEndianFloatCurve(ref reader, "ParticleEffect loop alpha");

        var surfaceCount = reader.ReadCountBigEndian(MaximumSurfaces, "ParticleEffect surface");
        for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            reader.Skip(4 + 8 + 4 + 4 + 4 + 4 + 4);
            reader.SkipAsciiZString(MaximumStringLength);
            reader.SkipAsciiZString(MaximumStringLength);
            var surfacePointCount = reader.ReadCountBigEndian(MaximumSurfacePoints, "ParticleEffect surface point");
            reader.SkipChecked(surfacePointCount, 12);
        }

        reader.Skip(7 * 4);
        reader.Skip(2 * 4);
        reader.Skip(3 * 8);
        reader.Skip(4 * 4);
        reader.Skip(3 * 4);
        SkipLittleEndianFloatCurve(ref reader, "ParticleEffect turn offset");
        reader.Skip(1);
        reader.Skip(6 * 4);
        SkipBigEndianFloatCurve(ref reader, "ParticleEffect attractor strength");
        reader.Skip(2 * 4);
        reader.Skip(1);

        var pathPointCount = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect path point");
        reader.SkipChecked(pathPointCount, 28);
        reader.Skip(3 * 4);

        var unknown16Count = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect Unknown16");
        reader.SkipChecked(unknown16Count, 12);
        reader.Skip(1 + 4 + 12 + 4 + 1 + 12 + 8);

        var unknown32Count = reader.ReadByte();
        reader.SkipChecked(unknown32Count, 37);
        var unknown32a = reader.ReadByte();
        if (unknown32a > 0)
            reader.Skip(7);

        SkipBigEndianFloatCurve(ref reader, "ParticleEffect Unknown33");
        reader.Skip(4);
        reader.Skip(1);
        var unknown37Count = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect Unknown37");
        reader.Skip(unknown37Count);
        var unknown38Count = reader.ReadCountBigEndian(MaximumCurveValues, "ParticleEffect Unknown38");
        reader.SkipChecked(unknown38Count, 12);
        reader.Skip(12);
        if (version >= 24)
            reader.Skip(6);

        return new VfxSwarmParticleEffect(
            index,
            flags,
            lifetime1,
            lifetime2,
            emitDelay1,
            emitDelay2,
            emitRetrigger1,
            emitRetrigger2,
            directionMinX,
            directionMinY,
            directionMinZ,
            directionMaxX,
            directionMaxY,
            directionMaxZ,
            speed1,
            speed2,
            volumeMinX,
            volumeMinY,
            volumeMinZ,
            volumeMaxX,
            volumeMaxY,
            volumeMaxZ,
            emitRateCurve,
            emitRateCurveTime,
            sizeCurve,
            sizeVary,
            aspectRatioCurve,
            aspectRatioVary,
            rotationVary,
            rotationOffset,
            rotationCurve,
            alphaCurve,
            alphaVary,
            colorCurve,
            colorVaryRed,
            colorVaryGreen,
            colorVaryBlue,
            textureInstance,
            format,
            drawMode,
            drawValue,
            drawFlags,
            buffer,
            layer,
            sortOffset,
            secondaryTextureInstance,
            physicsType,
            overrideSet,
            tileCountU,
            tileCountV,
            alignMode,
            frameSpeed,
            frameStart,
            frameCount,
            frameRandom,
            directionalForceX,
            directionalForceY,
            directionalForceZ,
            windStrength,
            gravityStrength,
            drag,
            velocityStretch);
    }

    private static float[] ReadBigEndianFloatCurve(ref Reader reader, string label)
    {
        var count = reader.ReadCountBigEndian(MaximumCurveValues, label);
        var values = new float[count];
        for (var index = 0; index < count; index++)
            values[index] = reader.ReadSingleBigEndian();
        return values;
    }

    private static void SkipBigEndianFloatCurve(ref Reader reader, string label)
    {
        var count = reader.ReadCountBigEndian(MaximumCurveValues, label);
        reader.SkipChecked(count, 4);
    }

    private static void SkipLittleEndianFloatCurve(ref Reader reader, string label)
    {
        var count = reader.ReadCountBigEndian(MaximumCurveValues, label);
        reader.SkipChecked(count, 4);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly int _end;

        public Reader(byte[] data, int position, int end)
        {
            if (position < 0 || end < position || end > data.Length)
                throw new InvalidDataException("Invalid ParticleEffect reader bounds.");
            _data = data;
            Position = position;
            _end = end;
        }

        public int Position { get; private set; }
        public int Remaining => _end - Position;

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _data[Position++];
        }

        public ushort ReadUInt16BigEndian()
        {
            EnsureAvailable(2);
            var value = BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(Position, 2));
            Position += 2;
            return value;
        }

        public uint ReadUInt32BigEndian()
        {
            EnsureAvailable(4);
            var value = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(Position, 4));
            Position += 4;
            return value;
        }

        public ulong ReadUInt64BigEndian()
        {
            EnsureAvailable(8);
            var value = BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(Position, 8));
            Position += 8;
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

        public ushort PeekUInt16BigEndian()
        {
            EnsureAvailable(2);
            return BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(Position, 2));
        }

        public void SkipAsciiZString(int maximumLength)
        {
            var start = Position;
            while (Position < _end && Position - start <= maximumLength)
            {
                if (_data[Position++] == 0)
                    return;
            }
            throw new InvalidDataException("ParticleEffect string is missing its terminator or exceeds the supported length.");
        }

        public void Skip(int count)
        {
            if (count < 0)
                throw new InvalidDataException("Negative ParticleEffect skip length.");
            EnsureAvailable(count);
            Position += count;
        }

        public void SkipChecked(int itemCount, int itemSize)
        {
            Skip(checked(itemCount * itemSize));
        }

        private void EnsureAvailable(int count)
        {
            if (count < 0 || Position > _end - count)
                throw new InvalidDataException("The Swarm VFX resource ended unexpectedly while decoding ParticleEffect data.");
        }
    }
}
