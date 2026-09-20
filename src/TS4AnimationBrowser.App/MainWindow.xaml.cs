using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using TS4AnimationBrowser.Core.Animation;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public partial class MainWindow : Window
{
    private ObservableCollection<ResourceRow> _resources = [];
    private ICollectionView _view;
    private readonly PlaybackController _playback = new();
    private readonly SkeletonPreview _skeletonPreview;
    private ClipAnimation? _currentClip;
    private TimeSpan? _lastRenderingTime;
    private bool _updatingTimeline;
    private int _selectionVersion;

    private Point3D _cameraTarget = new(0, 0.85, 0);
    private double _cameraYaw = -Math.PI / 4;
    private double _cameraPitch = 0.26;
    private double _cameraDistance = 4.8;
    private Point _lastViewerMousePosition;
    private bool _orbitingCamera;
    private bool _panningCamera;

    public MainWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_resources);
        _view.Filter = MatchesFilter;
        ResourcesGrid.ItemsSource = _view;

        BuildPreviewScene();
        _skeletonPreview = new SkeletonPreview(AnimationViewport);
        ApplyDefaultViewerCamera();
        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    private async void SelectGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the main The Sims 4 installation folder",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        SetLoading(true, "Scanning The Sims 4…");
        StatusText.Text = dialog.FolderName;
        CountText.Text = string.Empty;

        try
        {
            var progress = new Progress<ScanProgress>(scan =>
            {
                LoadingText.Text = scan.Stage switch
                {
                    "Scanning packages" => "Scanning game packages…",
                    "Resolving patched resources" => "Resolving patched resources…",
                    "Indexing VFX" => "Indexing VFX…",
                    "Classifying RIGs" => "Classifying animation rigs…",
                    "Classifying animations" => "Classifying animations…",
                    "Sorting library" => "Preparing animation library…",
                    _ => scan.Stage
                };

                LoadingDetail.Text = scan.Stage switch
                {
                    "Scanning packages" => $"Package {scan.Current:N0} / {scan.Total:N0} • {scan.ResourceCount:N0} resources discovered",
                    "Resolving patched resources" => scan.Current >= scan.Total
                        ? $"{scan.ResourceCount:N0} effective resources ready"
                        : $"Comparing {scan.ResourceCount:N0} discovered resources",
                    "Indexing VFX" => $"VFX {scan.Current:N0} / {scan.Total:N0}",
                    "Classifying RIGs" => $"RIG {scan.Current:N0} / {scan.Total:N0}",
                    "Classifying animations" => $"Animation {scan.Current:N0} / {scan.Total:N0}",
                    "Sorting library" => scan.Current >= scan.Total
                        ? $"{scan.ResourceCount:N0} resources ready"
                        : $"Sorting {scan.ResourceCount:N0} resources",
                    _ => $"{scan.Current:N0} / {scan.Total:N0}"
                };

                LoadingProgress.Maximum = Math.Max(1, scan.Total);
                LoadingProgress.Value = Math.Min(scan.Current, LoadingProgress.Maximum);
            });

            var result = await Task.Run(() => InstallationScanner.Scan(dialog.FolderName, progress));
            LoadLibraryRows(result.Resources);

            // Brotli + DPAPI can take a noticeable moment for the full game library. Keep that work
            // off the WPF thread and show a real stage instead of appearing frozen on "Preparing".
            LoadingText.Text = "Saving encrypted library cache…";
            LoadingDetail.Text = $"Protecting {result.Resources.Count:N0} indexed resources for {AppInfo.Version}…";
            LoadingProgress.Minimum = 0;
            LoadingProgress.Maximum = 1;
            LoadingProgress.Value = 1;
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            var cacheSaved = await Task.Run(() => DtabCache.TrySave(dialog.FolderName, result));

            Title = AppInfo.DisplayName;
            ViewportMessage.Text = "Select a resource to preview";
            ViewportMessagePanel.Visibility = Visibility.Visible;

            var shadowed = result.ShadowedResourceCount > 0
                ? $" • {result.ShadowedResourceCount:N0} older duplicate resource(s) hidden"
                : string.Empty;
            var cacheStatus = cacheSaved ? " • cache updated" : " • cache could not be written";
            StatusText.Text = result.ErrorCount == 0
                ? $"Scanned {result.PackageCount:N0} client packages{shadowed}{cacheStatus} • {dialog.FolderName}"
                : $"Scanned {result.PackageCount:N0} client packages • {result.ErrorCount:N0} skipped package(s){shadowed}{cacheStatus} • {dialog.FolderName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not scan The Sims 4", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Scan failed.";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void ResourcesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectionVersion = ++_selectionVersion;
        if (ResourcesGrid.SelectedItem is ResourceRow selected)
            SetActiveResource(selected);

        ResetPreviewState();

        if (ResourcesGrid.SelectedItem is not ResourceRow row)
        {
            SelectedTitle.Text = "No resource selected";
            SelectedDetail.Text = "Select an animation or VFX resource from the list below.";
            ViewportMessage.Text = "Select a resource to preview";
            ViewportMessagePanel.Visibility = Visibility.Visible;
            SetViewerLoading(false);
            return;
        }

        SelectedTitle.Text = string.IsNullOrWhiteSpace(row.Name) ? row.Instance : row.Name;
        SelectedDetail.Text = BuildResourceIdentity(row);

        if (row.Entry.Key.Type == KnownResourceTypes.Clip)
        {
            ViewportMessage.Text = "Decoding animation…";
            ViewportMessagePanel.Visibility = Visibility.Visible;
            SetViewerLoading(true, "Decoding animation…", "Reading the selected CLIP…");

            try
            {
                var snapshot = _resources.ToArray();
                var decodeProgress = new Progress<AnimationLoadProgress>(load =>
                {
                    if (selectionVersion != _selectionVersion)
                        return;

                    var detail = load.Total > 0
                        ? $"Checking RIG {load.Current:N0} / {load.Total:N0}…"
                        : "Reading animation data…";
                    SetViewerLoading(true, load.Stage, detail);
                });

                var result = await Task.Run(() => AnimationResourceLoader.LoadClip(row, snapshot, decodeProgress));
                if (selectionVersion != _selectionVersion)
                    return;

                _currentClip = result.Clip;
                _playback.Load(result.Clip.EffectiveDurationSeconds);
                if (result.Category != ResourceVisualCategory.Unknown)
                {
                    row.SetCategory(result.Category);
                    result.RigRow?.SetCategory(result.Category);
                }

                var displayName = FirstNonEmpty(result.Clip.Name, result.Clip.CodecAnimationName, row.Name, row.Instance);
                row.SetName(displayName);
                RefreshView();
                SelectedTitle.Text = displayName;
                SelectedDetail.Text = BuildClipDetail(row, result);

                if (result.Rig is not null)
                {
                    _skeletonPreview.Load(result.Rig, result.Clip, result.Category);
                    _skeletonPreview.Update(0);
                    if (_skeletonPreview.FramingBounds is { } framingBounds)
                        FramePreview(framingBounds);
                    else if (_skeletonPreview.CurrentBounds is { } currentBounds)
                        FramePreview(currentBounds);
                    else
                        ApplyDefaultViewerCamera();
                    ViewportMessagePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ApplyDefaultViewerCamera();
                    ViewportMessage.Text = result.Clip.RigInstance != 0
                        ? $"CLIP references RIG 0x{result.Clip.RigInstance:X16}, but that exact RIG was not found or could not be decoded. No substitute RIG will be used."
                        : result.MatchingTracks > 0
                            ? $"Legacy CLIP decoded, but the best RIG candidate matched only {result.MatchingTracks:N0}/{result.Clip.Tracks.Count:N0} tracks. The match was rejected instead of drawing an unreliable skeleton."
                            : "Legacy CLIP decoded, but no compatible decoded RIG was found. The timeline is available, but no skeleton will be drawn.";
                    ViewportMessagePanel.Visibility = Visibility.Visible;
                }

                UpdatePlaybackUi();
            }
            catch (Exception ex)
            {
                if (selectionVersion != _selectionVersion)
                    return;
                SelectedDetail.Text = BuildResourceIdentity(row);
                ViewportMessage.Text = $"Could not decode this CLIP: {ex.Message}";
                ViewportMessagePanel.Visibility = Visibility.Visible;
            }
            finally
            {
                if (selectionVersion == _selectionVersion)
                    SetViewerLoading(false);
            }
            return;
        }

        ViewportMessage.Text = KnownResourceTypes.IsVfx(row.Entry.Key.Type)
            ? "VFX selected — effect decoding/rendering is the next independent viewer pipeline."
            : "Preview is not available for this resource yet.";
        ViewportMessagePanel.Visibility = Visibility.Visible;
    }

    private static string BuildResourceIdentity(ResourceRow row)
    {
        var name = string.IsNullOrWhiteSpace(row.Name) ? "—" : row.Name;
        return $"Name: {name}\nInstance: {row.Instance}\nType: {row.TypeId} • Group: {row.Group}\nSource: {row.Pack} • {row.Package}";
    }

    private static string BuildClipDetail(ResourceRow row, AnimationLoadResult result)
    {
        var clip = result.Clip;
        var category = result.Category switch
        {
            ResourceVisualCategory.SimAnimation => "Sim / Character",
            ResourceVisualCategory.ObjectAnimation => "Object",
            _ => "Unknown"
        };
        var rigNamespace = FormatRigNamespace(clip.RigNamespace);
        var rigInstance = result.RigRow?.Instance
            ?? (clip.RigInstance == 0 ? "—" : $"0x{clip.RigInstance:X16}");
        var rigSource = result.RigRow is null
            ? "—"
            : $"{result.RigRow.Pack} • {result.RigRow.Package}";
        var rigInfo = result.Rig is null
            ? result.MatchingTracks > 0
                ? $"RIG unavailable • {result.MatchingTracks:N0}/{clip.Tracks.Count:N0} tracks matched"
                : "RIG unavailable"
            : $"{result.Rig.Bones.Count:N0} bones • {result.MatchingTracks:N0}/{clip.Tracks.Count:N0} tracks matched";

        return $"{category}\nCLIP Instance: {row.Instance}\nType: {row.TypeId} • Group: {row.Group}\nCLIP Source: {row.Pack} • {row.Package}\nDuration: {clip.EffectiveDurationSeconds:0.###} s • Frames: {clip.MaxFrameCount:N0} • Tracks: {clip.Tracks.Count:N0}\nRig namespace: {rigNamespace}\nRIG instance: {rigInstance}\nRIG source: {rigSource}\n{rigInfo}";
    }

    private static string FormatRigNamespace(string value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private void ResetPreviewState()
    {
        _currentClip = null;
        _playback.Clear();
        _skeletonPreview.Clear();
        UpdatePlaybackUi();
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_playback.HasAnimation)
            return;

        if (_playback.IsPlaying)
            _playback.Pause();
        else
            _playback.Play();

        UpdatePlaybackUi();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _playback.Stop();
        _skeletonPreview.Update(0);
        UpdatePlaybackUi();
    }

    private void LoopChanged(object sender, RoutedEventArgs e)
    {
        _playback.Loop = LoopCheckBox.IsChecked == true;
    }

    private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedComboBox.SelectedItem is ComboBoxItem item
            && double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            _playback.Speed = speed;
        }
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingTimeline || !_playback.HasAnimation)
            return;

        _playback.Seek(e.NewValue);
        _skeletonPreview.Update(_playback.PositionSeconds);
        UpdatePlaybackUi();
    }

    private void ResetCamera_Click(object sender, RoutedEventArgs e) => ApplyDefaultViewerCamera();

    private void ResetCamera() => ApplyDefaultViewerCamera();

    private void Viewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsViewerInteractionControl(e.OriginalSource))
            return;
        _orbitingCamera = true;
        _lastViewerMousePosition = e.GetPosition(ViewerSurface);
        ViewerSurface.CaptureMouse();
        e.Handled = true;
    }

    private void Viewer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsViewerInteractionControl(e.OriginalSource))
            return;
        _panningCamera = true;
        _lastViewerMousePosition = e.GetPosition(ViewerSurface);
        ViewerSurface.CaptureMouse();
        e.Handled = true;
    }

    private static bool IsViewerInteractionControl(object? source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is TextBox or Button or ScrollBar)
                return true;

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private void Viewer_MouseButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            _orbitingCamera = false;
        if (e.ChangedButton == MouseButton.Right)
            _panningCamera = false;

        if (!_orbitingCamera && !_panningCamera)
            ViewerSurface.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Viewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_orbitingCamera && !_panningCamera)
            return;

        var current = e.GetPosition(ViewerSurface);
        var dx = current.X - _lastViewerMousePosition.X;
        var dy = current.Y - _lastViewerMousePosition.Y;
        _lastViewerMousePosition = current;

        if (_orbitingCamera)
        {
            _cameraYaw -= dx * 0.01;
            _cameraPitch = Math.Clamp(_cameraPitch + dy * 0.01, -1.45, 1.45);
            UpdateCamera();
        }
        else if (_panningCamera)
        {
            PanCamera(dx, dy);
        }
    }

    private void Viewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsViewerInteractionControl(e.OriginalSource))
            return;

        var steps = e.Delta / 120.0;
        _cameraDistance *= Math.Pow(0.86, steps);
        _cameraDistance = Math.Clamp(_cameraDistance, 0.25, 80.0);
        UpdateCamera();
        e.Handled = true;
    }

    private void PanCamera(double dx, double dy)
    {
        var look = ViewportCamera.LookDirection;
        if (look.LengthSquared < 0.000001)
            return;
        look.Normalize();

        var right = Vector3D.CrossProduct(look, ViewportCamera.UpDirection);
        if (right.LengthSquared < 0.000001)
            return;
        right.Normalize();

        var up = Vector3D.CrossProduct(right, look);
        up.Normalize();
        var scale = Math.Max(0.0005, _cameraDistance * 0.0018);
        var shift = right * (-dx * scale) + up * (dy * scale);
        _cameraTarget += shift;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var cosPitch = Math.Cos(_cameraPitch);
        var offset = new Vector3D(
            Math.Sin(_cameraYaw) * cosPitch,
            Math.Sin(_cameraPitch),
            Math.Cos(_cameraYaw) * cosPitch);
        offset *= _cameraDistance;

        ViewportCamera.Position = _cameraTarget + offset;
        ViewportCamera.LookDirection = _cameraTarget - ViewportCamera.Position;
        ViewportCamera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs renderingArgs)
            return;

        if (_lastRenderingTime is null)
        {
            _lastRenderingTime = renderingArgs.RenderingTime;
            return;
        }

        var elapsed = renderingArgs.RenderingTime - _lastRenderingTime.Value;
        _lastRenderingTime = renderingArgs.RenderingTime;

        if (!_playback.IsPlaying)
            return;

        _playback.Tick(elapsed.TotalSeconds);
        _skeletonPreview.Update(_playback.PositionSeconds);
        UpdatePlaybackUi();
    }

    private void UpdatePlaybackUi()
    {
        var enabled = _playback.HasAnimation;
        PlayPauseButton.IsEnabled = enabled;
        StopButton.IsEnabled = enabled;
        TimelineSlider.IsEnabled = enabled;
        PlayPauseButton.Content = _playback.IsPlaying ? "Pause" : "Play";

        _updatingTimeline = true;
        TimelineSlider.Maximum = Math.Max(1, _playback.DurationSeconds);
        TimelineSlider.Value = _playback.PositionSeconds;
        _updatingTimeline = false;

        TimeText.Text = $"{FormatTime(_playback.PositionSeconds)} / {FormatTime(_playback.DurationSeconds)}";
    }

    private static string FormatTime(double seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}";
    }

    private void BuildPreviewScene()
    {
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(175, 175, 175)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.5, -1, -1)));

        const int gridExtent = 6;
        const double gridSpacing = 0.5;
        const double gridThickness = 0.004;
        var gridSize = gridExtent * gridSpacing * 2;
        var gridColor = Color.FromArgb(62, 175, 175, 175);

        scene.Children.Add(CreateBox(
            new Point3D(0, -0.008, 0),
            new Vector3D(gridSize, 0.003, gridSize),
            Color.FromArgb(16, 205, 205, 205)));

        for (var i = -gridExtent; i <= gridExtent; i++)
        {
            var offset = i * gridSpacing;
            scene.Children.Add(CreateBox(new Point3D(offset, 0, 0), new Vector3D(gridThickness, gridThickness, gridSize), gridColor));
            scene.Children.Add(CreateBox(new Point3D(0, 0, offset), new Vector3D(gridSize, gridThickness, gridThickness), gridColor));
        }

        scene.Children.Add(CreateBox(new Point3D(0.6, 0.012, 0), new Vector3D(1.2, 0.018, 0.018), Color.FromArgb(120, 190, 70, 70)));
        scene.Children.Add(CreateBox(new Point3D(0, 0.65, 0), new Vector3D(0.018, 1.3, 0.018), Color.FromArgb(120, 70, 155, 90)));
        scene.Children.Add(CreateBox(new Point3D(0, 0.012, 0.6), new Vector3D(0.018, 0.018, 1.2), Color.FromArgb(120, 70, 105, 190)));

        AnimationViewport.Children.Add(new ModelVisual3D { Content = scene });
    }

    private static GeometryModel3D CreateBox(Point3D center, Vector3D size, Color color)
    {
        var hx = size.X / 2;
        var hy = size.Y / 2;
        var hz = size.Z / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(center.X - hx, center.Y - hy, center.Z - hz),
                new(center.X + hx, center.Y - hy, center.Z - hz),
                new(center.X + hx, center.Y + hy, center.Z - hz),
                new(center.X - hx, center.Y + hy, center.Z - hz),
                new(center.X - hx, center.Y - hy, center.Z + hz),
                new(center.X + hx, center.Y - hy, center.Z + hz),
                new(center.X + hx, center.Y + hy, center.Z + hz),
                new(center.X - hx, center.Y + hy, center.Z + hz)
            },
            TriangleIndices = new Int32Collection
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                2,3,7, 2,7,6,
                0,4,7, 0,7,3,
                1,2,6, 1,6,5
            }
        };

        var material = new DiffuseMaterial(new SolidColorBrush(color));
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshView();
    private void FilterChanged(object sender, RoutedEventArgs e) => RefreshView();

    private void RefreshView()
    {
        _view.Refresh();
        var libraryTotal = _resources.Count(row => IsLibraryResource(row.Entry.Key.Type));
        CountText.Text = $"{_view.Cast<object>().Count():N0} / {libraryTotal:N0} library resources";
    }

    private bool MatchesFilter(object item)
    {
        if (item is not ResourceRow row)
            return false;
        if (!IsLibraryResource(row.Entry.Key.Type))
            return false;

        if (HideUnknown.IsChecked == true
            && row.Entry.Key.Type == KnownResourceTypes.Clip
            && row.Category == ResourceVisualCategory.Unknown)
        {
            return false;
        }

        var simFilter = SimAnimationsOnly.IsChecked == true;
        var objectFilter = ObjectAnimationsOnly.IsChecked == true;
        var vfxFilter = VfxOnly.IsChecked == true;

        if (simFilter || objectFilter || vfxFilter)
        {
            var matchesCategory = KnownResourceTypes.IsVfx(row.Entry.Key.Type)
                ? vfxFilter
                : row.Entry.Key.Type == KnownResourceTypes.Clip && row.Category switch
                {
                    ResourceVisualCategory.SimAnimation => simFilter,
                    ResourceVisualCategory.ObjectAnimation => objectFilter,
                    _ => false
                };

            if (!matchesCategory)
                return false;
        }

        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
            return true;

        return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Type.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.TypeId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Instance.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Pack.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Package.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLibraryResource(uint type)
        => type == KnownResourceTypes.Clip || KnownResourceTypes.IsVfx(type);

    private void SetViewerLoading(bool isLoading, string? title = null, string? detail = null)
    {
        ViewportLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (!isLoading)
            return;

        ViewportLoadingText.Text = title ?? "Decoding…";
        ViewportLoadingDetail.Text = detail ?? "Reading resource data…";
    }

    private void SetLoading(bool isLoading, string? message = null)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectGameFolderButton.IsEnabled = !isLoading;
        SearchBox.IsEnabled = !isLoading;
        HideUnknown.IsEnabled = !isLoading;
        SimAnimationsOnly.IsEnabled = !isLoading;
        ObjectAnimationsOnly.IsEnabled = !isLoading;
        VfxOnly.IsEnabled = !isLoading;

        if (isLoading)
        {
            LoadingText.Text = message ?? "Loading…";
            LoadingDetail.Text = "Finding packages, resolving effective resources and classifying animation RIGs…";
            LoadingProgress.Minimum = 0;
            LoadingProgress.Maximum = 1;
            LoadingProgress.Value = 0;
        }
    }
}
