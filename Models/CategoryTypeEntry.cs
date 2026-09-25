using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Remnant2UnlockerApp.Models;

// One subcategory button in the sidebar. The instances are kept for the app's lifetime (the
// category search only changes which ones are shown), so ProgressText can update in place when a
// new inventory scan arrives.
public sealed class CategoryTypeEntry : INotifyPropertyChanged
{
    private string _progressText = "";

    public CategoryTypeEntry(string type)
    {
        Type = type;
    }

    public string Type { get; }

    // "23/41" for tracked types once an inventory scan exists; empty otherwise.
    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (_progressText == value)
                return;

            _progressText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    public bool HasProgress => ProgressText.Length > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
