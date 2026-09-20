using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Wpf;

namespace TS4AnimationBrowser.App;

public sealed class VfxDirectXPreview : IDisposable
{
    private const int MaxParticlesPerEmitter = 32;
    private const int GlobalParticleBudget = 768;
    private const int MaxVertices = GlobalParticleBudget * 6;
    private const double MinimumRadius = 0.008;
    private const double MaximumRadius = 0.22;
    private const int FramingSeedSamples = 16;
    private const int WarmUpSamples = 12;
    private const double StateEpsilon = 0.000001;

    private const string ShaderSource = """
cbuffer CameraBuffer : register(b0)
{
    row_major float4x4 ViewProjection;
};

Texture2D ParticleTexture : register(t0);
SamplerState ParticleSampler : register(s0);

struct VSInput
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0), ViewProjection);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_Target
{
    return ParticleTexture.Sample(ParticleSampler, input.TexCoord) * input.Color;
}
""";

    private readonly DrawingSurface _surface;
    private readonly Grid _host;
    private readonly PerspectiveCamera _camera;
    private readonly List<EmitterPreview> _emitters = [];
    private readonly List<ParticleVertex> _frameVertices = new(MaxVertices);
    private readonly List<DrawBatch> _drawBatches = [];
    private readonly Dictionary<ulong, GpuTexture> _gpuTextures = [];

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _particleVertexBuffer;
    private ID3D11Buffer? _cameraBuffer;
    private ID3D11Buffer? _gridVertexBuffer;
    private ID3D11SamplerState? _samplerState;
    private ID3D11BlendState? _alphaBlendState;
    private ID3D11BlendState? _additiveBlendState;
    private ID3D11DepthStencilState? _depthReadState;
    private ID3D11DepthStencilState? _depthWriteState;
    private ID3D11DepthStencilState? _depthDisabledState;
    private ID3D11RasterizerState? _rasterizerState;
    private GpuTexture? _whiteTexture;
    private int _gridVertexCount;
    private bool _gpuReady;
    private bool _disposed;

    private double _lastTimeSeconds = double.NaN;
    private Vector3D _lastCameraRight;
    private Vector3D _lastCameraUp;
    private bool _hasCameraAxes;
    private Color _backgroundColor = Color.FromRgb(244, 244, 244);
    private bool _gridVisible = true;

