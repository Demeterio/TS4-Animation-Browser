using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TS4AnimationBrowser.App;

public sealed class VfxParticlePreview
{
    private const int MaxParticlesPerEmitter = 32;
    private const int GlobalParticleBudget = 768;
    private const double MinimumRadius = 0.008;
    private const double MaximumRadius = 0.22;
    private const int FramingSeedSamples = 16;
    private const int WarmUpSamples = 12;
    private const double StateEpsilon = 0.000001;

    private static readonly Transform3D HiddenTransform = CreateHiddenTransform();

    private readonly ModelVisual3D _visual = new();
    private readonly Model3DGroup _group = new();
    private readonly List<EmitterPreview> _emitters = [];
    private readonly Dictionary<ParticleMaterialKey, Material> _materialCache = [];
    private readonly PerspectiveCamera _camera;

    private double _lastTimeSeconds = double.NaN;
    private Vector3D _lastCameraRight;
    private Vector3D _lastCameraUp;
    private bool _hasCameraAxes;

    public VfxParticlePreview(Viewport3D viewport, PerspectiveCamera camera)
    {
        _camera = camera;
        _visual.Content = _group;
        viewport.Children.Add(_visual);
    }

    public bool HasEffect => _emitters.Count > 0;
    public bool HasResolvedTextures => _emitters.Any(emitter => emitter.Texture is not null);
    public PreviewBounds? FramingBounds { get; private set; }
    public double PreferredFrontYaw { get; private set; } = Math.PI;

    public void Clear()
    {
        _emitters.Clear();
        _group.Children.Clear();
        _materialCache.Clear();
        _lastTimeSeconds = double.NaN;
        _hasCameraAxes = false;
        FramingBounds = null;
        PreferredFrontYaw = Math.PI;
    }

    public void Load(VfxLoadResult result)
    {
        Clear();
        if (result.ParticleEffects.Count == 0)
            return;

        var particleBlocks = result.Blocks
            .Where(block => block.BlockType == VfxSwarmBlockType.ParticleEffect)
            .ToArray();
        if (particleBlocks.Length != result.ParticleEffects.Count)
            return;

        var slotsPerEmitter = Math.Max(1, Math.Min(MaxParticlesPerEmitter, GlobalParticleBudget / Math.Max(1, particleBlocks.Length)));
        for (var index = 0; index < particleBlocks.Length; index++)
        {
            var particle = result.ParticleEffects[index];
            result.Textures.TryGetValue(particle.TextureInstance, out var texture);
            var emitter = new EmitterPreview(
                particleBlocks[index],
                particle,
                texture,
                result.VisualEffectFlags,
                result.VisualEffectSeed,
                slotsPerEmitter);
            _emitters.Add(emitter);
            foreach (var slot in emitter.Slots)
                _group.Children.Add(slot.Model);
        }

        ResolvePreferredFrontYaw();
        FramingBounds = CalculateFramingBounds();
        Update(0, force: true);
    }

    public void WarmUp(double durationSeconds)
    {
        if (_emitters.Count == 0)
            return;

        var duration = Math.Max(0, durationSeconds);
        for (var sample = 0; sample <= WarmUpSamples; sample++)
        {
            var time = duration <= 0 ? 0 : duration * sample / WarmUpSamples;
            Update(time, force: true);
        }

        Update(0, force: true);
    }

    public void Update(double timeSeconds) => Update(timeSeconds, force: false);

    private void Update(double timeSeconds, bool force)
    {
        if (_emitters.Count == 0)
            return;

        var time = Math.Max(0, timeSeconds);
        var cameraAxes = GetCameraFacingAxes();
        var simulationChanged = force || !double.IsFinite(_lastTimeSeconds) || Math.Abs(time - _lastTimeSeconds) > StateEpsilon;
        var cameraChanged = force
            || !_hasCameraAxes
            || !NearlyEqual(cameraAxes.Right, _lastCameraRight)
            || !NearlyEqual(cameraAxes.Up, _lastCameraUp);

        if (!simulationChanged && !cameraChanged)
            return;

        foreach (var emitter in _emitters)
        {
            if (simulationChanged)
                UpdateEmitterState(emitter, time);

            if (simulationChanged || cameraChanged)
                ApplyEmitterGeometry(emitter, cameraAxes.Right, cameraAxes.Up);
        }

        _lastTimeSeconds = time;
        _lastCameraRight = cameraAxes.Right;
        _lastCameraUp = cameraAxes.Up;
        _hasCameraAxes = true;
    }

