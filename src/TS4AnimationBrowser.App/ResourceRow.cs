using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public enum ResourceVisualCategory
{
    Unknown,
    SimAnimation,
    ObjectAnimation,
    Vfx
}

public sealed class ResourceRow : INotifyPropertyChanged
{
    private ResourceVisualCategory _category;
    private string _name;
    private bool _isActive;

    public ResourceRow(ResourceEntry entry, string packagePath, string gameRoot, string? name = null, bool isRelevant = true)
    {
        Entry = entry;
        PackagePath = packagePath;
        GameRoot = gameRoot;
        Package = Path.GetFileName(packagePath);
        Pack = GetPackName(packagePath, gameRoot);
        _name = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        IsRelevant = isRelevant;
        _category = KnownResourceTypes.IsVfx(entry.Key.Type) ? ResourceVisualCategory.Vfx : ResourceVisualCategory.Unknown;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ResourceEntry Entry { get; }
    public string PackagePath { get; }
    public string GameRoot { get; }
    public string Name => _name;
    public bool IsRelevant { get; }
    public bool IsActive => _isActive;
    public string Pack { get; }
    public string Package { get; }
    public string TypeId => Entry.TypeHex;
    public string Group => Entry.GroupHex;
    public string Instance => Entry.InstanceHex;
    public ResourceVisualCategory Category => _category;

    public string Type => Entry.Key.Type switch
    {
        KnownResourceTypes.Clip when _category == ResourceVisualCategory.SimAnimation => "Sim / Character",
        KnownResourceTypes.Clip when _category == ResourceVisualCategory.ObjectAnimation => "Object",
        KnownResourceTypes.Clip => "Unknown",
        KnownResourceTypes.ClipHeader => "Animation Header",
        KnownResourceTypes.Rig when _category == ResourceVisualCategory.SimAnimation => "Sim / Character RIG",
        KnownResourceTypes.Rig when _category == ResourceVisualCategory.ObjectAnimation => "Object RIG",
        KnownResourceTypes.Rig => "RIG",
        _ when KnownResourceTypes.IsVfx(Entry.Key.Type) => "VFX",
        _ => Entry.TypeName
    };

    public void SetName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        if (string.Equals(value, Instance, StringComparison.OrdinalIgnoreCase))
            value = string.Empty;
        if (string.Equals(_name, value, StringComparison.Ordinal))
            return;

        _name = value;
        OnPropertyChanged(nameof(Name));
    }

    public void SetCategory(ResourceVisualCategory category)
    {
        // A nameless CLIP remains genuinely Unknown. This prevents the preview click from turning a
        // row into Object/Sim merely because a compatible RIG was found after the initial scan.
        if (Entry.Key.Type == KnownResourceTypes.Clip
            && string.IsNullOrWhiteSpace(_name)
            && category is ResourceVisualCategory.SimAnimation or ResourceVisualCategory.ObjectAnimation)
        {
            return;
        }

        if (_category == category)
            return;

        _category = category;
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Type));
    }

    public void SetActive(bool value)
    {
        if (_isActive == value)
            return;
        _isActive = value;
        OnPropertyChanged(nameof(IsActive));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string GetPackName(string packagePath, string gameRoot)
    {
        var parts = Path.GetRelativePath(gameRoot, packagePath)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length == 0)
            return "Base Game";

        if (parts[0].Equals("Delta", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length >= 2 && IsPackFolder(parts[1]))
                return parts[1].ToUpperInvariant();
            return "Base Game";
        }

        if (parts[0].Equals("Data", StringComparison.OrdinalIgnoreCase))
            return "Base Game";

        return parts[0];
    }

    private static bool IsPackFolder(string value)
    {
        if (value.Length < 3)
            return false;

        return (value.StartsWith("EP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("GP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("SP", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("FP", StringComparison.OrdinalIgnoreCase))
            && value[2..].All(char.IsDigit);
    }
}