    public VfxDirectXPreview(Viewport3D viewport, PerspectiveCamera camera)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewport.Parent is not Grid host)
            throw new InvalidOperationException("The animation viewport must be hosted by a Grid to attach the Direct3D VFX surface.");

        _host = host;
        _camera = camera;
        _surface = new DrawingSurface
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Focusable = false,
            AlwaysRefresh = false,
            DepthStencilFormat = Format.D32_Float
        };
        _surface.LoadContent += Surface_LoadContent;
        _surface.Draw += Surface_Draw;
        _surface.UnloadContent += Surface_UnloadContent;

        var viewportIndex = host.Children.IndexOf(viewport);
        host.Children.Insert(Math.Max(0, viewportIndex + 1), _surface);
    }

    public bool HasEffect => _emitters.Count > 0;
    public bool HasResolvedTextures => _emitters.Any(emitter => emitter.Texture is not null);
    public PreviewBounds? FramingBounds { get; private set; }
    public double PreferredFrontYaw { get; private set; } = Math.PI;

    public void SetBackgroundColor(Color color)
    {
        _backgroundColor = color;
        _surface.Invalidate();
    }

    public void SetGridVisible(bool visible)
    {
        _gridVisible = visible;
        _surface.Invalidate();
    }

    public void Clear()
    {
        _emitters.Clear();
        _frameVertices.Clear();
        _drawBatches.Clear();
        _lastTimeSeconds = double.NaN;
        _hasCameraAxes = false;
        FramingBounds = null;
        PreferredFrontYaw = Math.PI;
        DisposeEffectTextures();
        _surface.AlwaysRefresh = false;
        _surface.Visibility = Visibility.Collapsed;
    }

    public void Load(VfxLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
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
            _emitters.Add(new EmitterPreview(
                particleBlocks[index],
                particle,
                texture,
                result.VisualEffectFlags,
                result.VisualEffectSeed,
                slotsPerEmitter));
        }

        ResolvePreferredFrontYaw();
        FramingBounds = CalculateFramingBounds();
        RebuildEffectTextures();
        Update(0, force: true);
        _surface.Visibility = Visibility.Visible;
        _surface.AlwaysRefresh = true;
        _surface.Invalidate();
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

        if (simulationChanged)
        {
            foreach (var emitter in _emitters)
                UpdateEmitterState(emitter, time);
        }

        if (simulationChanged || cameraChanged)
            BuildFrameGeometry(cameraAxes.Right, cameraAxes.Up);

        _lastTimeSeconds = time;
        _lastCameraRight = cameraAxes.Right;
        _lastCameraUp = cameraAxes.Up;
        _hasCameraAxes = true;
        _surface.Invalidate();
    }

    private void Surface_LoadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        DisposeGpuResources();
        _device = e.Device;
        _context = e.Context;

        var vertexShaderByteCode = Compiler.Compile(ShaderSource, "VSMain", "TS4VfxDirectX.hlsl", "vs_4_0");
        var pixelShaderByteCode = Compiler.Compile(ShaderSource, "PSMain", "TS4VfxDirectX.hlsl", "ps_4_0");

        _vertexShader = e.Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = e.Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = e.Device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 20, 0)
        ], vertexShaderByteCode.Span);

        _particleVertexBuffer = e.Device.CreateBuffer(
            (uint)(ParticleVertex.SizeInBytes * MaxVertices),
            BindFlags.VertexBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write);
        _cameraBuffer = e.Device.CreateConstantBuffer<CameraConstants>();
        _samplerState = e.Device.CreateSamplerState(SamplerDescription.LinearClamp);
        _alphaBlendState = e.Device.CreateBlendState(BlendDescription.NonPremultiplied);
        _additiveBlendState = e.Device.CreateBlendState(BlendDescription.Additive);
        _depthReadState = e.Device.CreateDepthStencilState(DepthStencilDescription.DepthRead);
        _depthWriteState = e.Device.CreateDepthStencilState(DepthStencilDescription.Default);
        _depthDisabledState = e.Device.CreateDepthStencilState(DepthStencilDescription.None);
        _rasterizerState = e.Device.CreateRasterizerState(RasterizerDescription.CullNone);

        var gridVertices = CreateGridVertices();
        _gridVertexCount = gridVertices.Length;
        _gridVertexBuffer = e.Device.CreateBuffer(gridVertices, BindFlags.VertexBuffer);
        _whiteTexture = CreateGpuTexture(e.Device, CreateWhiteBitmap());
        _gpuReady = true;
        RebuildEffectTextures();
    }

    private void Surface_UnloadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        DisposeGpuResources();
        _context = null;
        _device = null;
    }

    private void Surface_Draw(object? sender, DrawEventArgs e)
    {
        var background = new Color4(
            _backgroundColor.R / 255f,
            _backgroundColor.G / 255f,
            _backgroundColor.B / 255f,
            1f);
        e.Context.ClearRenderTargetView(e.Surface.ColorTextureView!, background);
        if (e.Surface.DepthStencilView is not null)
            e.Context.ClearDepthStencilView(e.Surface.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        if (!_gpuReady || _vertexShader is null || _pixelShader is null || _inputLayout is null
            || _cameraBuffer is null || _particleVertexBuffer is null || _samplerState is null || _rasterizerState is null)
        {
            return;
        }

        UpdateCameraBuffer(e.Context, e.Surface.TextureWidth, e.Surface.TextureHeight);
        e.Context.IASetInputLayout(_inputLayout);
        e.Context.VSSetShader(_vertexShader);
        e.Context.VSSetConstantBuffer(0, _cameraBuffer);
        e.Context.PSSetShader(_pixelShader);
        e.Context.PSSetSampler(0, _samplerState);
        e.Context.RSSetState(_rasterizerState);

        if (_gridVisible && _gridVertexBuffer is not null && _whiteTexture is not null && _gridVertexCount > 0)
            DrawGrid(e.Context);

        if (_frameVertices.Count == 0)
            return;

        var mapped = e.Context.Map(_particleVertexBuffer, 0, MapMode.WriteDiscard);
        CollectionsMarshal.AsSpan(_frameVertices).CopyTo(mapped.AsSpan<ParticleVertex>(_frameVertices.Count));
        e.Context.Unmap(_particleVertexBuffer, 0);

        e.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        e.Context.IASetVertexBuffer(0, _particleVertexBuffer, ParticleVertex.SizeInBytes);

        foreach (var batch in _drawBatches)
        {
            if (batch.VertexCount <= 0)
                continue;

            e.Context.OMSetBlendState(batch.Additive ? _additiveBlendState : _alphaBlendState);
            e.Context.OMSetDepthStencilState(batch.IgnoreDepth ? _depthDisabledState : _depthReadState, 0);
            e.Context.PSSetShaderResource(0, ResolveGpuTexture(batch.TextureInstance));
            e.Context.Draw((uint)batch.VertexCount, (uint)batch.StartVertex);
        }

        e.Context.PSUnsetShaderResource(0);
    }

    private void DrawGrid(ID3D11DeviceContext context)
    {
        context.IASetPrimitiveTopology(PrimitiveTopology.LineList);
        context.IASetVertexBuffer(0, _gridVertexBuffer!, ParticleVertex.SizeInBytes);
        context.OMSetBlendState(_alphaBlendState);
        context.OMSetDepthStencilState(_depthWriteState, 0);
        context.PSSetShaderResource(0, _whiteTexture!.View);
        context.Draw((uint)_gridVertexCount, 0);
    }

    private void UpdateCameraBuffer(ID3D11DeviceContext context, int width, int height)
    {
        var position = new Vector3((float)_camera.Position.X, (float)_camera.Position.Y, (float)_camera.Position.Z);
        var look = new Vector3((float)_camera.LookDirection.X, (float)_camera.LookDirection.Y, (float)_camera.LookDirection.Z);
        if (look.LengthSquared() < 0.000001f)
            look = new Vector3(0, 0, -1);
        var up = new Vector3((float)_camera.UpDirection.X, (float)_camera.UpDirection.Y, (float)_camera.UpDirection.Z);
        if (up.LengthSquared() < 0.000001f)
            up = Vector3.UnitY;

        var view = Matrix4x4.CreateLookAt(position, position + look, Vector3.Normalize(up));
        var aspect = Math.Max(0.1f, width / (float)Math.Max(1, height));
        var horizontalFov = (float)(_camera.FieldOfView * Math.PI / 180.0);
        var verticalFov = 2f * MathF.Atan(MathF.Tan(horizontalFov * 0.5f) / aspect);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(verticalFov, aspect, 0.01f, 1000f);
        var constants = new CameraConstants { ViewProjection = view * projection };

        var mapped = context.Map(_cameraBuffer!, 0, MapMode.WriteDiscard);
        mapped.AsSpan<CameraConstants>(1)[0] = constants;
        context.Unmap(_cameraBuffer!, 0);
    }

    private void BuildFrameGeometry(Vector3D cameraRight, Vector3D cameraUp)
    {
        _frameVertices.Clear();
        _drawBatches.Clear();

        var cameraPosition = _camera.Position;
        var items = new List<RenderItem>();
        var sequence = 0;
        foreach (var emitter in _emitters)
        {
            var axes = GetEmitterAxes(emitter, cameraRight, cameraUp);
            foreach (var slot in emitter.Slots)
            {
                if (!slot.IsVisible)
                    continue;

                var baseMode = VfxSwarmSemantics.GetBaseDrawMode(emitter.Effect.DrawMode);
                if (baseMode == VfxBaseDrawMode.None)
                    continue;

                var rotated = RotateBillboardAxes(axes.Right, axes.Up, slot.Rotation);
                var toCamera = slot.Center - cameraPosition;
                items.Add(new RenderItem(
                    emitter,
                    slot,
                    rotated.Right,
                    rotated.Up,
                    toCamera.LengthSquared,
                    sequence++));
            }
        }

        items.Sort(static (left, right) =>
        {
            var layer = left.Emitter.Effect.Layer.CompareTo(right.Emitter.Effect.Layer);
            if (layer != 0)
                return layer;

            var sort = left.Emitter.Effect.SortOffset.CompareTo(right.Emitter.Effect.SortOffset);
            if (sort != 0)
                return sort;

            if (!VfxSwarmSemantics.IsAdditive(left.Emitter.Effect.DrawMode)
                || !VfxSwarmSemantics.IsAdditive(right.Emitter.Effect.DrawMode))
            {
                var distance = right.CameraDistanceSquared.CompareTo(left.CameraDistanceSquared);
                if (distance != 0)
                    return distance;
            }

            return left.Sequence.CompareTo(right.Sequence);
        });

        DrawBatch? currentBatch = null;
        foreach (var item in items)
        {
            if (_frameVertices.Count + 6 > MaxVertices)
                break;

            var effect = item.Emitter.Effect;
            var textureInstance = item.Emitter.Texture?.Instance ?? 0;
            var additive = VfxSwarmSemantics.IsAdditive(effect.DrawMode);
            var baseMode = VfxSwarmSemantics.GetBaseDrawMode(effect.DrawMode);
            var ignoreDepth = baseMode is VfxBaseDrawMode.DecalIgnoreDepth or VfxBaseDrawMode.AdditiveIgnoreDepth;
            var startVertex = _frameVertices.Count;
            AddQuadVertices(item);

            if (currentBatch is not null
                && currentBatch.TextureInstance == textureInstance
                && currentBatch.Additive == additive
                && currentBatch.IgnoreDepth == ignoreDepth)
            {
                currentBatch.VertexCount += 6;
            }
            else
            {
                currentBatch = new DrawBatch(textureInstance, additive, ignoreDepth, startVertex, 6);
                _drawBatches.Add(currentBatch);
            }
        }
    }

    private void AddQuadVertices(RenderItem item)
    {
        var slot = item.Slot;
        var horizontal = item.Right * slot.HalfWidth;
        var vertical = item.Up * slot.HalfHeight;
        var p0 = slot.Center - horizontal - vertical;
        var p1 = slot.Center + horizontal - vertical;
        var p2 = slot.Center + horizontal + vertical;
        var p3 = slot.Center - horizontal + vertical;
        var color = new Vector4(
            slot.Color.R / 255f,
            slot.Color.G / 255f,
            slot.Color.B / 255f,
            (float)slot.Alpha);

        var uv0 = new Vector2((float)slot.U0, (float)slot.V1);
        var uv1 = new Vector2((float)slot.U1, (float)slot.V1);
        var uv2 = new Vector2((float)slot.U1, (float)slot.V0);
        var uv3 = new Vector2((float)slot.U0, (float)slot.V0);

        var v0 = new ParticleVertex(ToVector3(p0), uv0, color);
        var v1 = new ParticleVertex(ToVector3(p1), uv1, color);
        var v2 = new ParticleVertex(ToVector3(p2), uv2, color);
        var v3 = new ParticleVertex(ToVector3(p3), uv3, color);
        _frameVertices.Add(v0);
        _frameVertices.Add(v2);
        _frameVertices.Add(v1);
        _frameVertices.Add(v0);
        _frameVertices.Add(v3);
        _frameVertices.Add(v2);
    }

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
                slot.IsVisible = false;
                continue;
            }

            var spawnIndex = newestSpawn - slotIndex;
            if (spawnIndex < 0)
            {
                slot.IsVisible = false;
                continue;
            }

            var spawnTime = emitter.Delay + spawnIndex * emitter.Interval;
            var age = localTime - spawnTime;
            if (age < 0 || age > emitter.Lifetime)
            {
                slot.IsVisible = false;
                continue;
            }

            slot.IsVisible = true;
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
            slot.Alpha = Math.Clamp(alpha, 0, 1);
            slot.Color = ApplyColorVariation(EvaluateColor(effect.ColorCurve, normalizedAge), effect, seed);
            slot.Rotation = effect.RotationOffset
                + EvaluateCurve(effect.RotationCurve, normalizedAge, 0)
                + SignedUnit(seed ^ 0x77A5942Bu) * effect.RotationVary;
            UpdateTextureCoordinates(slot, effect, age, seed);
        }
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

        // The exact native pole formula is still unknown. Preserve the VisualEffect block
        // orientation for source/dir/pole modes instead of collapsing them into billboards.
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
        return Math.Clamp(largestSize * variation * GetBlockScale(emitter) * 0.5, MinimumRadius, MaximumRadius);
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
        return new Point3D(origin.X + localPosition.X, origin.Y + localPosition.Y, origin.Z + localPosition.Z);
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

    private static (Vector3D Right, Vector3D Up) RotateBillboardAxes(Vector3D right, Vector3D up, double radians)
    {
        if (!double.IsFinite(radians) || Math.Abs(radians) < StateEpsilon)
            return (right, up);
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return (right * cosine + up * sine, up * cosine - right * sine);
    }

    private static void UpdateTextureCoordinates(ParticleSlot slot, VfxSwarmParticleEffect effect, double age, uint seed)
    {
        var tilesU = Math.Max(1, (int)effect.TileCountU);
        var tilesV = Math.Max(1, (int)effect.TileCountV);
        var totalTiles = Math.Max(1, tilesU * tilesV);
        var availableFrames = effect.FrameCount > 0 ? Math.Min((int)effect.FrameCount, totalTiles) : totalTiles;
        var start = Math.Min((int)effect.FrameStart, totalTiles - 1);
        var randomOffset = effect.FrameRandom > 0 ? (int)(seed % (uint)Math.Max(1, availableFrames)) : 0;
        var animatedOffset = effect.FrameSpeed > 0 ? (int)Math.Floor(age * effect.FrameSpeed) : 0;
        var frame = (start + randomOffset + animatedOffset) % totalTiles;
        if (slot.TextureFrame == frame)
            return;

        slot.TextureFrame = frame;
        var column = frame % tilesU;
        var row = frame / tilesU;
        slot.U0 = column / (double)tilesU;
        slot.U1 = (column + 1) / (double)tilesU;
        slot.V0 = row / (double)tilesV;
        slot.V1 = (row + 1) / (double)tilesV;
    }

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

    private void RebuildEffectTextures()
    {
        DisposeEffectTextures();
        if (!_gpuReady || _device is null)
            return;

        foreach (var texture in _emitters
                     .Select(emitter => emitter.Texture)
                     .Where(texture => texture is not null)
                     .Cast<VfxTextureAsset>()
                     .DistinctBy(texture => texture.Instance))
        {
            _gpuTextures[texture.Instance] = CreateGpuTexture(_device, texture.Image);
        }
    }

    private ID3D11ShaderResourceView ResolveGpuTexture(ulong instance)
    {
        if (instance != 0 && _gpuTextures.TryGetValue(instance, out var texture))
            return texture.View;
        return _whiteTexture!.View;
    }

    private static GpuTexture CreateGpuTexture(ID3D11Device device, BitmapSource source)
    {
        BitmapSource bitmap = source;
        if (bitmap.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = bitmap;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();
            bitmap = converted;
        }

        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        bitmap.CopyPixels(pixels, stride, 0);
        var texture = device.CreateTexture2D(
            pixels,
            Format.B8G8R8A8_UNorm,
            (uint)width,
            (uint)height,
            bindFlags: BindFlags.ShaderResource);
        return new GpuTexture(texture, device.CreateShaderResourceView(texture));
    }

    private static BitmapSource CreateWhiteBitmap()
    {
        var bitmap = BitmapSource.Create(
            1, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 255, 255, 255, 255 },
            4);
        bitmap.Freeze();
        return bitmap;
    }

    private static ParticleVertex[] CreateGridVertices()
    {
        var vertices = new List<ParticleVertex>();
        const int halfSteps = 20;
        const float step = 0.25f;
        var extent = halfSteps * step;
        var uv = new Vector2(0.5f, 0.5f);

        for (var index = -halfSteps; index <= halfSteps; index++)
        {
            var coordinate = index * step;
            var major = index == 0;
            var color = major
                ? new Vector4(0.40f, 0.40f, 0.40f, 0.72f)
                : new Vector4(0.62f, 0.62f, 0.62f, 0.42f);
            vertices.Add(new ParticleVertex(new Vector3(-extent, 0, coordinate), uv, color));
            vertices.Add(new ParticleVertex(new Vector3(extent, 0, coordinate), uv, color));
            vertices.Add(new ParticleVertex(new Vector3(coordinate, 0, -extent), uv, color));
            vertices.Add(new ParticleVertex(new Vector3(coordinate, 0, extent), uv, color));
        }

        return vertices.ToArray();
    }

    private void DisposeEffectTextures()
    {
        foreach (var texture in _gpuTextures.Values)
            texture.Dispose();
        _gpuTextures.Clear();
    }

    private void DisposeGpuResources()
    {
        _gpuReady = false;
        DisposeEffectTextures();
        _whiteTexture?.Dispose();
        _whiteTexture = null;
        _gridVertexBuffer?.Dispose();
        _gridVertexBuffer = null;
        _particleVertexBuffer?.Dispose();
        _particleVertexBuffer = null;
        _cameraBuffer?.Dispose();
        _cameraBuffer = null;
        _samplerState?.Dispose();
        _samplerState = null;
        _alphaBlendState?.Dispose();
        _alphaBlendState = null;
        _additiveBlendState?.Dispose();
        _additiveBlendState = null;
        _depthReadState?.Dispose();
        _depthReadState = null;
        _depthWriteState?.Dispose();
        _depthWriteState = null;
        _depthDisabledState?.Dispose();
        _depthDisabledState = null;
        _rasterizerState?.Dispose();
        _rasterizerState = null;
        _inputLayout?.Dispose();
        _inputLayout = null;
        _vertexShader?.Dispose();
        _vertexShader = null;
        _pixelShader?.Dispose();
        _pixelShader = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _surface.AlwaysRefresh = false;
        _surface.LoadContent -= Surface_LoadContent;
        _surface.Draw -= Surface_Draw;
        _surface.UnloadContent -= Surface_UnloadContent;
        DisposeGpuResources();
        if (_surface.Parent == _host)
            _host.Children.Remove(_surface);
    }

    private static void IncludePoint(ref Point3D minimum, ref Point3D maximum, Point3D point)
    {
        if (!IsFinite(point))
            return;
        minimum = new Point3D(Math.Min(minimum.X, point.X), Math.Min(minimum.Y, point.Y), Math.Min(minimum.Z, point.Z));
        maximum = new Point3D(Math.Max(maximum.X, point.X), Math.Max(maximum.Y, point.Y), Math.Max(maximum.Z, point.Z));
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

    private static Vector3 ToVector3(Point3D point) => new((float)point.X, (float)point.Y, (float)point.Z);
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ParticleVertex
    {
        public const uint SizeInBytes = 36;

        public ParticleVertex(Vector3 position, Vector2 texCoord, Vector4 color)
        {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }

        public readonly Vector3 Position;
        public readonly Vector2 TexCoord;
        public readonly Vector4 Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CameraConstants
    {
        public Matrix4x4 ViewProjection;
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
                Slots.Add(new ParticleSlot());
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
    }

    private sealed class ParticleSlot
    {
        public bool IsVisible { get; set; }
        public Point3D Center { get; set; }
        public double HalfWidth { get; set; }
        public double HalfHeight { get; set; }
        public double Rotation { get; set; }
        public int TextureFrame { get; set; } = -1;
        public double U0 { get; set; }
        public double U1 { get; set; } = 1;
        public double V0 { get; set; }
        public double V1 { get; set; } = 1;
        public Color Color { get; set; } = Colors.White;
        public double Alpha { get; set; } = 1;
    }

    private sealed record RenderItem(
        EmitterPreview Emitter,
        ParticleSlot Slot,
        Vector3D Right,
        Vector3D Up,
        double CameraDistanceSquared,
        int Sequence);

    private sealed class DrawBatch
    {
        public DrawBatch(ulong textureInstance, bool additive, bool ignoreDepth, int startVertex, int vertexCount)
        {
            TextureInstance = textureInstance;
            Additive = additive;
            IgnoreDepth = ignoreDepth;
            StartVertex = startVertex;
            VertexCount = vertexCount;
        }

        public ulong TextureInstance { get; }
        public bool Additive { get; }
        public bool IgnoreDepth { get; }
        public int StartVertex { get; }
        public int VertexCount { get; set; }
    }

    private sealed class GpuTexture : IDisposable
    {
        public GpuTexture(ID3D11Texture2D texture, ID3D11ShaderResourceView view)
        {
            Texture = texture;
            View = view;
        }

        public ID3D11Texture2D Texture { get; }
        public ID3D11ShaderResourceView View { get; }

        public void Dispose()
        {
            View.Dispose();
            Texture.Dispose();
        }
    }
}
