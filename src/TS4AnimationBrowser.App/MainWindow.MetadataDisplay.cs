using System.Windows.Controls;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private void SelectedDetail_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        const string current = "Rig namespace: X (standard Sim actor namespace)";
        const string normalized = "Rig namespace: x (standard Sim actor namespace)";
        if (!textBox.Text.Contains(current, StringComparison.Ordinal))
            return;

        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        textBox.Text = textBox.Text.Replace(current, normalized, StringComparison.Ordinal);
        textBox.SelectionStart = Math.Min(selectionStart, textBox.Text.Length);
        textBox.SelectionLength = Math.Min(selectionLength, textBox.Text.Length - textBox.SelectionStart);
    }
}
