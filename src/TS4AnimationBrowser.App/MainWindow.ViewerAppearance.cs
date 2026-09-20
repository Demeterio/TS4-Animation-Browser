using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private ModelVisual3D? _gridSceneVisual;
    private ModelVisual3D? _gridHiddenSceneVisual;
    private Button? _gridVisibilityButton;
    private bool _gridVisible = true;
    private bool _viewerAppearanceControlsInstalled;

    private void InstallViewerAppearanceControls()
    {
        if (_viewerAppearanceControlsInstalled)
            return;

        var cameraPanel = FindCameraControlsPanel(ViewerSurface);
        if (cameraPanel is null)
            return;

        _viewerAppearanceControlsInstalled = true;
        EnsureViewerAppearanceVisuals();

        cameraPanel.Children.Add(new Separator { Margin = new Thickness(0, 7, 0, 5) });

        _gridVisibilityButton = new Button
        {
            Width = 104,
            Height = 27,
            Content = "Grid: On",
            ToolTip = "Show or hide the floor grid."
        };
        _gridVisibilityButton.Click += GridVisibility_Click;
        cameraPanel.Children.Add(_gridVisibilityButton);

        cameraPanel.Children.Add(new TextBlock
        {
            Text = "Environment",
            FontSize = 10,
            Margin = new Thickness(0, 7, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
        });

        var brightnessSlider = new Slider
        {
            Width = 104,
            Height = 22,
            Minimum = 40,
            Maximum = 255,
            Value = 244,
            SmallChange = 5,
            LargeChange = 20,
            ToolTip = "Darken or brighten the viewer environment without changing the grid."
        };
        brightnessSlider.ValueChanged += EnvironmentBrightnessSlider_ValueChanged;
        cameraPanel.Children.Add(brightnessSlider);
    }

    private static StackPanel? FindCameraControlsPanel(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is StackPanel panel && ContainsResetCameraButton(panel))
                return panel;

            var nested = FindCameraControlsPanel(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static bool ContainsResetCameraButton(StackPanel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is Button button && string.Equals(button.Content?.ToString(), "Reset Camera", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void GridVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureViewerAppearanceVisuals())
            return;

        var currentVisual = _gridVisible ? _gridSceneVisual : _gridHiddenSceneVisual;
        var replacementVisual = _gridVisible ? _gridHiddenSceneVisual : _gridSceneVisual;
        if (currentVisual is null || replacementVisual is null)
            return;

        var index = AnimationViewport.Children.IndexOf(currentVisual);
        if (index < 0)
            return;

        AnimationViewport.Children.Remove(currentVisual);
        AnimationViewport.Children.Insert(index, replacementVisual);

        _gridVisible = !_gridVisible;
        _vfxParticlePreview?.SetGridVisible(_gridVisible);
        if (_gridVisibilityButton is not null)
            _gridVisibilityButton.Content = _gridVisible ? "Grid: On" : "Grid: Off";
    }

    private void EnvironmentBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var brightness = (byte)Math.Clamp((int)Math.Round(e.NewValue), 0, 255);
        var color = Color.FromRgb(brightness, brightness, brightness);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        ViewerSurface.Background = brush;
        _vfxParticlePreview?.SetBackgroundColor(color);
    }

    private bool EnsureViewerAppearanceVisuals()
    {
        if (_gridSceneVisual is null)
        {
            if (AnimationViewport.Children.Count == 0 || AnimationViewport.Children[0] is not ModelVisual3D sceneVisual)
                return false;

            _gridSceneVisual = sceneVisual;
        }

        _gridHiddenSceneVisual ??= CreateGridHiddenSceneVisual();
        return true;
    }

    private static ModelVisual3D CreateGridHiddenSceneVisual()
    {
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(175, 175, 175)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.5, -1, -1)));
        return new ModelVisual3D { Content = scene };
    }
}