    private void ResolvePreferredFrontYaw()
    {
        foreach (var emitter in _emitters)
        {
            var forward = ApplyBlockRotation(emitter, new Vector3D(0, 0, 1));
            forward.Y = 0;
            if (!double.IsFinite(forward.X) || !double.IsFinite(forward.Z) || forward.LengthSquared < StateEpsilon)
                continue;

            forward.Normalize();
            PreferredFrontYaw = NormalizeAngle(Math.Atan2(forward.X, forward.Z) + Math.PI);
            return;
        }
    }

    private (Vector3D Right, Vector3D Up) GetCameraFacingAxes()
    {
        var look = _camera.LookDirection;
        if (look.LengthSquared < StateEpsilon)
            look = new Vector3D(0, 0, -1);
        look.Normalize();

        var right = Vector3D.CrossProduct(look, _camera.UpDirection);
        if (right.LengthSquared < StateEpsilon)
            right = new Vector3D(1, 0, 0);
        else
            right.Normalize();

        var up = Vector3D.CrossProduct(right, look);
        if (up.LengthSquared < StateEpsilon)
            up = new Vector3D(0, 1, 0);
        else
            up.Normalize();
        return (right, up);
    }

    private static (Vector3D Right, Vector3D Up) GetEmitterAxes(
        EmitterPreview emitter,
        Vector3D cameraRight,
        Vector3D cameraUp)
    {
        var alignment = VfxSwarmSemantics.GetAlignment(emitter.Effect.AlignMode);
        if (alignment == VfxParticleAlignment.Camera)
            return (cameraRight, cameraUp);

        if (alignment == VfxParticleAlignment.Ground)
            return (new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));

        // Source/dir/pole modes are not camera billboards. Most importantly, sunPole (7)
        // must not erase the orientation carried by each VisualEffect block. Until the native
        // pole formula is recovered, preserve the source transform rather than inventing one.
        var right = ApplyBlockRotation(emitter, new Vector3D(1, 0, 0));
        var up = ApplyBlockRotation(emitter, new Vector3D(0, 1, 0));
        if (right.LengthSquared < StateEpsilon || up.LengthSquared < StateEpsilon)
            return (cameraRight, cameraUp);

        right.Normalize();
        up -= right * Vector3D.DotProduct(up, right);
        if (up.LengthSquared < StateEpsilon)
        {
            var forward = ApplyBlockRotation(emitter, new Vector3D(0, 0, 1));
            if (forward.LengthSquared < StateEpsilon)
                return (cameraRight, cameraUp);
            forward.Normalize();
            up = Vector3D.CrossProduct(forward, right);
        }

