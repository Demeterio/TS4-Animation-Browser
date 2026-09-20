using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private const string ReleasesLatestPage = "https://github.com/Demeterio/TS4-Animation-Browser/releases/latest/";
    private const string LatestReleaseApi = "https://api.github.com/repos/Demeterio/TS4-Animation-Browser/releases/latest";

    private bool _startupCacheAttempted;
    private string? _latestReleasePage;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = AppInfo.DisplayName;
        UpdateRuntimeVersionLabels(this);
        EnsureTechnicalDetailsButton();
        _ = CheckForUpdatesAsync();

        if (_startupCacheAttempted)
            return;
        _startupCacheAttempted = true;

        SetStartupCacheLoading(true);

        // Let WPF render the complete window and loading overlay before DPAPI/Brotli work starts.
        // The cache can contain tens of thousands of rows, so decrypt/decompress it off the UI thread.
        await Dispatcher.Yield(DispatcherPriority.Render);
        var cache = await Task.Run(DtabCache.TryLoad);

        if (cache is null)
        {
            SetStartupCacheLoading(false);
            return;
        }

        LoadingText.Text = "Preparing library…";
        LoadingDetail.Text = $"Loading {cache.Resources.Count:N0} cached resources…";
        await Dispatcher.Yield(DispatcherPriority.Render);

        LoadLibraryRows(cache.Resources);
        ViewportMessage.Text = "Select a resource to preview";
        ViewportMessagePanel.Visibility = Visibility.Visible;

        var shadowed = cache.ShadowedResourceCount > 0
            ? $" • {cache.ShadowedResourceCount:N0} older duplicate resource(s) hidden"
            : string.Empty;
        StatusText.Text = $"Loaded library cache • {cache.PackageCount:N0} client packages{shadowed} • {cache.GameRoot}";
        SetStartupCacheLoading(false);
    }

    private void SetStartupCacheLoading(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectGameFolderButton.IsEnabled = !isLoading;
        SearchBox.IsEnabled = !isLoading;
        HideUnknown.IsEnabled = !isLoading;
        SimAnimationsOnly.IsEnabled = !isLoading;
        ObjectAnimationsOnly.IsEnabled = !isLoading;
        VfxOnly.IsEnabled = !isLoading;

        if (!isLoading)
            return;

        LoadingText.Text = "Loading library cache…";
        LoadingDetail.Text = "Decrypting and restoring the previous animation library…";
        LoadingProgress.Minimum = 0;
        LoadingProgress.Maximum = 1;
        LoadingProgress.Value = 0;
    }

    private void LoadLibraryRows(IEnumerable<ResourceRow> rows)
    {
        ClearActiveResource();
        ResourcesGrid.SelectedItem = null;
        _resources = new ObservableCollection<ResourceRow>(rows);
        _view = CollectionViewSource.GetDefaultView(_resources);
        _view.Filter = MatchesFilter;
        ResourcesGrid.ItemsSource = _view;
        RefreshView();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (UpdateStatusButton is null)
            return;

        UpdateStatusButton.Content = "Checking updates…";
        UpdateStatusButton.IsEnabled = false;
        UpdateStatusButton.ToolTip = "Checking whether a newer published version is available.";
        _latestReleasePage = null;

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TS4-Animation-Browser", NormalizeVersion(AppInfo.Version)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await client.GetAsync(LatestReleaseApi);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                SetUpdateStatusUnavailable("No published release is available yet.");
                return;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                SetUpdateStatusUnavailable("The update server returned an unexpected response.");
                return;
            }

            var latestTag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(latestTag)
                || !Version.TryParse(NormalizeVersion(latestTag), out var latestVersion)
                || !Version.TryParse(NormalizeVersion(AppInfo.Version), out var currentVersion))
            {
                SetUpdateStatusUnavailable("The published version number could not be read.");
                return;
            }

            var releasePage = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            _latestReleasePage = string.IsNullOrWhiteSpace(releasePage) ? ReleasesLatestPage : releasePage;

            if (latestVersion > currentVersion)
            {
                UpdateStatusButton.Content = "Update available";
                UpdateStatusButton.IsEnabled = true;
                UpdateStatusButton.ToolTip = $"Version {latestVersion} is available. Click to open the download page.";
                return;
            }

            UpdateStatusButton.Content = "Software up to date";
            UpdateStatusButton.IsEnabled = false;
            UpdateStatusButton.ToolTip = $"You are using the latest published version ({currentVersion}).";
        }
        catch (Exception)
        {
            SetUpdateStatusUnavailable("Could not connect to GitHub to check for updates.");
        }
    }

    private void SetUpdateStatusUnavailable(string tooltip)
    {
        if (UpdateStatusButton is null)
            return;

        UpdateStatusButton.Content = "Update check unavailable";
        UpdateStatusButton.IsEnabled = false;
        UpdateStatusButton.ToolTip = tooltip;
        _latestReleasePage = null;
    }

    private void UpdateStatus_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestReleasePage))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _latestReleasePage,
                UseShellExecute = true
            });
        }
        catch
        {
            UpdateStatusButton.ToolTip = "The release page could not be opened in your default browser.";
        }
    }

    private static string NormalizeVersion(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];
        return normalized;
    }

    private static void UpdateRuntimeVersionLabels(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock textBlock
                && textBlock.Text.Trim().StartsWith("•  v", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Text = $"  •  {AppInfo.Version}";
            }

            UpdateRuntimeVersionLabels(child);
        }
    }
}
