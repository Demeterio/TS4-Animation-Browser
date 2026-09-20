using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace TS4AnimationBrowser.App;

internal sealed class ResourceDiagnosticsWindow : Window
{
    private const int ClipboardCannotOpen = unchecked((int)0x800401D0);
    private readonly TextBox _details;
    private readonly string _resourceName;

    public ResourceDiagnosticsWindow(string resourceName, string details)
    {
        _resourceName = resourceName;
        Title = $"Technical details — {resourceName}";
        Width = 920;
        Height = 720;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        Foreground = Brushes.Black;

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _details = new TextBox
        {
            Text = details,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(10),
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
        Grid.SetRow(_details, 0);
        root.Children.Add(_details);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var copyButton = new Button
        {
            Content = "Copy all",
            MinWidth = 100,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0)
        };
        copyButton.Click += async (_, _) => await CopyAllAsync(copyButton);

        var saveButton = new Button
        {
            Content = "Save diagnostics…",
            MinWidth = 130,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Save the complete technical report as a UTF-8 text file."
        };
        saveButton.Click += (_, _) => SaveDiagnostics();

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 90,
            Height = 30,
            IsCancel = true
        };
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(copyButton);
        buttons.Children.Add(saveButton);
        buttons.Children.Add(closeButton);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) => _details.Focus();
    }

    private async Task CopyAllAsync(Button copyButton)
    {
        if (string.IsNullOrEmpty(_details.Text))
            return;

        copyButton.IsEnabled = false;
        copyButton.Content = "Copying…";
        var selectionStart = _details.SelectionStart;
        var selectionLength = _details.SelectionLength;

        try
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    _details.Focus();
                    _details.SelectAll();
                    _details.Copy();
                    copyButton.Content = "Copied";
                    await Task.Delay(700);
                    return;
                }
                catch (COMException ex) when (ex.HResult == ClipboardCannotOpen)
                {
                    if (attempt == 7)
                        break;
                    await Task.Delay(35 + (attempt * 20));
                }
            }

            MessageBox.Show(
                this,
                "Windows still refused the clipboard operation. This is not an application permission requirement: the report remains selectable for Ctrl+C, and you can also use Save diagnostics… to write it directly to a text file.",
                "Clipboard unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            _details.Select(selectionStart, selectionLength);
            copyButton.Content = "Copy all";
            copyButton.IsEnabled = true;
        }
    }

    private void SaveDiagnostics()
    {
        var safeName = string.Concat(_resourceName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "resource";

        var dialog = new SaveFileDialog
        {
            Title = "Save technical diagnostics",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"{safeName}_diagnostics.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, _details.Text, new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not save diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
