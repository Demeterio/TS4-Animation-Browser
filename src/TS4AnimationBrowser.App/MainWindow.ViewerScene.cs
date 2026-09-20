using System.Windows.Media.Media3D;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private bool _viewerSceneInitialized;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_viewerSceneInitialized)
            return;

        _viewerSceneInitialized = true;
        RemoveWorldAxes();
        ApplyDefaultViewerCamera();
        InstallViewerAppearanceControls();
        InstallExplicitSelectionGate();
    }

    private void RemoveWorldAxes()
    {
        var sceneVisual = AnimationViewport.Children
            .OfType<ModelVisual3D>()
            .FirstOrDefault(visual => visual.Content is Model3DGroup);
        if (sceneVisual?.Content is not Model3DGroup scene)
            return;

        // BuildPreviewScene appends X/Y/Z as its final three models.
        for (var i = 0; i < 3 && scene.Children.Count > 0; i++)
            scene.Children.RemoveAt(scene.Children.Count - 1);
    }
}
