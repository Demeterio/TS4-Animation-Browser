using System.IO;
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

public sealed class VfxDirectXModelPreview : IDisposable
{
    private const int MaximumVertices = 1_500_000;
    private const string ShaderSource = """
cbuffer CameraBuffer : register(b0)
{
    row_major float4x4 ViewProjection;
};

cbuffer MaterialBuffer : register(b1)
{
    float AlphaThreshold;
    float UseSeparateAlpha;
    float2 MaterialPadding;
};

Texture2D DiffuseTexture : register(t0);
Texture2D AlphaTexture : register(t1);
SamplerState MaterialSampler : register(s0);

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0), ViewProjection);
    output.Normal = input.Normal;
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_Target
{
    float4 diffuseSample = DiffuseTexture.Sample(MaterialSampler, input.TexCoord);
    float alphaMask = UseSeparateAlpha > 0.5
        ? AlphaTexture.Sample(MaterialSampler, input.TexCoord).a
        : 1.0;
    float alpha = saturate(diffuseSample.a * alphaMask * input.Color.a);
    if (AlphaThreshold > 0.0)
        clip(alpha - AlphaThreshold);

    float3 normal = normalize(input.Normal);
    float3 lightDirection = normalize(float3(-0.35, 0.8, -0.45));
    float lighting = 0.35 + 0.65 * saturate(dot(normal, lightDirection));
    return float4(diffuseSample.rgb * input.Color.rgb * lighting, alpha);
}
""";

    private readonly DrawingSurface _surface;
    private readonly PerspectiveCamera _camera;
    private readonly List<ModelEmitter> _emitters = [];
    private readonly List<ModelVertex> _vertices = [];
    private readonly List<DrawBatch> _drawBatches = [];
    private readonly Dictionary<ulong, GpuTexture> _gpuTextures = [];

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _cameraBuffer;
    private ID3D11Buffer? _materialBuffer;
    private ID3D11SamplerState? _samplerState;
    private ID3D11BlendState? _opaqueBlendState;
    private ID3D11BlendState? _alphaBlendState;
    private ID3D11BlendState? _additiveBlendState;
    private ID3D11DepthStencilState? _depthWriteState;
    private ID3D11DepthStencilState? _depthReadState;
    private ID3D11RasterizerState? _rasterizerState;
    private GpuTexture? _whiteTexture;
    private bool _gpuReady;
    private bool _disposed;

    public VfxDirectXModelPreview(Viewport3D viewport, PerspectiveCamera camera)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(camera);
        if (viewport.Parent is not Grid host)
            throw new InvalidOperationException("The animation viewport must be hosted by a Grid to attach the Direct3D model surface.");

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
    public PreviewBounds? FramingBounds { get; private set; }
    public double PreferredFrontYaw { get; private set; } = Math.PI;

    public void Clear()
    {
        _emitters.Clear();
        _vertices.Clear();
        _drawBatches.Clear();
        DisposeEffectTextures();
        FramingBounds = null;
        PreferredFrontYaw = Math.PI;
        _surface.AlwaysRefresh = false;
        _surface.Visibility = Visibility.Collapsed;
        _surface.Invalidate();
    }

