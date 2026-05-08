using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Forms = System.Windows.Forms;

namespace Remnant2UnlockerApp.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> WikiLowercaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "of",
        "the",
        "and",
        "a",
        "an",
        "in",
        "on",
        "to",
        "for",
        "with",
        "from"
    };

    private readonly GamePathService _pathService;
    private readonly ItemRepository _itemRepository;
    private readonly QueueWriter _queueWriter;
    private readonly WikiImageService _wikiImageService;
    private readonly ConsoleSpawnService _consoleSpawnService;
    private readonly HotkeySettingsService _hotkeySettingsService;

    private List<RemnantItem> _allItems = new();
    private string _searchText = "";
    private string _selectedGroup = "All";
    private string _selectedType = "All";
    private string _selectedWiki = "wiki.gg";
    private RemnantItem? _selectedItem;
    private string _statusText = "Ready";

    private bool _alwaysOnTop;
    private bool _isCapturingTeleportHotkey;
    private bool _isCapturingConsoleKey;
    private string _teleportHotkey = "F6";
    private string _consoleKey = "F10";

    public MainViewModel()
    {
        _pathService = new GamePathService();

        _itemRepository = new ItemRepository(_pathService);
        _queueWriter = new QueueWriter(_pathService);
        _wikiImageService = new WikiImageService();
        _consoleSpawnService = new ConsoleSpawnService();
        _hotkeySettingsService = new HotkeySettingsService(_pathService);

        var settings = _hotkeySettingsService.Load();

        _alwaysOnTop = settings.AlwaysOnTop;
        _teleportHotkey = string.IsNullOrWhiteSpace(settings.Teleport) ? "F6" : settings.Teleport;
        _consoleKey = string.IsNullOrWhiteSpace(settings.ConsoleKey) ? "F10" : settings.ConsoleKey;
        _selectedWiki = string.IsNullOrWhiteSpace(settings.Wiki) ? "wiki.gg" : settings.Wiki;

        CategoryGroups = new ObservableCollection<CategoryGroup>
        {
            new() { Name = "Weapons", Types = new List<string> { "Bow", "Handgun", "Long Gun", "Melee" } },
            new() { Name = "Armor", Types = new List<string> { "Body", "Gloves", "Head", "Legs" } },
            new() { Name = "Accessories", Types = new List<string> { "Amulet", "Ring" } },
            new() { Name = "Traits", Types = new List<string> { "Archetype Trait", "Core Trait", "Trait" } },
            new() { Name = "Items", Types = new List<string> { "Concoction", "Consumable", "Curative", "Grenade", "Relic" } },
            new() { Name = "Materials", Types = new List<string> { "Crafting Material", "Currency", "Engram Material", "Material", "Upgrade Material" } },
            new() { Name = "Other", Types = new List<string> { "Mutator", "Prism Fragment", "Special", "Trait Point" } }
        };

        SelectTypeCommand = new RelayCommand<string>(SelectType);
        UnlockGroupCommand = new RelayCommand(async () => await UnlockGroupAsync());
        ReloadCommand = new RelayCommand(async () => await LoadAsync());
        SelectGamePathCommand = new RelayCommand(async () => await SelectGamePathAsync());
        StartTeleportHotkeyCaptureCommand = new RelayCommand(StartTeleportHotkeyCapture);
        StartConsoleKeyCaptureCommand = new RelayCommand(StartConsoleKeyCapture);

        RefreshPathState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RemnantItem> Items { get; } = new();

    public ObservableCollection<CategoryGroup> CategoryGroups { get; }

    public RelayCommand<string> SelectTypeCommand { get; }

    public RelayCommand UnlockGroupCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand SelectGamePathCommand { get; }

    public RelayCommand StartTeleportHotkeyCaptureCommand { get; }

    public RelayCommand StartConsoleKeyCaptureCommand { get; }

    public List<string> WikiOptions { get; } = new()
    {
        "wiki.gg",
        "Fextralife"
    };

    public string SelectedWiki
    {
        get => _selectedWiki;
        set
        {
            _selectedWiki = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWikiGgSelected));
            OnPropertyChanged(nameof(IsFextralifeSelected));
            SaveHotkeySettings();
        }
    }

    public bool IsWikiGgSelected
    {
        get => SelectedWiki == "wiki.gg";
        set
        {
            if (value)
                SelectedWiki = "wiki.gg";

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFextralifeSelected));
        }
    }

    public bool IsFextralifeSelected
    {
        get => SelectedWiki == "Fextralife";
        set
        {
            if (value)
                SelectedWiki = "Fextralife";

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWikiGgSelected));
        }
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            _alwaysOnTop = value;
            OnPropertyChanged();
            SaveHotkeySettings();
            StatusText = AlwaysOnTop ? "Always on top enabled" : "Always on top disabled";
        }
    }

    public bool IsCapturingTeleportHotkey
    {
        get => _isCapturingTeleportHotkey;
        set
        {
            _isCapturingTeleportHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TeleportHotkeyDisplay));
        }
    }

    public bool IsCapturingConsoleKey
    {
        get => _isCapturingConsoleKey;
        set
        {
            _isCapturingConsoleKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConsoleKeyDisplay));
        }
    }

    public string TeleportHotkey
    {
        get => _teleportHotkey;
        set
        {
            _teleportHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TeleportHotkeyDisplay));
        }
    }

    public string ConsoleKey
    {
        get => _consoleKey;
        set
        {
            _consoleKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConsoleKeyDisplay));
        }
    }

    public string TeleportHotkeyDisplay => IsCapturingTeleportHotkey
        ? "Press key..."
        : TeleportHotkey;

    public string ConsoleKeyDisplay => IsCapturingConsoleKey
        ? "Press key..."
        : ConsoleKey;

    public bool IsGamePathValid => _pathService.IsConfigured;

    public string GamePathStatus => IsGamePathValid
        ? "Game path configured"
        : "Game path not configured";

    public string GamePathDisplay => string.IsNullOrWhiteSpace(_pathService.Win64Path)
        ? "Select Remnant 2 Win64 folder"
        : _pathService.Win64Path;

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            _selectedGroup = value;
            OnPropertyChanged();
        }
    }

    public string SelectedType
    {
        get => _selectedType;
        set
        {
            _selectedType = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public RemnantItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            RefreshPathState();

            if (!IsGamePathValid)
            {
                Items.Clear();
                StatusText = "Select the Remnant 2 Win64 folder first";
                return;
            }

            StatusText = "Loading items...";

            _allItems = await _itemRepository.LoadItemsAsync();

            ApplyFilter();

            _ = LoadImagesInBackgroundAsync();

            await _queueWriter.ReloadItemsAsync();

            StatusText = $"Loaded {_allItems.Count} items";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    private async Task SelectGamePathAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select Remnant2\\Remnant2\\Binaries\\Win64",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(_pathService.Win64Path)
            && Directory.Exists(_pathService.Win64Path))
        {
            dialog.SelectedPath = _pathService.Win64Path;
        }

        var result = dialog.ShowDialog();

        if (result != Forms.DialogResult.OK)
            return;

        _pathService.SetWin64Path(dialog.SelectedPath);

        RefreshPathState();

        if (!IsGamePathValid)
        {
            Items.Clear();
            StatusText = "Invalid folder. Select the folder that contains Remnant2-Win64-Shipping.exe and Mods\\Remnant2Unlocker";
            return;
        }

        StatusText = "Game path saved";

        await LoadAsync();
    }

    private async Task LoadImagesInBackgroundAsync()
    {
        foreach (var item in _allItems)
        {
            if (item.HasImage)
                continue;

            item.IsImageLoading = true;
            item.ImagePath = await _wikiImageService.GetImageAsync(item.Name);
            item.IsImageLoading = false;
        }
    }

    private void SelectType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return;

        SelectedType = type;

        if (type == "All")
        {
            SelectedGroup = "All";
            return;
        }

        var group = CategoryGroups.FirstOrDefault(x => x.Types.Contains(type));

        if (group != null)
            SelectedGroup = group.Name;
    }

    private void ApplyFilter()
    {
        Items.Clear();

        IEnumerable<RemnantItem> query = _allItems;

        if (!string.IsNullOrWhiteSpace(SelectedType) && SelectedType != "All")
        {
            query = query.Where(x => string.Equals(x.Type, SelectedType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();

            query = query.Where(x =>
                x.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                x.Type.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query.OrderBy(x => x.Type).ThenBy(x => x.Name))
            Items.Add(item);

        StatusText = $"{Items.Count} items shown";
    }

    public string BuildWikiUrl(RemnantItem item)
    {
        if (SelectedWiki == "Fextralife")
        {
            var pageName = Uri.EscapeDataString(item.Name.Replace(" ", "+"));
            return $"https://remnant2.wiki.fextralife.com/{pageName}";
        }

        var wikiTitle = BuildWikiGgTitle(item.Name);
        return $"https://remnant2.wiki.gg/wiki/{wikiTitle}";
    }

    private static string BuildWikiGgTitle(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return "";

        var parts = itemName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            var word = parts[i];

            if (i > 0 && WikiLowercaseWords.Contains(word))
            {
                parts[i] = word.ToLowerInvariant();
                continue;
            }

            parts[i] = ToTitleWord(word);
        }

        return Uri.EscapeDataString(string.Join("_", parts));
    }

    private static string ToTitleWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return "";

        if (word.Length == 1)
            return word.ToUpperInvariant();

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    public async Task SpawnItemAsync(RemnantItem item)
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Spawn blocked: game path is not configured";
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            StatusText = $"Spawn blocked: missing path for {item.Name}";
            return;
        }

        await _queueWriter.SpawnAsync(item);

        StatusText = $"Spawn sent: {item.Name}";
    }

    public async Task ForceConsoleSpawnItemAsync(RemnantItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                StatusText = $"Force spawn blocked: missing path for {item.Name}";
                return;
            }

            await _consoleSpawnService.SpawnViaConsoleAsync(item, ConsoleKey);

            StatusText = $"Force spawn sent: {item.Name}";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public void CopySummonCommand(RemnantItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            StatusText = $"Copy blocked: missing path for {item.Name}";
            return;
        }

        Forms.Clipboard.SetText(item.SummonCommand);

        StatusText = $"Copied summon command: {item.Name}";
    }

    private async Task UnlockGroupAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Group spawn blocked: game path is not configured";
            return;
        }

        if (SelectedType == "All")
        {
            StatusText = "Select a subcategory before spawning a group";
            return;
        }

        await _queueWriter.UnlockTypesAsync(new[] { SelectedType });

        StatusText = $"Group spawn sent: {SelectedType}";
    }

    private void StartTeleportHotkeyCapture()
    {
        IsCapturingConsoleKey = false;
        IsCapturingTeleportHotkey = true;
    }

    private void StartConsoleKeyCapture()
    {
        IsCapturingTeleportHotkey = false;
        IsCapturingConsoleKey = true;
    }

    public void SetTeleportHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        TeleportHotkey = key;
        IsCapturingTeleportHotkey = false;

        SaveHotkeySettings();

        StatusText = $"Teleport hotkey saved: {TeleportHotkey}";
    }

    public void SetConsoleKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        ConsoleKey = key;
        IsCapturingConsoleKey = false;

        SaveHotkeySettings();

        StatusText = $"Console key saved: {ConsoleKey}";
    }

    private void SaveHotkeySettings()
    {
        _hotkeySettingsService.Save(new HotkeySettings
        {
            AlwaysOnTop = AlwaysOnTop,
            ConsoleKey = ConsoleKey,
            Teleport = TeleportHotkey,
            Wiki = SelectedWiki
        });
    }

    private void RefreshPathState()
    {
        OnPropertyChanged(nameof(IsGamePathValid));
        OnPropertyChanged(nameof(GamePathStatus));
        OnPropertyChanged(nameof(GamePathDisplay));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _asyncExecute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_asyncExecute != null)
            await _asyncExecute();

        _execute?.Invoke();
    }
}

public sealed class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }
}