        if (up.LengthSquared < StateEpsilon)
            return (cameraRight, cameraUp);
        up.Normalize();
        return (right, up);
    }

    private PreviewBounds? CalculateFramingBounds()
    {
        if (_emitters.Count == 0)
            return null;

        var minimum = new Point3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var maximum = new Point3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        var largestRadius = MinimumRadius;

        foreach (var emitter in _emitters)
        {
            largestRadius = Math.Max(largestRadius, CalculateMaximumParticleRadius(emitter));
            IncludePoint(ref minimum, ref maximum, GetBlockOrigin(emitter));

            for (var sample = 0; sample < FramingSeedSamples; sample++)
            {
                var seed = Hash(emitter.Seed, (uint)sample);
                for (var ageStep = 0; ageStep <= 4; ageStep++)
                {
                    var age = emitter.Lifetime * ageStep / 4.0;
                    IncludePoint(ref minimum, ref maximum, EvaluatePosition(emitter, seed, age));
                }
            }
        }

        if (!IsFinite(minimum) || !IsFinite(maximum))
            return null;

        var margin = Math.Clamp(largestRadius * 2.5 + 0.04, 0.08, 0.6);
        minimum = new Point3D(minimum.X - margin, minimum.Y - margin, minimum.Z - margin);
        maximum = new Point3D(maximum.X + margin, maximum.Y + margin, maximum.Z + margin);
        EnsureMinimumExtent(ref minimum, ref maximum, 0.35);
        return new PreviewBounds(minimum, maximum);
    }

    private static double CalculateMaximumParticleRadius(EmitterPreview emitter)
    {
        var largestSize = emitter.Effect.SizeCurve
            .Where(float.IsFinite)
            .Select(value => Math.Abs((double)value))
            .DefaultIfEmpty(0.05)
            .Max();
        var variation = 1.0 + Math.Abs(Math.Clamp(emitter.Effect.SizeVary, -0.95f, 4f));
        var scaledSize = largestSize * variation * GetBlockScale(emitter);
        return Math.Clamp(scaledSize * 0.5, MinimumRadius, MaximumRadius);
    }

    private static void IncludePoint(ref Point3D minimum, ref Point3D maximum, Point3D point)
    {
        if (!IsFinite(point))
            return;

        minimum = new Point3D(
            Math.Min(minimum.X, point.X),
            Math.Min(minimum.Y, point.Y),
            Math.Min(minimum.Z, point.Z));
        maximum = new Point3D(
            Math.Max(maximum.X, point.X),
            Math.Max(maximum.Y, point.Y),
            Math.Max(maximum.Z, point.Z));
    }

    private static void EnsureMinimumExtent(ref Point3D minimum, ref Point3D maximum, double minimumExtent)
    {
        var center = new Point3D(
            (minimum.X + maximum.X) * 0.5,
            (minimum.Y + maximum.Y) * 0.5,
            (minimum.Z + maximum.Z) * 0.5);
        var halfX = Math.Max(minimumExtent * 0.5, (maximum.X - minimum.X) * 0.5);
        var halfY = Math.Max(minimumExtent * 0.5, (maximum.Y - minimum.Y) * 0.5);
        var halfZ = Math.Max(minimumExtent * 0.5, (maximum.Z - minimum.Z) * 0.5);
        minimum = new Point3D(center.X - halfX, center.Y - halfY, center.Z - halfZ);
        maximum = new Point3D(center.X + halfX, center.Y + halfY, center.Z + halfZ);
    }

    private static bool IsFinite(Point3D point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private void UpdateEmitterState(EmitterPreview emitter, double timeSeconds)
    {
        var effect = emitter.Effect;
        var localTime = timeSeconds * emitter.TimeScale;
        var newestSpawn = localTime < emitter.Delay ? -1 : Math.Floor((localTime - emitter.Delay) / emitter.Interval);

        for (var slotIndex = 0; slotIndex < emitter.Slots.Count; slotIndex++)
        {
            var slot = emitter.Slots[slotIndex];
            if (slotIndex >= emitter.ActiveWindow || newestSpawn < 0)
            {
                SetSlotVisible(slot, false);
                continue;
            }

            var spawnIndex = newestSpawn - slotIndex;
            if (spawnIndex < 0)
            {
                SetSlotVisible(slot, false);
                continue;
            }

            var spawnTime = emitter.Delay + spawnIndex * emitter.Interval;
            var age = localTime - spawnTime;
            if (age < 0 || age > emitter.Lifetime)
            {
                SetSlotVisible(slot, false);
                continue;
            }

            SetSlotVisible(slot, true);
            var normalizedAge = Math.Clamp(age / emitter.Lifetime, 0, 1);
            var seed = Hash(emitter.Seed, (uint)spawnIndex);
            slot.Center = EvaluatePosition(emitter, seed, age);

            var size = EvaluateCurve(effect.SizeCurve, normalizedAge, 0.05) * GetBlockScale(emitter);
            size *= 1.0 + SignedUnit(seed ^ 0x52A2C2A1u) * Math.Clamp(effect.SizeVary, -0.95f, 4f);
            var radius = Math.Clamp(Math.Abs(size) * 0.5, MinimumRadius, MaximumRadius);

            var aspect = Math.Abs(EvaluateCurve(effect.AspectRatioCurve, normalizedAge, 1));
            aspect *= 1.0 + SignedUnit(seed ^ 0x147A3C91u) * Math.Clamp(effect.AspectRatioVary, -0.95f, 4f);
            aspect = Math.Clamp(aspect, 0.15, 6.0);
            slot.HalfWidth = radius * aspect;
            slot.HalfHeight = radius;

            var alpha = EvaluateCurve(effect.AlphaCurve, normalizedAge, 1);
            alpha *= 1.0 + SignedUnit(seed ^ 0x891C4F2Du) * Math.Clamp(effect.AlphaVary, -0.95f, 4f);
            alpha = Math.Clamp(alpha, 0, 1);

            var color = ApplyColorVariation(EvaluateColor(effect.ColorCurve, normalizedAge), effect, seed);
            var material = GetParticleMaterial(emitter.Texture, color, alpha, effect.DrawMode);
            if (!ReferenceEquals(slot.Model.Material, material))
            {
                slot.Model.Material = material;
                slot.Model.BackMaterial = material;
            }

            slot.Rotation = effect.RotationOffset
                + EvaluateCurve(effect.RotationCurve, normalizedAge, 0)
                + SignedUnit(seed ^ 0x77A5942Bu) * effect.RotationVary;

            UpdateTextureCoordinates(slot, effect, age, seed);
        }
    }

    private static void ApplyEmitterGeometry(EmitterPreview emitter, Vector3D cameraRight, Vector3D cameraUp)
    {
        var axes = GetEmitterAxes(emitter, cameraRight, cameraUp);
        foreach (var slot in emitter.Slots)
        {
            if (!slot.IsVisible)
                continue;

            var rotatedAxes = RotateBillboardAxes(axes.Right, axes.Up, slot.Rotation);
            UpdateBillboard(slot.Mesh, slot.Center, rotatedAxes.Right, rotatedAxes.Up, slot.HalfWidth, slot.HalfHeight);
        }
    }

    private static void SetSlotVisible(ParticleSlot slot, bool visible)
    {
        if (slot.IsVisible == visible)
            return;

        slot.IsVisible = visible;
        slot.Model.Transform = visible ? Transform3D.Identity : HiddenTransform;
    }

    private static (Vector3D Right, Vector3D Up) RotateBillboardAxes(Vector3D right, Vector3D up, double radians)
    {
        if (!double.IsFinite(radians) || Math.Abs(radians) < StateEpsilon)
            return (right, up);

        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return (right * cosine + up * sine, up * cosine - right * sine);
    }

    private static void UpdateBillboard(
        MeshGeometry3D mesh,
        Point3D center,
        Vector3D right,
        Vector3D up,
        double halfWidth,
        double halfHeight)
    {
        var horizontal = right * halfWidth;
        var vertical = up * halfHeight;
        var positions = mesh.Positions;
        positions[0] = center - horizontal - vertical;
        positions[1] = center + horizontal - vertical;
        positions[2] = center + horizontal + vertical;
        positions[3] = center - horizontal + vertical;
    }

    private static void UpdateTextureCoordinates(ParticleSlot slot, VfxSwarmParticleEffect effect, double age, uint seed)
    {
        var tilesU = Math.Max(1, (int)effect.TileCountU);
        var tilesV = Math.Max(1, (int)effect.TileCountV);
        var totalTiles = Math.Max(1, tilesU * tilesV);
        var availableFrames = effect.FrameCount > 0
            ? Math.Min((int)effect.FrameCount, totalTiles)
            : totalTiles;
        var start = Math.Min((int)effect.FrameStart, totalTiles - 1);
        var randomOffset = effect.FrameRandom > 0
            ? (int)(seed % (uint)Math.Max(1, availableFrames))
            : 0;
        var animatedOffset = effect.FrameSpeed > 0
            ? (int)Math.Floor(age * effect.FrameSpeed)
            : 0;
        var frame = (start + randomOffset + animatedOffset) % totalTiles;
        if (slot.TextureFrame == frame)
            return;

        slot.TextureFrame = frame;
        var column = frame % tilesU;
        var row = frame / tilesU;
        var u0 = column / (double)tilesU;
        var u1 = (column + 1) / (double)tilesU;
        var v0 = row / (double)tilesV;
        var v1 = (row + 1) / (double)tilesV;
        var uv = slot.Mesh.TextureCoordinates;
        uv[0] = new Point(u0, v1);
        uv[1] = new Point(u1, v1);
        uv[2] = new Point(u1, v0);
        uv[3] = new Point(u0, v0);
    }

    private static Point3D EvaluatePosition(EmitterPreview emitter, uint seed, double age)
    {
        var effect = emitter.Effect;
        var localPosition = new Vector3D(
            Lerp(effect.EmitVolumeMinX, effect.EmitVolumeMaxX, Unit(seed ^ 0xA1B2C3D4u)),
            Lerp(effect.EmitVolumeMinY, effect.EmitVolumeMaxY, Unit(seed ^ 0xB2C3D4E5u)),
            Lerp(effect.EmitVolumeMinZ, effect.EmitVolumeMaxZ, Unit(seed ^ 0xC3D4E5F6u)));

        var direction = new Vector3D(
            Lerp(effect.EmitDirectionMinX, effect.EmitDirectionMaxX, Unit(seed ^ 0x13579BDFu)),
            Lerp(effect.EmitDirectionMinY, effect.EmitDirectionMaxY, Unit(seed ^ 0x2468ACE0u)),
            Lerp(effect.EmitDirectionMinZ, effect.EmitDirectionMaxZ, Unit(seed ^ 0x31415926u)));
        if (direction.LengthSquared > StateEpsilon)
            direction.Normalize();

        var speed = Lerp(effect.EmitSpeedMin, effect.EmitSpeedMax, Unit(seed ^ 0x27182818u));
        var velocity = direction * speed;
        var acceleration = new Vector3D(
            effect.DirectionalForceX,
            effect.DirectionalForceY - effect.GravityStrength,
            effect.DirectionalForceZ);

        var drag = Math.Max(0, effect.Drag);
        var damp = drag > 0.0001 ? (1.0 - Math.Exp(-drag * age)) / drag : age;
        localPosition += velocity * damp + acceleration * (0.5 * age * age);
        localPosition *= GetBlockScale(emitter);
        localPosition = ApplyBlockRotation(emitter, localPosition);

        var origin = GetBlockOrigin(emitter);
        return new Point3D(
            origin.X + localPosition.X,
            origin.Y + localPosition.Y,
            origin.Z + localPosition.Z);
    }

    private static double GetBlockScale(EmitterPreview emitter)
    {
        if ((emitter.VisualEffectFlags & VfxSwarmSemantics.VisualIgnoreScale) != 0)
            return 1.0;
        if (!VfxSwarmSemantics.UsesScale(emitter.Block))
            return 1.0;
        return Math.Max(0.01f, emitter.Block.LocalScale);
    }

    private static Point3D GetBlockOrigin(EmitterPreview emitter)
    {
        if (!VfxSwarmSemantics.UsesOffset(emitter.Block))
            return new Point3D(0, 0, 0);
        return new Point3D(emitter.Block.PositionX, emitter.Block.PositionY, emitter.Block.PositionZ);
    }

    private static Vector3D ApplyBlockRotation(EmitterPreview emitter, Vector3D value)
    {
        if ((emitter.VisualEffectFlags & VfxSwarmSemantics.VisualIgnoreOrientation) != 0)
            return value;
        if (!VfxSwarmSemantics.UsesRotation(emitter.Block))
            return value;
        return TransformDirection(emitter.Block, value);
    }

    private static Vector3D TransformDirection(VfxSwarmVisualBlock block, Vector3D value)
        => new(
            block.Orientation11 * value.X + block.Orientation12 * value.Y + block.Orientation13 * value.Z,
            block.Orientation21 * value.X + block.Orientation22 * value.Y + block.Orientation23 * value.Z,
            block.Orientation31 * value.X + block.Orientation32 * value.Y + block.Orientation33 * value.Z);

    private static Color EvaluateColor(IReadOnlyList<VfxParticleColor> curve, double normalizedAge)
    {
        if (curve.Count == 0)
            return Colors.White;

        var scaled = normalizedAge * Math.Max(0, curve.Count - 1);
        var lower = Math.Clamp((int)Math.Floor(scaled), 0, curve.Count - 1);
        var upper = Math.Clamp(lower + 1, 0, curve.Count - 1);
        var fraction = scaled - lower;
        var a = curve[lower];
        var b = curve[upper];
        return Color.FromRgb(
            ToByte(Lerp(a.Red, b.Red, fraction)),
            ToByte(Lerp(a.Green, b.Green, fraction)),
            ToByte(Lerp(a.Blue, b.Blue, fraction)));
    }

    private static Color ApplyColorVariation(Color color, VfxSwarmParticleEffect effect, uint seed)
    {
        var red = color.R / 255.0 + SignedUnit(seed ^ 0xA762C543u) * effect.ColorVaryRed;
        var green = color.G / 255.0 + SignedUnit(seed ^ 0x1BC54D29u) * effect.ColorVaryGreen;
        var blue = color.B / 255.0 + SignedUnit(seed ^ 0x9D3821F7u) * effect.ColorVaryBlue;
        return Color.FromRgb(ToByte(red), ToByte(green), ToByte(blue));
    }

    private Material GetParticleMaterial(VfxTextureAsset? texture, Color color, double alpha, byte drawMode)
    {
        var alphaByte = (byte)Math.Clamp((int)Math.Round(Math.Clamp(alpha, 0, 1) * 255), 0, 255);
        var key = new ParticleMaterialKey(texture?.Instance ?? 0, color.R, color.G, color.B, alphaByte, drawMode);
        if (_materialCache.TryGetValue(key, out var cached))
            return cached;

        var opacity = alphaByte / 255.0;
        var display = Color.FromArgb(alphaByte, color.R, color.G, color.B);
        Material material;

        if (texture is not null)
        {
            Brush brush;
            if (color.R >= 250 && color.G >= 250 && color.B >= 250)
            {
                brush = new ImageBrush(texture.Image)
                {
                    Stretch = Stretch.Fill,
                    Opacity = opacity
                };
            }
            else
            {
                var mask = new ImageBrush(texture.Image) { Stretch = Stretch.Fill };
                var drawing = new DrawingGroup
                {
                    Opacity = opacity,
                    OpacityMask = mask
                };
                drawing.Children.Add(new GeometryDrawing(
                    new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                    null,
                    new RectangleGeometry(new Rect(0, 0, 1, 1))));
                brush = new DrawingBrush(drawing)
                {
                    Stretch = Stretch.Fill,
                    Viewbox = new Rect(0, 0, 1, 1),
                    ViewboxUnits = BrushMappingMode.Absolute
                };
            }

            material = VfxSwarmSemantics.IsAdditive(drawMode)
                ? new EmissiveMaterial(brush)
                : new DiffuseMaterial(brush);
        }
        else
        {
            var fallbackBrush = new SolidColorBrush(display);
            material = VfxSwarmSemantics.IsAdditive(drawMode)
                ? new EmissiveMaterial(fallbackBrush)
                : new DiffuseMaterial(fallbackBrush);
        }

        _materialCache[key] = material;
        return material;
    }

    private static double EvaluateCurve(IReadOnlyList<float> curve, double normalizedTime, double fallback)
    {
        if (curve.Count == 0)
            return fallback;
        if (curve.Count == 1)
            return float.IsFinite(curve[0]) ? curve[0] : fallback;

        var scaled = Math.Clamp(normalizedTime, 0, 1) * (curve.Count - 1);
        var lower = Math.Clamp((int)Math.Floor(scaled), 0, curve.Count - 1);
        var upper = Math.Clamp(lower + 1, 0, curve.Count - 1);
        var fraction = scaled - lower;
        var value = Lerp(curve[lower], curve[upper], fraction);
        return double.IsFinite(value) ? value : fallback;
    }

    private static double AveragePositive(float a, float b, double fallback)
    {
        var average = AverageFinite(a, b);
        return average > 0 ? average : fallback;
    }

    private static double AverageFinite(float a, float b)
    {
        if (float.IsFinite(a) && float.IsFinite(b))
            return (a + b) * 0.5;
        if (float.IsFinite(a))
            return a;
        if (float.IsFinite(b))
            return b;
        return 0;
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
            angle -= Math.PI * 2;
        while (angle <= -Math.PI)
            angle += Math.PI * 2;
        return angle;
    }

    private static bool NearlyEqual(Vector3D a, Vector3D b)
        => Math.Abs(a.X - b.X) <= StateEpsilon
           && Math.Abs(a.Y - b.Y) <= StateEpsilon
           && Math.Abs(a.Z - b.Z) <= StateEpsilon;

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(Math.Clamp(value, 0, 1) * 255), 0, 255);
    private static double Unit(uint value) => (value & 0x00FFFFFFu) / 16777215.0;
    private static double SignedUnit(uint value) => Unit(value) * 2 - 1;

    private static uint Hash(uint seed, uint value)
    {
        var hash = seed ^ (value + 0x9E3779B9u + (seed << 6) + (seed >> 2));
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        return hash ^ (hash >> 16);
    }

    private static Transform3D CreateHiddenTransform()
    {
        var transform = new ScaleTransform3D(0, 0, 0);
        transform.Freeze();
        return transform;
    }

    private sealed class EmitterPreview
    {
        public EmitterPreview(
            VfxSwarmVisualBlock block,
            VfxSwarmParticleEffect effect,
            VfxTextureAsset? texture,
            uint visualEffectFlags,
            uint visualEffectSeed,
            int slotLimit)
        {
            Block = block;
            Effect = effect;
            Texture = texture;
            VisualEffectFlags = visualEffectFlags;
            Seed = visualEffectSeed;
            Lifetime = Math.Max(0.01, AveragePositive(effect.ParticleLifetimeMin, effect.ParticleLifetimeMax, 0.5));
            Delay = Math.Max(0, AverageFinite(effect.EmitDelayMin, effect.EmitDelayMax));
            Rate = Math.Clamp(EvaluateCurve(effect.EmitRateCurve, 0.5, 8), 0.5, 120);
            Interval = 1.0 / Rate;
            TimeScale = float.IsFinite(block.TimeScale) && block.TimeScale > 0 ? block.TimeScale : 1.0;
            ActiveWindow = Math.Min(slotLimit, Math.Max(1, (int)Math.Ceiling(Lifetime / Interval) + 2));

            for (var slot = 0; slot < ActiveWindow; slot++)
                Slots.Add(CreateSlot());
        }

        public VfxSwarmVisualBlock Block { get; }
        public VfxSwarmParticleEffect Effect { get; }
        public VfxTextureAsset? Texture { get; }
        public uint VisualEffectFlags { get; }
        public uint Seed { get; }
        public double Lifetime { get; }
        public double Delay { get; }
        public double Rate { get; }
        public double Interval { get; }
        public double TimeScale { get; }
        public int ActiveWindow { get; }
        public List<ParticleSlot> Slots { get; } = [];

        private static ParticleSlot CreateSlot()
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    new(-0.5, -0.5, 0),
                    new(0.5, -0.5, 0),
                    new(0.5, 0.5, 0),
                    new(-0.5, 0.5, 0)
                },
                TriangleIndices = new Int32Collection { 0, 2, 1, 0, 3, 2 },
                TextureCoordinates = new PointCollection
                {
                    new(0, 1), new(1, 1), new(1, 0), new(0, 0)
                }
            };
            var material = new DiffuseMaterial(Brushes.Transparent);
            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material,
                Transform = HiddenTransform
            };
            return new ParticleSlot(mesh, model);
        }
    }

    private sealed class ParticleSlot
    {
        public ParticleSlot(MeshGeometry3D mesh, GeometryModel3D model)
        {
            Mesh = mesh;
            Model = model;
        }

        public MeshGeometry3D Mesh { get; }
        public GeometryModel3D Model { get; }
        public bool IsVisible { get; set; }
        public Point3D Center { get; set; }
        public double HalfWidth { get; set; }
        public double HalfHeight { get; set; }
        public double Rotation { get; set; }
        public int TextureFrame { get; set; } = -1;
    }

    private readonly record struct ParticleMaterialKey(
        ulong TextureInstance,
        byte Red,
        byte Green,
        byte Blue,
        byte Alpha,
        byte DrawMode);
}