    public void Load(VfxLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Clear();

        var blocks = result.Blocks
            .Where(block => block.BlockType == VfxSwarmBlockType.ParticleEffect)
            .ToArray();
        var count = Math.Min(blocks.Length, result.ParticleEffects.Count);
        for (var index = 0; index < count; index++)
        {
            var particle = result.ParticleEffects[index];
            if (!VfxSwarmSemantics.IsModelParticle(particle))
                continue;
            if (!result.Models.TryGetValue(particle.TextureInstance, out var asset) || asset.Model is null)
                continue;

            var meshes = new List<ModelMesh>();
            foreach (var mesh in asset.Model.Meshes)
            {
                VfxModelRenderMaterial material;
                try
                {
                    material = VfxModelMaterialResolver.ResolveRenderMaterial(asset, mesh);
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException or ArgumentOutOfRangeException)
                {
                    material = new VfxModelRenderMaterial(0, 0, 0, 0);
                }

                result.Textures.TryGetValue(material.DiffuseTextureInstance, out var diffuseTexture);
                result.Textures.TryGetValue(material.AlphaTextureInstance, out var alphaTexture);
                meshes.Add(new ModelMesh(mesh, material, diffuseTexture, alphaTexture));
            }

            _emitters.Add(new ModelEmitter(blocks[index], particle, asset.Model, meshes));
        }

        if (_emitters.Count == 0)
            return;

        FramingBounds = CalculateFramingBounds();
        BuildFrameGeometry(0);
        RebuildEffectTextures();
        _surface.Visibility = Visibility.Visible;
        _surface.AlwaysRefresh = true;
        _surface.Invalidate();
    }

    public void WarmUp(double durationSeconds)
    {
        if (_emitters.Count == 0)
            return;
        BuildFrameGeometry(Math.Max(0, durationSeconds));
        BuildFrameGeometry(0);
        _surface.Invalidate();
    }

    public void Update(double timeSeconds)
    {
        if (_emitters.Count == 0)
            return;
        BuildFrameGeometry(Math.Max(0, timeSeconds));
        _surface.Invalidate();
    }

    private PreviewBounds? CalculateFramingBounds()
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var hasPosition = false;

        foreach (var emitter in _emitters)
        {
            var size = Math.Max(0.0001, MaximumCurveValue(emitter.Particle.SizeCurve, 1.0));
            var scale = (float)(size * (VfxSwarmSemantics.UsesScale(emitter.Block) ? emitter.Block.LocalScale : 1.0));

            foreach (var mesh in emitter.Model.Meshes)
            {
                if (mesh.PrimitiveType != VfxModelPrimitiveType.TriangleList)
                    continue;

                foreach (var vertex in mesh.Vertices)
                {
                    var position = TransformPosition(emitter.Block, vertex.Position * scale);
                    if (!IsFinite(position))
                        continue;
                    minimum = Vector3.Min(minimum, position);
                    maximum = Vector3.Max(maximum, position);
                    hasPosition = true;
                }
            }
        }

        if (!hasPosition)
            return null;

