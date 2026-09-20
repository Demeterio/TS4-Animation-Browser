using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace TS4AnimationBrowser.App;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            MessageBox.Show(
                this,
                "The link could not be opened in your default web browser.",
                "Could not open link",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
