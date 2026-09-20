using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private bool _selectedDetailLabelNormalizationAttached;
    private bool _cacheSaveProgressAttached;
    private bool _vfxSelectionPipelineAttached;
    private DependencyPropertyDescriptor? _loadingTextDescriptor;

    private void ExtendedSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder is not null)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyExtendedLibraryFilter();
    }

    private void ExtendedFilterChanged(object sender, RoutedEventArgs e) => ApplyExtendedLibraryFilter();

    private void RefreshFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        HideUnknown.IsChecked = true;
        SimAnimationsOnly.IsChecked = false;
        ObjectAnimationsOnly.IsChecked = false;
        VfxOnly.IsChecked = false;
        ResetLibraryNameSort();
        ApplyExtendedLibraryFilter();
    }

    private void LoadingOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Title = AppInfo.DisplayName;

        if (!_selectedDetailLabelNormalizationAttached && SelectedDetail is not null)
        {
            SelectedDetail.TextChanged += NormalizeSelectedDetailLabels;
            _selectedDetailLabelNormalizationAttached = true;
            NormalizeSelectedDetailLabels(SelectedDetail, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
        }

        if (!_cacheSaveProgressAttached)
        {
            DtabCache.SaveProgressChanged += CacheSaveProgressChanged;
            _loadingTextDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            _loadingTextDescriptor?.AddValueChanged(LoadingText, LoadingTextValueChanged);
            Closed += (_, _) =>
            {
                DtabCache.SaveProgressChanged -= CacheSaveProgressChanged;
                _loadingTextDescriptor?.RemoveValueChanged(LoadingText, LoadingTextValueChanged);
            };
            _cacheSaveProgressAttached = true;
        }

        if (!_vfxSelectionPipelineAttached && ResourcesGrid is not null)
        {
            ResourcesGrid.SelectionChanged += VfxSelectionPipeline_SelectionChanged;
            _vfxSelectionPipelineAttached = true;
        }

        if (LoadingOverlay.IsVisible)
            return;

        ApplyExtendedLibraryFilter();
        if (_resources.Count > 0
            && ResourcesGrid is not null
            && ResourcesGrid.SelectedItem is null)
        {
            ViewportMessage.Text = "Select a resource to preview";
            ViewportMessagePanel.Visibility = Visibility.Visible;
        }
    }

    private void LoadingTextValueChanged(object? sender, EventArgs e)
    {
        if (!string.Equals(LoadingText.Text, "Saving encrypted library cache…", StringComparison.Ordinal))
            return;

        LoadingProgress.IsIndeterminate = false;
        LoadingProgress.Minimum = 0;
        LoadingProgress.Maximum = 100;
        LoadingProgress.Value = 0;
    }

    private void CacheSaveProgressChanged(CacheSaveProgress progress)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => CacheSaveProgressChanged(progress)));
            return;
        }

        LoadingText.Text = "Saving encrypted library cache…";
        LoadingDetail.Text = $"{progress.Stage} {progress.Detail}";
        LoadingProgress.IsIndeterminate = false;
        LoadingProgress.Minimum = 0;
        LoadingProgress.Maximum = 100;
        LoadingProgress.Value = progress.Percent;
    }

    private async void VfxSelectionPipeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearVfxParticlePreview();
        if (ResourcesGrid.SelectedItem is not ResourceRow row || !KnownResourceTypes.IsVfx(row.Entry.Key.Type))
            return;

        var selectionVersion = _selectionVersion;
        SetViewerLoading(true, "Loading VFX…", $"Resolving {row.Name} and its particle resources…");

        try
        {
            var result = await Task.Run(() => VfxResourceLoader.Load(row));
            if (selectionVersion != _selectionVersion || ResourcesGrid.SelectedItem != row)
                return;

            SelectedTitle.Text = result.EffectName;
            SelectedDetail.Text = BuildVfxDetail(row, result);

            if (result.VisualEffectVersion > 0)
            {
                var blockTypes = result.Blocks
                    .GroupBy(block => block.BlockType)
                    .OrderBy(group => (byte)group.Key)
                    .Select(group => $"{FormatVfxBlockType(group.Key)} × {group.Count():N0}");
                var blockSummary = result.Blocks.Count == 0
                    ? "no blocks"
                    : string.Join(" • ", blockTypes);

                var particleSummary = result.ParticleEffects.Count == 0
                    ? "No ParticleEffect blocks are directly referenced by this VisualEffect."
                    : $"ParticleEffect v{result.ParticleEffectVersion} decoded • {result.ParticleEffects.Count:N0} referenced particle block(s) • {FormatParticleSummary(result.ParticleEffects)} • {FormatTextureResolution(result)}";

                ViewportMessage.Text = $"VisualEffect #{result.VisualEffectIndex:N0} decoded • {result.Blocks.Count:N0} block(s) • {blockSummary}.\n{particleSummary}";
                if (result.ParticleEffects.Count > 0)
                {
                    StartVfxParticlePreview(result);
                    ViewportMessagePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ViewportMessagePanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ViewportMessage.Text = $"Legacy VFX resource loaded • {result.ResourceEffectNames.Count:N0} effect(s) • {result.PayloadSize:N0} bytes.";
                ViewportMessagePanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            if (selectionVersion != _selectionVersion || ResourcesGrid.SelectedItem != row)
                return;

            ClearVfxParticlePreview();
            SelectedDetail.Text = BuildVfxDetail(row);
            ViewportMessage.Text = $"Could not decode this VFX entry: {ex.Message}";
            ViewportMessagePanel.Visibility = Visibility.Visible;
        }
        finally
        {
            if (selectionVersion == _selectionVersion && ResourcesGrid.SelectedItem == row)
                SetViewerLoading(false);
        }
    }

    private void NormalizeSelectedDetailLabels(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || string.IsNullOrEmpty(textBox.Text))
            return;

        if (ResourcesGrid?.SelectedItem is ResourceRow row && KnownResourceTypes.IsVfx(row.Entry.Key.Type))
            return;

        var normalized = textBox.Text
            .Replace("CLIP Instance:", "CLIP instance:", StringComparison.Ordinal)
            .Replace("CLIP Source:", "CLIP source:", StringComparison.Ordinal)
            .Replace("Rig namespace:", "RIG namespace:", StringComparison.Ordinal);

        if (!string.Equals(normalized, textBox.Text, StringComparison.Ordinal))
            textBox.Text = normalized;
    }

    private static string BuildVfxDetail(ResourceRow row)
        => $"VFX instance: {row.Instance}\nType: {row.TypeId} • Group: {row.Group}\nVFX source: {row.Pack} • {row.Package}";

    private static string BuildVfxDetail(ResourceRow row, VfxLoadResult result)
    {
        var identity = BuildVfxDetail(row);
        if (result.VisualEffectVersion == 0)
            return identity;

        var referencedEffects = result.Blocks
            .Where(block => block.BlockType == VfxSwarmBlockType.VisualEffect && !string.IsNullOrWhiteSpace(block.ReferencedEffectName))
            .Select(block => block.ReferencedEffectName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var references = referencedEffects.Length == 0
            ? "—"
            : string.Join(", ", referencedEffects);

        var particleDetail = result.ParticleEffects.Count == 0
            ? "ParticleEffect references: —"
            : $"ParticleEffect: v{result.ParticleEffectVersion} • Referenced: {result.ParticleEffects.Count:N0}\nPreview: Direct3D 11 Swarm sprites • hardware additive blend state • {FormatParticleSummary(result.ParticleEffects)}\nParticle resources: {FormatTextureResolution(result)}{FormatResolvedTextureDetail(result)}{FormatParticleEmitterDetail(result)}{FormatResourceProbeDetail(result)}";

        var visualFlags = VfxSwarmSemantics.FormatVisualEffectFlags(result.VisualEffectFlags);
        return $"{identity}\nSwarm library: {result.LibraryVersionMajor}.{result.LibraryVersionMinor} • VisualEffect v{result.VisualEffectVersion}\nVisualEffect index: {result.VisualEffectIndex} • Effect IID: 0x{result.EffectInstance:X16}\nVisualEffect flags: 0x{result.VisualEffectFlags:X8} ({visualFlags}) • seed 0x{result.VisualEffectSeed:X8}\nBlocks: {result.Blocks.Count:N0} • VisualEffect references: {references}\n{particleDetail}";
    }

    private static string FormatParticleSummary(IReadOnlyList<VfxSwarmParticleEffect> particles)
    {
        var textureCount = particles
            .Select(particle => particle.TextureInstance)
            .Where(VfxTextureResolver.IsResourceReference)
            .Distinct()
            .Count();
        var lifetimeMin = particles.Min(particle => Math.Min(particle.ParticleLifetimeMin, particle.ParticleLifetimeMax));
        var lifetimeMax = particles.Max(particle => Math.Max(particle.ParticleLifetimeMin, particle.ParticleLifetimeMax));
        var emitRatePoints = particles.Sum(particle => particle.EmitRateCurve.Count);
        var sizePoints = particles.Sum(particle => particle.SizeCurve.Count);
        var colorPoints = particles.Sum(particle => particle.ColorCurve.Count);
        return $"primary resource refs {textureCount:N0} • lifetime {lifetimeMin:0.###}–{lifetimeMax:0.###} s • emit-rate points {emitRatePoints:N0} • size points {sizePoints:N0} • color points {colorPoints:N0}";
    }

    private static string FormatTextureResolution(VfxLoadResult result)
    {
        var requested = result.ParticleEffects
            .SelectMany(particle => new[] { particle.TextureInstance, particle.SecondaryTextureInstance })
            .Where(VfxTextureResolver.IsResourceReference)
            .Distinct()
            .Count();
        return $"{result.Textures.Count:N0}/{requested:N0} image resource(s) decoded";
    }

    private static string FormatResolvedTextureDetail(VfxLoadResult result)
    {
        if (result.Textures.Count == 0)
            return string.Empty;

        var details = result.Textures.Values
            .OrderBy(texture => texture.Instance)
            .Select(texture => $"0x{texture.Instance:X16} → Type 0x{texture.Type:X8} • Group 0x{texture.Group:X8} • {texture.Width}×{texture.Height} • {Path.GetFileName(texture.PackagePath)}");
        return $"\n{string.Join("\n", details)}";
    }

    private static string FormatParticleEmitterDetail(VfxLoadResult result)
    {
        var particleBlocks = result.Blocks
            .Where(block => block.BlockType == VfxSwarmBlockType.ParticleEffect)
            .ToArray();
        if (particleBlocks.Length == 0 || result.ParticleEffects.Count == 0)
            return string.Empty;

        var lines = new List<string>(particleBlocks.Length * 5 + 1) { "Emitters:" };
        var count = Math.Min(particleBlocks.Length, result.ParticleEffects.Count);
        for (var emitterIndex = 0; emitterIndex < count; emitterIndex++)
        {
            var block = particleBlocks[emitterIndex];
            var particle = result.ParticleEffects[emitterIndex];
            var texture = VfxTextureResolver.IsResourceReference(particle.TextureInstance)
                ? $"0x{particle.TextureInstance:X16}"
                : "—";
            var secondary = VfxTextureResolver.IsResourceReference(particle.SecondaryTextureInstance)
                ? $"0x{particle.SecondaryTextureInstance:X16}"
                : "—";
            var tiles = $"{Math.Max(1, (int)particle.TileCountU)}×{Math.Max(1, (int)particle.TileCountV)}";
            var frames = $"start {particle.FrameStart} • count {particle.FrameCount} • random {particle.FrameRandom} • speed {particle.FrameSpeed:0.###}";
            var variation = $"size ±{particle.SizeVary:0.###} • aspect ±{particle.AspectRatioVary:0.###} • alpha ±{particle.AlphaVary:0.###} • rotation ±{particle.RotationVary:0.###} / offset {particle.RotationOffset:0.###}";
            var colorVariation = $"color vary ({particle.ColorVaryRed:0.###}, {particle.ColorVaryGreen:0.###}, {particle.ColorVaryBlue:0.###})";
            var colorCurve = FormatColorCurve(particle.ColorCurve);
            var alphaCurve = FormatFloatCurve(particle.AlphaCurve);
            var sizeCurve = FormatFloatCurve(particle.SizeCurve);
            var rotationCurve = FormatFloatCurve(particle.RotationCurve);
            var transformFlags = VfxSwarmSemantics.FormatTransformFlags(block.LocalTransformFlags);
            var alignment = VfxSwarmSemantics.FormatAlignment(particle.AlignMode);
            var drawMode = VfxSwarmSemantics.FormatDrawMode(particle.DrawMode);

            lines.Add($"Emitter {emitterIndex} → Particle #{particle.Index} • block index {block.BlockIndex} • block flags 0x{block.Flags:X8} • transform 0x{block.LocalTransformFlags:X4} ({transformFlags})");
            lines.Add($"  pos ({block.PositionX:0.###}, {block.PositionY:0.###}, {block.PositionZ:0.###}) • scale {block.LocalScale:0.###} • time scale {block.TimeScale:0.###}");
            lines.Add($"  texture {texture} • secondary {secondary} • format {particle.Format} • draw {drawMode} • draw value 0x{particle.DrawValue:X8} • draw flags 0x{particle.DrawFlags:X4} • buffer {particle.Buffer} • layer {particle.Layer} • sort {particle.SortOffset:0.###} • override {particle.OverrideSet}");
            lines.Add($"  align {particle.AlignMode} ({alignment}) • tiles {tiles} • {frames} • {variation} • {colorVariation}");
            lines.Add($"  curves: size [{sizeCurve}] • rotation [{rotationCurve}] • alpha [{alphaCurve}] • color [{colorCurve}]");
            lines.Add($"  orientation: [{block.Orientation11:0.###}, {block.Orientation12:0.###}, {block.Orientation13:0.###}] [{block.Orientation21:0.###}, {block.Orientation22:0.###}, {block.Orientation23:0.###}] [{block.Orientation31:0.###}, {block.Orientation32:0.###}, {block.Orientation33:0.###}]");
        }
        return $"\n{string.Join("\n", lines)}";
    }

    private static string FormatFloatCurve(IReadOnlyList<float> curve)
        => curve.Count == 0
            ? "—"
            : string.Join(", ", curve.Select(value => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));

    private static string FormatColorCurve(IReadOnlyList<VfxParticleColor> curve)
        => curve.Count == 0
            ? "—"
            : string.Join(", ", curve.Select(color => $"({color.Red:0.###}, {color.Green:0.###}, {color.Blue:0.###})"));

    private static string FormatResourceProbeDetail(VfxLoadResult result)
    {
        if (result.ResourceProbes.Count == 0)
            return string.Empty;

        var lines = new List<string>();
        foreach (var probe in result.ResourceProbes.Values.OrderBy(probe => probe.Instance))
        {
            if (probe.Candidates.Count == 0)
            {
                lines.Add($"0x{probe.Instance:X16} → no matching TGI found in scanned game packages");
                continue;
            }

            foreach (var candidate in probe.Candidates
                         .GroupBy(candidate => new { candidate.Type, candidate.Group, candidate.PackagePath })
                         .Select(group => group.First()))
            {
                var suffix = string.IsNullOrWhiteSpace(candidate.DecodeError)
                    ? string.Empty
                    : $" • image decode: {candidate.DecodeError}";
                lines.Add($"0x{probe.Instance:X16} → Type 0x{candidate.Type:X8} • Group 0x{candidate.Group:X8} • {Path.GetFileName(candidate.PackagePath)}{suffix}");
            }
        }

        return lines.Count == 0 ? string.Empty : $"\nProbe:\n{string.Join("\n", lines)}";
    }

    private static string FormatVfxBlockType(VfxSwarmBlockType blockType)
        => blockType switch
        {
            VfxSwarmBlockType.VisualEffect => "Visual",
            VfxSwarmBlockType.ParticleEffect => "Particle",
            VfxSwarmBlockType.MetaparticleEffect => "Metaparticle",
            VfxSwarmBlockType.DecalEffect => "Decal",
            VfxSwarmBlockType.SequenceEffect => "Sequence",
            VfxSwarmBlockType.SoundEffect => "Sound",
            VfxSwarmBlockType.ShakeEffect => "Shake",
            VfxSwarmBlockType.CameraEffect => "Camera",
            VfxSwarmBlockType.ModelEffect => "Model",
            VfxSwarmBlockType.ScreenEffect => "Screen",
            VfxSwarmBlockType.GameEffect => "Game",
            VfxSwarmBlockType.FastParticleEffect => "Fast particle",
            VfxSwarmBlockType.DistributeEffect => "Distribute",
            VfxSwarmBlockType.RibbonEffect => "Ribbon",
            VfxSwarmBlockType.SpriteEffect => "Sprite",
            _ => $"Block {(byte)blockType}"
        };

    private void ResetLibraryNameSort()
    {
        if (_view is null)
            return;

        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            _view.SortDescriptions.Add(new SortDescription(nameof(ResourceRow.Name), ListSortDirection.Ascending));
        }
    }

    private void ApplyExtendedLibraryFilter()
    {
        if (_view is null || SearchBox is null || HideUnknown is null)
            return;

        _view.Filter = MatchesFilter;
        RefreshView();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var window = new HelpWindow { Owner = this };
        window.ShowDialog();
    }
}