        return new PreviewBounds(
            new Point3D(minimum.X, minimum.Y, minimum.Z),
            new Point3D(maximum.X, maximum.Y, maximum.Z));
    }

    private void BuildFrameGeometry(double timeSeconds)
    {
        _vertices.Clear();
        _drawBatches.Clear();
        foreach (var emitter in _emitters)
        {
            var lifetime = Math.Max(0.001, Math.Max(emitter.Particle.ParticleLifetimeMin, emitter.Particle.ParticleLifetimeMax));
            var normalizedAge = (timeSeconds % lifetime) / lifetime;
            var size = Math.Max(0.0001, EvaluateCurve(emitter.Particle.SizeCurve, normalizedAge, 1.0));
            var alpha = Math.Clamp(EvaluateCurve(emitter.Particle.AlphaCurve, normalizedAge, 1.0), 0.0, 1.0);
            var color = EvaluateColor(emitter.Particle.ColorCurve, normalizedAge);
            var tint = new Vector4(color.X, color.Y, color.Z, (float)alpha);
            var scale = (float)(size * (VfxSwarmSemantics.UsesScale(emitter.Block) ? emitter.Block.LocalScale : 1.0));

            foreach (var modelMesh in emitter.Meshes)
            {
                var mesh = modelMesh.Mesh;
                if (mesh.PrimitiveType != VfxModelPrimitiveType.TriangleList)
                    continue;

                var startVertex = _vertices.Count;
                foreach (var index in mesh.Indices)
                {
                    if ((uint)index >= (uint)mesh.Vertices.Count || _vertices.Count >= MaximumVertices)
                        continue;
                    var source = mesh.Vertices[index];
                    var position = TransformPosition(emitter.Block, source.Position * scale);
                    var normal = TransformDirection(emitter.Block, source.Normal);
                    if (normal.LengthSquared() < 0.000001f)
                        normal = Vector3.UnitY;
                    else
                        normal = Vector3.Normalize(normal);
                    var uv = modelMesh.Material.DiffuseUvChannel == 1 ? source.Uv1 : source.Uv;
                    _vertices.Add(new ModelVertex(position, normal, uv, tint));
                }

                var vertexCount = _vertices.Count - startVertex;
                if (vertexCount > 0)
                {
                    var diffuseInstance = modelMesh.DiffuseTexture?.Instance ?? 0;
                    var alphaInstance = modelMesh.AlphaTexture?.Instance ?? 0;
                    var alphaThreshold = modelMesh.Material.RenderMode == VfxModelRenderMode.OpaqueCutout
                        ? Math.Max(modelMesh.Material.AlphaMaskThreshold, 1f / 255f)
                        : modelMesh.Material.AlphaMaskThreshold;
                    _drawBatches.Add(new DrawBatch(
                        diffuseInstance,
                        alphaInstance,
                        alphaInstance != 0 && alphaInstance != diffuseInstance,
                        alphaThreshold,
                        modelMesh.Material.RenderMode,
                        startVertex,
                        vertexCount));
                }
            }
        }
    }

    private static Vector3 TransformPosition(VfxSwarmVisualBlock block, Vector3 value)
    {
        var rotated = TransformDirection(block, value);
        if (!VfxSwarmSemantics.UsesOffset(block))
            return rotated;
        return rotated + new Vector3(block.PositionX, block.PositionY, block.PositionZ);
    }

    private static Vector3 TransformDirection(VfxSwarmVisualBlock block, Vector3 value)
    {
        if (!VfxSwarmSemantics.UsesRotation(block))
            return value;
        return new Vector3(
            block.Orientation11 * value.X + block.Orientation12 * value.Y + block.Orientation13 * value.Z,
            block.Orientation21 * value.X + block.Orientation22 * value.Y + block.Orientation23 * value.Z,
            block.Orientation31 * value.X + block.Orientation32 * value.Y + block.Orientation33 * value.Z);
    }

    private static double MaximumCurveValue(IReadOnlyList<float> curve, double fallback)
    {
        var maximum = fallback;
        foreach (var value in curve)
        {
            if (float.IsFinite(value))
                maximum = Math.Max(maximum, value);
        }
        return maximum;
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static double EvaluateCurve(IReadOnlyList<float> curve, double normalizedAge, double fallback)
    {
        if (curve.Count == 0)
            return fallback;
        if (curve.Count == 1)
            return curve[0];
        var scaled = Math.Clamp(normalizedAge, 0, 1) * (curve.Count - 1);
        var lower = Math.Clamp((int)Math.Floor(scaled), 0, curve.Count - 1);
        var upper = Math.Min(curve.Count - 1, lower + 1);
        var fraction = scaled - lower;
        return curve[lower] + (curve[upper] - curve[lower]) * fraction;
    }

    private static Vector3 EvaluateColor(IReadOnlyList<VfxParticleColor> curve, double normalizedAge)
    {
        if (curve.Count == 0)
            return Vector3.One;
        if (curve.Count == 1)
            return new Vector3(curve[0].Red, curve[0].Green, curve[0].Blue);
        var scaled = Math.Clamp(normalizedAge, 0, 1) * (curve.Count - 1);
        var lower = Math.Clamp((int)Math.Floor(scaled), 0, curve.Count - 1);
        var upper = Math.Min(curve.Count - 1, lower + 1);
        var fraction = (float)(scaled - lower);
        var a = curve[lower];
        var b = curve[upper];
        return Vector3.Lerp(
            new Vector3(a.Red, a.Green, a.Blue),
            new Vector3(b.Red, b.Green, b.Blue),
            fraction);
    }

    private void Surface_LoadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        DisposeGpuResources();
        _device = e.Device;
        _context = e.Context;

        var vertexShaderByteCode = Compiler.Compile(ShaderSource, "VSMain", "TS4VfxModelDirectX.hlsl", "vs_4_0");
        var pixelShaderByteCode = Compiler.Compile(ShaderSource, "PSMain", "TS4VfxModelDirectX.hlsl", "ps_4_0");
        _vertexShader = e.Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = e.Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = e.Device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 32, 0)
        ], vertexShaderByteCode.Span);
        _vertexBuffer = e.Device.CreateBuffer(
            (uint)(ModelVertex.SizeInBytes * MaximumVertices),
            BindFlags.VertexBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write);
        _cameraBuffer = e.Device.CreateConstantBuffer<CameraConstants>();
        _materialBuffer = e.Device.CreateConstantBuffer<MaterialConstants>();
        _samplerState = e.Device.CreateSamplerState(SamplerDescription.LinearWrap);
        _opaqueBlendState = e.Device.CreateBlendState(BlendDescription.Opaque);
        _alphaBlendState = e.Device.CreateBlendState(BlendDescription.NonPremultiplied);
        _additiveBlendState = e.Device.CreateBlendState(BlendDescription.Additive);
        _depthWriteState = e.Device.CreateDepthStencilState(DepthStencilDescription.Default);
        _depthReadState = e.Device.CreateDepthStencilState(DepthStencilDescription.DepthRead);
        _rasterizerState = e.Device.CreateRasterizerState(RasterizerDescription.CullNone);
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
        e.Context.ClearRenderTargetView(e.Surface.ColorTextureView!, new Color4(0, 0, 0, 0));
        if (e.Surface.DepthStencilView is not null)
            e.Context.ClearDepthStencilView(e.Surface.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        if (!_gpuReady || _vertices.Count == 0 || _drawBatches.Count == 0 || _vertexShader is null || _pixelShader is null
            || _inputLayout is null || _vertexBuffer is null || _cameraBuffer is null || _materialBuffer is null
            || _samplerState is null || _rasterizerState is null || _whiteTexture is null
            || _opaqueBlendState is null || _alphaBlendState is null || _additiveBlendState is null
            || _depthWriteState is null || _depthReadState is null)
        {
            return;
        }

        UpdateCameraBuffer(e.Context, e.Surface.TextureWidth, e.Surface.TextureHeight);
        var mapped = e.Context.Map(_vertexBuffer, 0, MapMode.WriteDiscard);
        CollectionsMarshal.AsSpan(_vertices).CopyTo(mapped.AsSpan<ModelVertex>(_vertices.Count));
        e.Context.Unmap(_vertexBuffer, 0);

        e.Context.IASetInputLayout(_inputLayout);
        e.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        e.Context.IASetVertexBuffer(0, _vertexBuffer, (uint)ModelVertex.SizeInBytes);
        e.Context.VSSetShader(_vertexShader);
        e.Context.VSSetConstantBuffer(0, _cameraBuffer);
        e.Context.PSSetShader(_pixelShader);
        e.Context.PSSetConstantBuffer(1, _materialBuffer);
        e.Context.PSSetSampler(0, _samplerState);
        e.Context.RSSetState(_rasterizerState);

        foreach (var batch in _drawBatches)
        {
            switch (batch.RenderMode)
            {
                case VfxModelRenderMode.AlphaBlend:
                    e.Context.OMSetBlendState(_alphaBlendState);
                    e.Context.OMSetDepthStencilState(_depthReadState, 0);
                    break;
                case VfxModelRenderMode.Additive:
                    e.Context.OMSetBlendState(_additiveBlendState);
                    e.Context.OMSetDepthStencilState(_depthReadState, 0);
                    break;
                default:
                    e.Context.OMSetBlendState(_opaqueBlendState);
                    e.Context.OMSetDepthStencilState(_depthWriteState, 0);
                    break;
            }

            UpdateMaterialBuffer(e.Context, batch.AlphaThreshold, batch.UseSeparateAlpha);
            e.Context.PSSetShaderResource(0, ResolveGpuTexture(batch.DiffuseTextureInstance));
            e.Context.PSSetShaderResource(1, ResolveGpuTexture(batch.AlphaTextureInstance));
            e.Context.Draw((uint)batch.VertexCount, (uint)batch.StartVertex);
        }

        e.Context.PSUnsetShaderResource(0);
        e.Context.PSUnsetShaderResource(1);
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
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(verticalFov, aspect, 0.01f, 250f);
        var constants = new CameraConstants { ViewProjection = view * projection };

        var mapped = context.Map(_cameraBuffer!, 0, MapMode.WriteDiscard);
        mapped.AsSpan<CameraConstants>(1)[0] = constants;
        context.Unmap(_cameraBuffer!, 0);
    }

    private void UpdateMaterialBuffer(ID3D11DeviceContext context, float alphaThreshold, bool useSeparateAlpha)
    {
        var constants = new MaterialConstants
        {
            AlphaThreshold = Math.Clamp(alphaThreshold, 0, 1),
            UseSeparateAlpha = useSeparateAlpha ? 1f : 0f
        };
        var mapped = context.Map(_materialBuffer!, 0, MapMode.WriteDiscard);
        mapped.AsSpan<MaterialConstants>(1)[0] = constants;
        context.Unmap(_materialBuffer!, 0);
    }

    private void RebuildEffectTextures()
    {
        DisposeEffectTextures();
        if (!_gpuReady || _device is null)
            return;

        foreach (var texture in _emitters
                     .SelectMany(emitter => emitter.Meshes)
                     .SelectMany(mesh => new[] { mesh.DiffuseTexture, mesh.AlphaTexture })
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
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _cameraBuffer?.Dispose();
        _cameraBuffer = null;
        _materialBuffer?.Dispose();
        _materialBuffer = null;
        _samplerState?.Dispose();
        _samplerState = null;
        _opaqueBlendState?.Dispose();
        _opaqueBlendState = null;
        _alphaBlendState?.Dispose();
        _alphaBlendState = null;
        _additiveBlendState?.Dispose();
        _additiveBlendState = null;
        _depthWriteState?.Dispose();
        _depthWriteState = null;
        _depthReadState?.Dispose();
        _depthReadState = null;
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
        _surface.LoadContent -= Surface_LoadContent;
        _surface.Draw -= Surface_Draw;
        _surface.UnloadContent -= Surface_UnloadContent;
        Clear();
        DisposeGpuResources();
        if (_surface.Parent is Panel panel)
            panel.Children.Remove(_surface);
    }

    private sealed record ModelEmitter(
        VfxSwarmVisualBlock Block,
        VfxSwarmParticleEffect Particle,
        VfxDecodedModel Model,
        IReadOnlyList<ModelMesh> Meshes);

    private sealed record ModelMesh(
        VfxDecodedModelMesh Mesh,
        VfxModelRenderMaterial Material,
        VfxTextureAsset? DiffuseTexture,
        VfxTextureAsset? AlphaTexture);

    private sealed record DrawBatch(
        ulong DiffuseTextureInstance,
        ulong AlphaTextureInstance,
        bool UseSeparateAlpha,
        float AlphaThreshold,
        VfxModelRenderMode RenderMode,
        int StartVertex,
        int VertexCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct ModelVertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 color)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;
        public Vector2 TexCoord = texCoord;
        public Vector4 Color = color;
        public static int SizeInBytes => Marshal.SizeOf<ModelVertex>();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CameraConstants
    {
        public Matrix4x4 ViewProjection;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MaterialConstants
    {
        public float AlphaThreshold;
        public float UseSeparateAlpha;
        public Vector2 Padding;
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
