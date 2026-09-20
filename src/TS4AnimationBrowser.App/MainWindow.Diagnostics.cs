using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private Button? _technicalDetailsButton;
    private bool _technicalDetailsAttached;
    private bool _normalizingVfxSummary;

    private void EnsureTechnicalDetailsButton()
    {
        if (_technicalDetailsAttached || SelectedDetail?.Parent is not Panel detailPanel)
            return;

        _technicalDetailsButton = new Button
        {
            Content = "Technical details / Copy",
            Height = 28,
            MinWidth = 158,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
            ToolTip = "Open complete technical diagnostics for the selected resource and copy or save them."
        };
        _technicalDetailsButton.Click += TechnicalDetailsButton_Click;
        detailPanel.Children.Add(_technicalDetailsButton);

        SelectedDetail.TextChanged += TechnicalDetails_SelectedDetailTextChanged;
        _technicalDetailsAttached = true;
    }

    private void TechnicalDetails_SelectedDetailTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_normalizingVfxSummary
            || ResourcesGrid?.SelectedItem is not ResourceRow row
            || !KnownResourceTypes.IsVfx(row.Entry.Key.Type)
            || !SelectedDetail.Text.Contains("Swarm library:", StringComparison.Ordinal))
        {
            return;
        }

        _normalizingVfxSummary = true;
        try
        {
            SelectedDetail.Text = BuildBasicVfxSummary(row);
        }
        finally
        {
            _normalizingVfxSummary = false;
        }
    }

    private static string BuildBasicVfxSummary(ResourceRow row)
    {
        var name = string.IsNullOrWhiteSpace(row.Name) ? "—" : row.Name;
        return $"VFX\nName: {name}\nVFX instance: {row.Instance}\nType: {row.TypeId} • Group: {row.Group}\nVFX source: {row.Pack} • {row.Package}";
    }

    private async void TechnicalDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ResourcesGrid.SelectedItem is not ResourceRow row)
            return;

        var resourceSnapshot = _resources.ToArray();
        var button = _technicalDetailsButton;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Reading…";
        }

        try
        {
            var details = await Task.Run(() => BuildTechnicalDiagnostics(row, resourceSnapshot));
            var name = string.IsNullOrWhiteSpace(row.Name) ? row.Instance : row.Name;
            var window = new ResourceDiagnosticsWindow(name, details) { Owner = this };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not build technical details", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (button is not null)
            {
                button.Content = "Technical details / Copy";
                button.IsEnabled = true;
            }
        }
    }

    private static string BuildTechnicalDiagnostics(ResourceRow row, IReadOnlyCollection<ResourceRow> resources)
    {
        var builder = new StringBuilder(16_384);
        AppendResourceIdentity(builder, row);

        if (KnownResourceTypes.IsVfx(row.Entry.Key.Type))
        {
            builder.AppendLine();
            AppendVfxDiagnostics(builder, VfxResourceLoader.Load(row));
            return builder.ToString();
        }

        if (row.Entry.Key.Type == KnownResourceTypes.Clip)
        {
            builder.AppendLine();
            AppendClipDiagnostics(builder, AnimationResourceLoader.LoadClip(row, resources));
            return builder.ToString();
        }

        try
        {
            var payload = DbpfPackage.ReadResource(row.PackagePath, row.Entry);
            builder.AppendLine();
            builder.AppendLine("RESOURCE DATA");
            builder.AppendLine($"Payload bytes: {payload.Length:N0}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            builder.AppendLine();
            builder.AppendLine("RESOURCE DATA");
            builder.AppendLine($"Read failed: {ex.Message}");
        }

        return builder.ToString();
    }

    private static void AppendResourceIdentity(StringBuilder builder, ResourceRow row)
    {
        builder.AppendLine("RESOURCE");
        builder.AppendLine($"Name: {(string.IsNullOrWhiteSpace(row.Name) ? "—" : row.Name)}");
        builder.AppendLine($"Kind: {row.Type}");
        builder.AppendLine($"Type: {row.TypeId}");
        builder.AppendLine($"Group: {row.Group}");
        builder.AppendLine($"Instance: {row.Instance}");
        builder.AppendLine($"Pack: {row.Pack}");
        builder.AppendLine($"Package: {row.Package}");
        builder.AppendLine($"Package path: {row.PackagePath}");
    }

    private static void AppendClipDiagnostics(StringBuilder builder, AnimationLoadResult result)
    {
        var clip = result.Clip;
        builder.AppendLine("CLIP");
        builder.AppendLine($"Decoded name: {clip.Name}");
        builder.AppendLine($"Codec animation name: {clip.CodecAnimationName}");
        builder.AppendLine($"Duration: {clip.EffectiveDurationSeconds:0.######} s");
        builder.AppendLine($"Frames: {clip.MaxFrameCount:N0}");
        builder.AppendLine($"Tracks: {clip.Tracks.Count:N0}");
        builder.AppendLine($"Rig namespace: {(string.IsNullOrWhiteSpace(clip.RigNamespace) ? "—" : clip.RigNamespace)}");
        builder.AppendLine($"Referenced RIG instance: {(clip.RigInstance == 0 ? "—" : $"0x{clip.RigInstance:X16}")}");
        builder.AppendLine($"RIG resolution: {result.RigResolutionMode}");
        builder.AppendLine($"Visual category: {result.Category}");
        builder.AppendLine($"Matching tracks: {result.MatchingTracks:N0}/{clip.Tracks.Count:N0}");

        builder.AppendLine();
        builder.AppendLine("TRACK KEYS");
        foreach (var track in clip.Tracks.OrderBy(track => track.TrackKey))
            builder.AppendLine($"0x{track.TrackKey:X8}");

        builder.AppendLine();
        builder.AppendLine("RIG");
        if (result.Rig is null)
        {
            builder.AppendLine("Decoded RIG: no");
        }
        else
        {
            builder.AppendLine("Decoded RIG: yes");
            builder.AppendLine($"Bones: {result.Rig.Bones.Count:N0}");
            if (result.RigRow is not null)
            {
                builder.AppendLine($"RIG instance: {result.RigRow.Instance}");
                builder.AppendLine($"RIG source: {result.RigRow.Pack} • {result.RigRow.Package}");
                builder.AppendLine($"RIG package path: {result.RigRow.PackagePath}");
            }

            builder.AppendLine();
            builder.AppendLine("BONES");
            for (var index = 0; index < result.Rig.Bones.Count; index++)
            {
                var bone = result.Rig.Bones[index];
                builder.AppendLine($"[{index:N0}] {bone.Name} • hash 0x{bone.Hash:X8} • parent {bone.ParentIndex} • bind pos ({bone.Position.X:0.######}, {bone.Position.Y:0.######}, {bone.Position.Z:0.######})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("POSE / LIMB DIAGNOSTICS");
        if (result.LimbDiagnostics.Count == 0)
        {
            builder.AppendLine("No limb diagnostics.");
        }
        else
        {
            foreach (var item in result.LimbDiagnostics)
            {
                builder.AppendLine($"{item.BoneName} <- {item.ParentName} • bind {item.BindLength:0.######} • first {item.FirstLength:0.######} • min {item.MinLength:0.######} ({item.MinRatio:0.###}x) • max {item.MaxLength:0.######} ({item.MaxRatio:0.###}x) • max frame {item.MaxFrame} • position track {item.HasPositionTrack} • suspicious {item.IsSuspicious}");
            }
        }
    }

    private static void AppendVfxDiagnostics(StringBuilder builder, VfxLoadResult result)
    {
        builder.AppendLine("VFX / SWARM");
        builder.AppendLine($"Effect name: {result.EffectName}");
        builder.AppendLine($"Payload bytes: {result.PayloadSize:N0}");
        builder.AppendLine($"Library version: {result.LibraryVersionMajor}.{result.LibraryVersionMinor}");
        builder.AppendLine($"VisualEffect version: {result.VisualEffectVersion}");
        builder.AppendLine($"VisualEffect index: {result.VisualEffectIndex:N0}");
        builder.AppendLine($"Effect IID: 0x{result.EffectInstance:X16}");
        builder.AppendLine($"Visual flags: 0x{result.VisualEffectFlags:X8} ({VfxSwarmSemantics.FormatVisualEffectFlags(result.VisualEffectFlags)})");
        builder.AppendLine($"Seed: 0x{result.VisualEffectSeed:X8}");
        builder.AppendLine($"Blocks: {result.Blocks.Count:N0}");
        builder.AppendLine($"Referenced particle blocks: {result.ParticleEffects.Count:N0}");
        builder.AppendLine($"Decoded image resources: {result.Textures.Count:N0}");
        builder.AppendLine($"Resolved model resources: {result.Models.Count:N0}");

        builder.AppendLine();
        builder.AppendLine("VISUAL BLOCKS");
        for (var index = 0; index < result.Blocks.Count; index++)
        {
            var block = result.Blocks[index];
            builder.AppendLine($"[{index:N0}] {block.BlockType} • referenced index {block.BlockIndex:N0} • flags 0x{block.Flags:X8} • transform 0x{block.LocalTransformFlags:X4} ({VfxSwarmSemantics.FormatTransformFlags(block.LocalTransformFlags)})");
            builder.AppendLine($"    scale {block.LocalScale:0.######} • pos ({block.PositionX:0.######}, {block.PositionY:0.######}, {block.PositionZ:0.######}) • time scale {block.TimeScale:0.######} • LOD {block.LodBegin}-{block.LodEnd}");
            builder.AppendLine($"    orientation [{block.Orientation11:0.######}, {block.Orientation12:0.######}, {block.Orientation13:0.######}] [{block.Orientation21:0.######}, {block.Orientation22:0.######}, {block.Orientation23:0.######}] [{block.Orientation31:0.######}, {block.Orientation32:0.######}, {block.Orientation33:0.######}]");
            if (!string.IsNullOrWhiteSpace(block.ReferencedEffectName))
                builder.AppendLine($"    referenced effect: {block.ReferencedEffectName}");
        }

        builder.AppendLine();
        builder.AppendLine("PARTICLE EFFECTS");
        for (var index = 0; index < result.ParticleEffects.Count; index++)
        {
            var particle = result.ParticleEffects[index];
            var isModel = VfxSwarmSemantics.IsModelParticle(particle);
            builder.AppendLine($"Particle #{particle.Index} • model particle {isModel} • flags 0x{particle.Flags:X16}");
            builder.AppendLine($"    primary IID 0x{particle.TextureInstance:X16} • secondary IID 0x{particle.SecondaryTextureInstance:X16}");
            builder.AppendLine($"    lifetime {particle.ParticleLifetimeMin:0.######}..{particle.ParticleLifetimeMax:0.######} • delay {particle.EmitDelayMin:0.######}..{particle.EmitDelayMax:0.######} • retrigger {particle.EmitRetriggerMin:0.######}..{particle.EmitRetriggerMax:0.######}");
            builder.AppendLine($"    direction min ({particle.EmitDirectionMinX:0.######}, {particle.EmitDirectionMinY:0.######}, {particle.EmitDirectionMinZ:0.######}) • max ({particle.EmitDirectionMaxX:0.######}, {particle.EmitDirectionMaxY:0.######}, {particle.EmitDirectionMaxZ:0.######})");
            builder.AppendLine($"    speed {particle.EmitSpeedMin:0.######}..{particle.EmitSpeedMax:0.######} • volume min ({particle.EmitVolumeMinX:0.######}, {particle.EmitVolumeMinY:0.######}, {particle.EmitVolumeMinZ:0.######}) • max ({particle.EmitVolumeMaxX:0.######}, {particle.EmitVolumeMaxY:0.######}, {particle.EmitVolumeMaxZ:0.######})");
            builder.AppendLine($"    format {particle.Format} • draw {VfxSwarmSemantics.FormatDrawMode(particle.DrawMode)} • raw draw mode {particle.DrawMode} • draw value 0x{particle.DrawValue:X8} • draw flags 0x{particle.DrawFlags:X4} • buffer {particle.Buffer} • layer {particle.Layer} • sort {particle.SortOffset:0.######}");
            builder.AppendLine($"    align {particle.AlignMode} ({VfxSwarmSemantics.FormatAlignment(particle.AlignMode)}) • physics {particle.PhysicsType} • override {particle.OverrideSet} • tiles {particle.TileCountU}x{particle.TileCountV}");
            builder.AppendLine($"    frame speed {particle.FrameSpeed:0.######} • start {particle.FrameStart} • count {particle.FrameCount} • random {particle.FrameRandom}");
            builder.AppendLine($"    force ({particle.DirectionalForceX:0.######}, {particle.DirectionalForceY:0.######}, {particle.DirectionalForceZ:0.######}) • wind {particle.WindStrength:0.######} • gravity {particle.GravityStrength:0.######} • drag {particle.Drag:0.######} • velocity stretch {particle.VelocityStretch:0.######}");
            builder.AppendLine($"    size curve [{FormatFloatValues(particle.SizeCurve)}] • aspect curve [{FormatFloatValues(particle.AspectRatioCurve)}] • rotation curve [{FormatFloatValues(particle.RotationCurve)}]");
            builder.AppendLine($"    alpha curve [{FormatFloatValues(particle.AlphaCurve)}] • emit-rate curve [{FormatFloatValues(particle.EmitRateCurve)}] • emit-rate time {particle.EmitRateCurveTime:0.######}");
            builder.AppendLine($"    color curve [{FormatColorValues(particle.ColorCurve)}]");
        }

        builder.AppendLine();
        builder.AppendLine("MODEL PARTICLE DIAGNOSTICS");
        var modelParticles = result.ParticleEffects.Where(VfxSwarmSemantics.IsModelParticle).ToArray();
        if (modelParticles.Length == 0)
        {
            builder.AppendLine("Model Particle detected: no");
        }
        else
        {
            builder.AppendLine($"Model Particle detected: yes ({modelParticles.Length:N0})");
            foreach (var group in modelParticles.GroupBy(particle => particle.TextureInstance))
            {
                var particleIds = string.Join(", ", group.Select(particle => $"#{particle.Index}"));
                builder.AppendLine($"Primary model IID: 0x{group.Key:X16} • used by Particle {particleIds}");
                if (!result.Models.TryGetValue(group.Key, out var asset))
                {
                    builder.AppendLine("Resource resolved: no");
                    continue;
                }

                builder.AppendLine("Resource resolved: yes");
                if (asset.RootModelKey is { } rootKey)
                {
                    builder.AppendLine($"MODL root: {rootKey.Type:X8}:{rootKey.Group:X8}:{rootKey.Instance:X16}");
                    builder.AppendLine($"MODL package: {asset.RootModelPackagePath}");
                }
                if (asset.RootModel is not null)
                {
                    builder.AppendLine($"MODL version: 0x{asset.RootModel.Version:X8} • LOD entries {asset.RootModel.Entries.Count:N0}");
                    foreach (var lod in asset.RootModel.Entries)
                    {
                        var key = lod.ModelLodKey is { } lodKey
                            ? $"{lodKey.Type:X8}:{lodKey.Group:X8}:{lodKey.Instance:X16}"
                            : "unresolved";
                        builder.AppendLine($"    {lod.Id} • ref 0x{lod.ModelLodReference:X8} • key {key} • flags 0x{lod.Flags:X8} • Z {lod.MinZ:0.######}..{lod.MaxZ:0.######}");
                    }
                }
                if (asset.SelectedLod is not null)
                    builder.AppendLine($"Selected visible LOD: {asset.SelectedLod.Id}");

                builder.AppendLine($"Selected MLOD type: 0x{asset.Type:X8}");
                builder.AppendLine($"Selected MLOD group: 0x{asset.Group:X8}");
                builder.AppendLine($"Package: {Path.GetFileName(asset.PackagePath)}");
                builder.AppendLine($"Package path: {asset.PackagePath}");
                builder.AppendLine($"Resource bytes: {asset.Data.Length:N0}");
                builder.AppendLine($"Mesh decode: {(asset.Model is null ? "FAILED" : "OK")}");
                if (!string.IsNullOrWhiteSpace(asset.DecodeError))
                    builder.AppendLine($"Decode error: {asset.DecodeError}");

                try
                {
                    var rcol = VfxRcolDecoder.Decode(asset.Data);
                    builder.AppendLine($"RCOL version: 0x{rcol.Version:X8} • public chunks {rcol.PublicChunkCount:N0} • external resources {rcol.ExternalResources.Count:N0} • chunks {rcol.Chunks.Count:N0}");
                    foreach (var chunk in rcol.Chunks)
                        builder.AppendLine($"    chunk [{chunk.Index}] {chunk.Tag} • TGI {chunk.Key.Type:X8}:{chunk.Key.Group:X8}:{chunk.Key.Instance:X16} • offset {chunk.Offset:N0} • length {chunk.Length:N0}");
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
                {
                    builder.AppendLine($"RCOL diagnostics failed: {ex.Message}");
                }

                if (asset.Model is not null)
                {
                    builder.AppendLine($"MLOD version: 0x{asset.Model.Version:X8}");
                    builder.AppendLine($"Meshes: {asset.Model.Meshes.Count:N0}");
                    for (var meshIndex = 0; meshIndex < asset.Model.Meshes.Count; meshIndex++)
                    {
                        var mesh = asset.Model.Meshes[meshIndex];
                        builder.AppendLine($"    mesh [{meshIndex}] name 0x{mesh.Name:X8} • primitive {mesh.PrimitiveType} • flags 0x{mesh.MeshFlags:X8} • material ref 0x{mesh.MaterialReference:X8} • vertices {mesh.Vertices.Count:N0} • indices {mesh.Indices.Count:N0} • triangles {mesh.Indices.Count / 3:N0}");
                    }
                }

                VfxModelDiagnostics.Append(builder, asset);
            }
        }

        builder.AppendLine();
        builder.AppendLine("TEXTURES");
        if (result.Textures.Count == 0)
            builder.AppendLine("No decoded image resources.");
        foreach (var texture in result.Textures.Values.OrderBy(texture => texture.Instance))
            builder.AppendLine($"0x{texture.Instance:X16} • type 0x{texture.Type:X8} • group 0x{texture.Group:X8} • {texture.Width}x{texture.Height} • {texture.PackagePath}");

        builder.AppendLine();
        builder.AppendLine("RESOURCE PROBES");
        if (result.ResourceProbes.Count == 0)
            builder.AppendLine("No resource probes.");
        foreach (var probe in result.ResourceProbes.Values.OrderBy(probe => probe.Instance))
        {
            builder.AppendLine($"IID 0x{probe.Instance:X16}");
            if (probe.Candidates.Count == 0)
            {
                builder.AppendLine("    no matching TGI found");
                continue;
            }
            foreach (var candidate in probe.Candidates)
            {
                var decode = string.IsNullOrWhiteSpace(candidate.DecodeError) ? string.Empty : $" • decode error: {candidate.DecodeError}";
                builder.AppendLine($"    type 0x{candidate.Type:X8} • group 0x{candidate.Group:X8} • {candidate.PackagePath}{decode}");
            }
        }
    }

    private static string FormatFloatValues(IReadOnlyList<float> values)
        => values.Count == 0
            ? "—"
            : string.Join(", ", values.Select(value => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)));

    private static string FormatColorValues(IReadOnlyList<VfxParticleColor> values)
        => values.Count == 0
            ? "—"
            : string.Join(", ", values.Select(value => $"({value.Red:0.######}, {value.Green:0.######}, {value.Blue:0.######})"));
}
