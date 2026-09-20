using System.Windows;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    static MainWindow()
    {
        TitleProperty.OverrideMetadata(
            typeof(MainWindow),
            new FrameworkPropertyMetadata(AppInfo.DisplayName, null, CoerceWindowTitle));
    }

    private static object CoerceWindowTitle(DependencyObject dependencyObject, object baseValue)
        => AppInfo.DisplayName;
}
