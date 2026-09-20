using System.Windows;
using System.Windows.Controls;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private ResourceRow? _activeResourceRow;
    private bool _librarySelectionTrackingAttached;

    private void ResourcesGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_librarySelectionTrackingAttached)
            return;

        ResourcesGrid.SelectionChanged += ResourcesGrid_TrackActiveSelection;
        _librarySelectionTrackingAttached = true;
    }

    private void ResourcesGrid_TrackActiveSelection(object sender, SelectionChangedEventArgs e)
    {
        if (ResourcesGrid.SelectedItem is ResourceRow selected)
            SetActiveResource(selected);
    }

    private void SetActiveResource(ResourceRow selected)
    {
        if (ReferenceEquals(_activeResourceRow, selected))
        {
            selected.SetActive(true);
            return;
        }

        _activeResourceRow?.SetActive(false);
        _activeResourceRow = selected;
        _activeResourceRow.SetActive(true);
    }

    private void ClearActiveResource()
    {
        _activeResourceRow?.SetActive(false);
        _activeResourceRow = null;
    }
}
