using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private ResourceRow? _clickedResourceRow;
    private bool _explicitSelectionGateInstalled;

    private void InstallExplicitSelectionGate()
    {
        if (_explicitSelectionGateInstalled)
            return;

        _explicitSelectionGateInstalled = true;

        // XAML wires the CLIP handler directly and LibraryControls wires the VFX handler later.
        // Route both through one click gate so CollectionView refreshes cannot load the viewer.
        ResourcesGrid.SelectionChanged -= ResourcesGrid_SelectionChanged;
        ResourcesGrid.SelectionChanged -= VfxSelectionPipeline_SelectionChanged;
        _vfxSelectionPipelineAttached = true;

        ResourcesGrid.PreviewMouseLeftButtonDown += ResourcesGrid_PreviewMouseLeftButtonDown;
        ResourcesGrid.SelectionChanged += ResourcesGrid_ExplicitSelectionChanged;
        SearchBox.TextChanged += SearchBox_ClearViewerOnTextChanged;
    }

    private void ResourcesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var row = source is null
            ? null
            : ItemsControl.ContainerFromElement(ResourcesGrid, source) as DataGridRow;
        _clickedResourceRow = row?.Item as ResourceRow;
    }

    private void ResourcesGrid_ExplicitSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResourcesGrid.SelectedItem is not ResourceRow row
            || _clickedResourceRow is null
            || !ReferenceEquals(row, _clickedResourceRow))
        {
            return;
        }

        _clickedResourceRow = null;
        ResourcesGrid_SelectionChanged(sender, e);
        VfxSelectionPipeline_SelectionChanged(sender, e);
    }

    private void SearchBox_ClearViewerOnTextChanged(object sender, TextChangedEventArgs e)
    {
        _clickedResourceRow = null;
        _selectionVersion++;

        ResourcesGrid.UnselectAll();
        ResetPreviewState();
        ClearVfxParticlePreview();

        SelectedTitle.Text = "No resource selected";
        SelectedDetail.Text = "Click a resource in the filtered list to load its preview.";
        ViewportMessage.Text = "Search results updated — click a resource to preview it";
        ViewportMessagePanel.Visibility = Visibility.Visible;
        SetViewerLoading(false);
    }
}
