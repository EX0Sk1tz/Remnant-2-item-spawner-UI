using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Remnant2UnlockerApp.Services;

namespace Remnant2UnlockerApp.Models;

public sealed class RemnantItem : INotifyPropertyChanged
{
    private string? _imagePath;
    private ImageSource? _image;
    private bool _isImageLoading;
    private bool _isFavorite;

    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";

    public string SummonCommand => $"summon {Path}";

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = value;
            Image = LoadImage(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public ImageSource? Image
    {
        get => _image;
        private set
        {
            _image = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => Image != null;

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            _isFavorite = value;
            OnPropertyChanged();
        }
    }

    public bool IsImageLoading
    {
        get => _isImageLoading;
        set
        {
            _isImageLoading = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static ImageSource? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception)
        {
            // Unreadable or corrupt cache file: show the placeholder instead of failing the item.
            return null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool _isSummonableTraitsInstalled;

    public bool IsTraitEntry =>
        Type.Equals("Trait", StringComparison.OrdinalIgnoreCase)
        || Type.Equals("Core Trait", StringComparison.OrdinalIgnoreCase)
        || Type.Equals("Archetype Trait", StringComparison.OrdinalIgnoreCase);

    public bool IsTraitPoint =>
        Type.Equals("Trait Point", StringComparison.OrdinalIgnoreCase);

    public bool NeedsSummonableTraitsMod => IsTraitEntry && !IsTraitPoint;

    public bool IsSummonableTraitsInstalled
    {
        get => _isSummonableTraitsInstalled;
        set
        {
            if (_isSummonableTraitsInstalled == value)
                return;

            _isSummonableTraitsInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTraitLocked));
            OnPropertyChanged(nameof(HasBadges));
            OnPropertyChanged(nameof(CanUseTraitCommands));
            OnPropertyChanged(nameof(CanUseNormalSpawn));
        }
    }

    private bool _isOwned;

    // Blueprint class name used to match this item against the player's inventory scan.
    public string ClassKey => CollectionTracker.ToClassKey(Path);

    // Null for base-game items; see DlcCatalog.
    public DlcInfo? Dlc => DlcCatalog.FromPath(Path);

    public bool IsDlc => Dlc != null;

    public string DlcBadgeText => Dlc?.ShortName ?? "";

    public string DlcTooltip => Dlc == null ? "" : $"DLC: {Dlc.Name}";

    // Whether the "missing items" view counts this item at all (see CollectionTracker.TrackedTypes).
    public bool IsCollectible => CollectionTracker.IsTrackedType(Type);

    // Set from the latest inventory scan; stays false when no scan has been read yet.
    public bool IsOwned
    {
        get => _isOwned;
        set
        {
            if (_isOwned == value)
                return;

            _isOwned = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBadges));
        }
    }

    // Whether the card shows any badge (trait lock, DLC, owned); the badge line is hidden otherwise.
    public bool HasBadges => IsTraitLocked || IsDlc || IsOwned;

    public bool IsTraitLocked => NeedsSummonableTraitsMod && !IsSummonableTraitsInstalled;

    public bool CanUseTraitCommands => NeedsSummonableTraitsMod && IsSummonableTraitsInstalled;

    public bool CanUseNormalSpawn => !NeedsSummonableTraitsMod;
}