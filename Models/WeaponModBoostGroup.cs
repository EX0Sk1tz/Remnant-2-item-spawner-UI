using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Remnant2UnlockerApp.Models;

public sealed class WeaponModBoostField
{
    public WeaponModBoostField(string key, double value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }

    public double Value { get; set; }
}

public sealed class WeaponModBoostGroup
{
    public WeaponModBoostGroup(string key, string displayName, string consoleCommand)
    {
        Key = key;
        DisplayName = displayName;
        ConsoleCommand = consoleCommand;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string ConsoleCommand { get; }

    public ObservableCollection<WeaponModBoostField> Fields { get; } = new();

    public ICommand? ApplyCommand { get; set; }

    public bool IsExpanded { get; set; }
}
