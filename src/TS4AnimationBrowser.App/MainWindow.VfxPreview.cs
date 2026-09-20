using System.Windows.Media;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private const double MinimumVfxPreviewDurationSeconds = 8.0;
    private const double MaximumVfxPreviewDurationSeconds = 30.0;
    private static readonly TimeSpan MinimumVfxRenderInterval = TimeSpan.FromMilliseconds(13);

    private VfxDirectXPreview? _vfxParticlePreview;
    private VfxDirectXModelPreview? _vfxModelPreview;
    private bool _vfxPreviewRenderingAttached;
    private bool _vfxPreviewClosedAttached;
    private TimeSpan? _lastVfxRenderingTime;

    private void StartVfxParticlePreview(VfxLoadResult result)
    {
        if (result.ParticleEffects.Count == 0)
        {
            ClearVfxParticlePreview();
            return;
        }

        _vfxParticlePreview ??= new VfxDirectXPreview(AnimationViewport, ViewportCamera);
        _vfxModelPreview ??= new VfxDirectXModelPreview(AnimationViewport, ViewportCamera);
        if (ViewerSurface.Background is SolidColorBrush viewerBrush)
            _vfxParticlePreview.SetBackgroundColor(viewerBrush.Color);
        _vfxParticlePreview.SetGridVisible(_gridVisible);

        var spriteResult = BuildSpritePreviewResult(result);
        _vfxParticlePreview.Load(spriteResult);
        _vfxModelPreview.Load(result);

        var duration = CalculateVfxPreviewDuration(result.ParticleEffects);
        _vfxParticlePreview.WarmUp(duration);
        _vfxModelPreview.WarmUp(duration);

        _playback.Load(duration);
        _playback.Loop = LoopCheckBox.IsChecked == true;
        _vfxParticlePreview.Update(0);
        _vfxModelPreview.Update(0);
        UpdatePlaybackUi();
        _lastVfxRenderingTime = null;
        ResetCameraButton.IsEnabled = false;

        if (!_vfxPreviewRenderingAttached)
        {
            CompositionTarget.Rendering += VfxPreview_Rendering;
            _vfxPreviewRenderingAttached = true;
        }

        if (!_vfxPreviewClosedAttached)
        {
            Closed += (_, _) =>
            {
                DetachVfxPreviewRendering();
                _vfxParticlePreview?.Dispose();
                _vfxParticlePreview = null;
                _vfxModelPreview?.Dispose();
                _vfxModelPreview = null;
            };
            _vfxPreviewClosedAttached = true;
        }

        ApplyFrontViewerCamera();
    }

    private static VfxLoadResult BuildSpritePreviewResult(VfxLoadResult result)
    {
        var particleBlocks = result.Blocks
            .Where(block => block.BlockType == VfxSwarmBlockType.ParticleEffect)
            .ToArray();
        var count = Math.Min(particleBlocks.Length, result.ParticleEffects.Count);
        var renderableBlocks = new List<VfxSwarmVisualBlock>(count);
        var renderableParticles = new List<VfxSwarmParticleEffect>(count);

        for (var index = 0; index < count; index++)
        {
            var particle = result.ParticleEffects[index];
            if (VfxSwarmSemantics.IsModelParticle(particle))
                continue;
            if (!result.Textures.ContainsKey(particle.TextureInstance))
                continue;

            renderableBlocks.Add(particleBlocks[index]);
            renderableParticles.Add(particle);
        }

        return result with
        {
            Blocks = renderableBlocks,
            ParticleEffects = renderableParticles
        };
    }

    private void ClearVfxParticlePreview()
    {
        _vfxParticlePreview?.Clear();
        _vfxModelPreview?.Clear();
        _lastVfxRenderingTime = null;
        DetachVfxPreviewRendering();
        ResetCameraButton.IsEnabled = true;
    }

    private void VfxPreview_Rendering(object? sender, EventArgs e)
    {
        if (_vfxParticlePreview?.HasEffect != true && _vfxModelPreview?.HasEffect != true)
            return;

        if (e is RenderingEventArgs renderingArgs)
        {
            if (_lastVfxRenderingTime is { } previous
                && renderingArgs.RenderingTime - previous < MinimumVfxRenderInterval)
            {
                return;
            }
            _lastVfxRenderingTime = renderingArgs.RenderingTime;
        }

        var position = _playback.PositionSeconds;
        _vfxParticlePreview?.Update(position);
        _vfxModelPreview?.Update(position);
    }

    private static double CalculateVfxPreviewDuration(IReadOnlyList<VfxSwarmParticleEffect> particles)
    {
        if (particles.Count == 0)
            return MinimumVfxPreviewDurationSeconds;

        var longestLifetime = particles.Max(particle =>
            Math.Max(
                Math.Max(0, particle.ParticleLifetimeMin),
                Math.Max(0, particle.ParticleLifetimeMax)));
        var longestRateCurve = particles.Max(particle => Math.Max(0, particle.EmitRateCurveTime));
        var longestRetrigger = particles.Max(particle =>
            Math.Max(
                Math.Max(0, particle.EmitRetriggerMin),
                Math.Max(0, particle.EmitRetriggerMax)));

        var duration = Math.Max(
            MinimumVfxPreviewDurationSeconds,
            Math.Max(longestLifetime * 8.0, Math.Max(longestRateCurve * 2.0, longestRetrigger * 2.0)));
        return Math.Clamp(duration, MinimumVfxPreviewDurationSeconds, MaximumVfxPreviewDurationSeconds);
    }

    private void DetachVfxPreviewRendering()
    {
        if (!_vfxPreviewRenderingAttached)
            return;

        CompositionTarget.Rendering -= VfxPreview_Rendering;
        _vfxPreviewRenderingAttached = false;
        _lastVfxRenderingTime = null;
    }
}
