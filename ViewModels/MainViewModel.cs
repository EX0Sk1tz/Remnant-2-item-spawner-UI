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
    private readonly BridgeStatusService _bridgeStatusService;
    private readonly WikiImageService _wikiImageService;
    private readonly ConsoleSpawnService _consoleSpawnService;
    private readonly HotkeySettingsService _hotkeySettingsService;
    private readonly CheatSettingsService _cheatSettingsService;
    private readonly DiagnosticsService _diagnosticsService;
    private DiagnosticReport _diagnosticReport = DiagnosticReport.Empty;
    private string _categorySearchText = "";
    private readonly List<CategoryGroup> _allCategoryGroups = new();


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
    private bool _isCapturingDestroyTargetHotkey;
    private string _teleportHotkey = "F6";
    private string _consoleKey = "F10";
    private string _destroyTargetHotkey = "None";
    private double _movementSpeedMultiplier = 1.0;
    private bool _infiniteHealth;
    private bool _infiniteStamina;
    private int _stackSize = 1;

    public MainViewModel()
    {
        _pathService = new GamePathService();

        _itemRepository = new ItemRepository(_pathService);
        _queueWriter = new QueueWriter(_pathService);
        _bridgeStatusService = new BridgeStatusService(_pathService);
        _wikiImageService = new WikiImageService();
        _consoleSpawnService = new ConsoleSpawnService();
        _hotkeySettingsService = new HotkeySettingsService(_pathService);
        _cheatSettingsService = new CheatSettingsService(_pathService);
        _diagnosticsService = new DiagnosticsService(_pathService);
        RefreshDiagnostics();

        var cheatSettings = _cheatSettingsService.Load();
        _infiniteHealth = cheatSettings.InfiniteHealth;
        _infiniteStamina = cheatSettings.InfiniteStamina;

        var settings = _hotkeySettingsService.Load();

        _alwaysOnTop = settings.AlwaysOnTop;
        _teleportHotkey = string.IsNullOrWhiteSpace(settings.Teleport) ? "F6" : settings.Teleport;
        _consoleKey = string.IsNullOrWhiteSpace(settings.ConsoleKey) ? "F10" : settings.ConsoleKey;
        _destroyTargetHotkey = string.IsNullOrWhiteSpace(settings.DestroyTarget) ? "None" : settings.DestroyTarget;
        _selectedWiki = string.IsNullOrWhiteSpace(settings.Wiki) ? "wiki.gg" : settings.Wiki;
        _movementSpeedMultiplier = settings.MovementSpeedMultiplier <= 0 ? 1.0 : settings.MovementSpeedMultiplier;
        _stackSize = settings.StackSize <= 0 ? 1 : settings.StackSize;

        _allCategoryGroups = new List<CategoryGroup>
        {
            new() { Name = "Weapons", Types = new List<string> { "Bow", "Handgun", "Long Gun", "Melee" } },
            new() { Name = "Armor", Types = new List<string> { "Body", "Gloves", "Head", "Legs" } },
            new() { Name = "Accessories", Types = new List<string> { "Amulet", "Ring" } },
            new() { Name = "Traits", Types = new List<string> { "Archetype Trait", "Core Trait", "Trait" } },
            new() { Name = "Items", Types = new List<string> { "Concoction", "Consumable", "Curative", "Grenade", "Relic" } },
            new() { Name = "Materials", Types = new List<string> { "Crafting Material", "Currency", "Engram Material", "Material", "Upgrade Material" } },
            new() { Name = "Other", Types = new List<string> { "Mutator", "Prism Fragment", "Special", "Trait Point" } }
        };

        CategoryGroups = new ObservableCollection<CategoryGroup>();

        ApplyCategoryFilter();

        SelectTypeCommand = new RelayCommand<string>(SelectType);
        UnlockGroupCommand = new RelayCommand(async () => await UnlockGroupAsync());
        ReloadCommand = new RelayCommand(async () => await LoadAsync());
        SelectGamePathCommand = new RelayCommand(async () => await SelectGamePathAsync());
        StartTeleportHotkeyCaptureCommand = new RelayCommand(StartTeleportHotkeyCapture);
        StartConsoleKeyCaptureCommand = new RelayCommand(StartConsoleKeyCapture);
        StartDestroyTargetHotkeyCaptureCommand = new RelayCommand(StartDestroyTargetHotkeyCapture);

        RefreshPathState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<string>? GroupSpawnQueued;

    public ObservableCollection<RemnantItem> Items { get; } = new();

    public ObservableCollection<CategoryGroup> CategoryGroups { get; }

    public RelayCommand<string> SelectTypeCommand { get; }

    public RelayCommand UnlockGroupCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand SelectGamePathCommand { get; }

    public RelayCommand StartTeleportHotkeyCaptureCommand { get; }

    public RelayCommand StartConsoleKeyCaptureCommand { get; }

    public RelayCommand StartDestroyTargetHotkeyCaptureCommand { get; }

    public List<string> WikiOptions { get; } = new()
    {
        "wiki.gg",
        "Fextralife"
    };

    public DiagnosticReport DiagnosticReport => _diagnosticReport;

    public BridgeStatusService BridgeStatusService => _bridgeStatusService;

    public QueueWriter QueueWriter => _queueWriter;

    public bool HasDiagnosticIssue => _diagnosticReport.HasIssues;

    public string DiagnosticHint => _diagnosticReport.Hint;

    public int StackSize
    {
        get => _stackSize;
        set
        {
            var clamped = value;

            if (clamped < 1)
                clamped = 1;

            if (clamped > 999)
                clamped = 999;

            if (_stackSize == clamped)
                return;

            _stackSize = clamped;
            OnPropertyChanged();

            SaveHotkeySettings();

            StatusText = $"Default stack size saved: {_stackSize}";
        }
    }

    private static bool IsRelicFragment(RemnantItem item)
    {
        if (item.Type.Contains("Relic Fragment", StringComparison.OrdinalIgnoreCase))
            return true;

        if (item.Name.Contains("Relic Fragment", StringComparison.OrdinalIgnoreCase))
            return true;

        if (item.Path.Contains("RelicFragment_", StringComparison.OrdinalIgnoreCase))
            return true;

        if (item.Path.Contains("/Items/Gems/", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private int GetItemLevel(RemnantItem item)
    {
        return IsRelicFragment(item) ? 31 : 0;
    }

    public string BuildSummonCommand(RemnantItem item)
    {
        var stackSize = Math.Clamp(StackSize, 1, 999);
        var itemLevel = GetItemLevel(item);

        if (itemLevel > 0)
            return $"summon {item.Path} 1 {stackSize} {itemLevel}";

        return $"summon {item.Path} 1 {stackSize}";
    }

    public void RefreshDiagnostics()
    {
        _diagnosticReport = _diagnosticsService.Run();

        OnPropertyChanged(nameof(DiagnosticReport));
        OnPropertyChanged(nameof(HasDiagnosticIssue));
        OnPropertyChanged(nameof(DiagnosticHint));
    }

    public string CategorySearchText
    {
        get => _categorySearchText;
        set
        {
            _categorySearchText = value;
            OnPropertyChanged();
            ApplyCategoryFilter();
        }
    }

    private void ApplyCategoryFilter()
    {
        CategoryGroups.Clear();

        var search = CategorySearchText.Trim();

        foreach (var group in _allCategoryGroups)
        {
            var types = group.Types;

            if (!string.IsNullOrWhiteSpace(search))
            {
                types = group.Types
                    .Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (types.Count == 0)
                continue;

            CategoryGroups.Add(new CategoryGroup
            {
                Name = group.Name,
                Types = types
            });
        }
    }

    private void SaveCheatSettings()
    {
        try
        {
            _cheatSettingsService.Save(new CheatSettings
            {
                InfiniteHealth = InfiniteHealth,
                InfiniteStamina = InfiniteStamina
            });

            StatusText = $"Cheat settings saved. Health={InfiniteHealth}, Stamina={InfiniteStamina}";
        }
        catch (Exception ex)
        {
            StatusText = $"Cheat settings save failed: {ex.Message}";
        }
    }

    public void SetDestroyTargetHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        DestroyTargetHotkey = key;
        IsCapturingDestroyTargetHotkey = false;

        SaveHotkeySettings();

        StatusText = DestroyTargetHotkey == "None"
            ? "DestroyTarget hotkey cleared"
            : $"DestroyTarget hotkey saved: {DestroyTargetHotkey}";
    }

    public bool InfiniteHealth
    {
        get => _infiniteHealth;
        set
        {
            if (_infiniteHealth == value)
                return;

            _infiniteHealth = value;
            OnPropertyChanged();

            StatusText = InfiniteHealth
                ? "Infinite Health enabled"
                : "Infinite Health disabled";

            SaveCheatSettings();
        }
    }

    public bool InfiniteStamina
    {
        get => _infiniteStamina;
        set
        {
            if (_infiniteStamina == value)
                return;

            _infiniteStamina = value;
            OnPropertyChanged();

            StatusText = InfiniteStamina
                ? "Infinite Stamina enabled"
                : "Infinite Stamina disabled";

            SaveCheatSettings();
        }
    }

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

    public bool IsCapturingDestroyTargetHotkey
    {
        get => _isCapturingDestroyTargetHotkey;
        set
        {
            _isCapturingDestroyTargetHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyTargetHotkeyDisplay));
        }
    }

    private void StartDestroyTargetHotkeyCapture()
    {
        IsCapturingTeleportHotkey = false;
        IsCapturingConsoleKey = false;
        IsCapturingDestroyTargetHotkey = true;
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

    public string DestroyTargetHotkey
    {
        get => _destroyTargetHotkey;
        set
        {
            _destroyTargetHotkey = string.IsNullOrWhiteSpace(value) ? "None" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyTargetHotkeyDisplay));
        }
    }

    public string TeleportHotkeyDisplay => IsCapturingTeleportHotkey
        ? "Press key..."
        : TeleportHotkey;

    public string ConsoleKeyDisplay => IsCapturingConsoleKey
        ? "Press key..."
        : ConsoleKey;

    public string DestroyTargetHotkeyDisplay => IsCapturingDestroyTargetHotkey
    ? "Press key..."
    : DestroyTargetHotkey;

    public double MovementSpeedMultiplier
    {
        get => _movementSpeedMultiplier;
        set
        {
            var rounded = Math.Round(value, 1);

            if (rounded < 1.0)
                rounded = 1.0;

            if (rounded > 5.0)
                rounded = 5.0;

            _movementSpeedMultiplier = rounded;

            OnPropertyChanged();
            OnPropertyChanged(nameof(MovementSpeedDisplay));

            SaveHotkeySettings();

            StatusText = $"Movement speed multiplier saved: {MovementSpeedDisplay}";
        }
    }

    public string MovementSpeedDisplay => $"{MovementSpeedMultiplier:0.0}x";

    public bool IsGamePathValid => _pathService.IsConfigured;

    public string GamePathStatus => IsGamePathValid
        ? $"Game path configured ({_pathService.PlatformName})"
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
            RefreshDiagnostics();

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
            Description = "Select Remnant 2 executable folder. Steam/Epic: Binaries\\Win64. Game Pass: Binaries\\WinGDK",
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
        RefreshDiagnostics();

        if (!IsGamePathValid)
        {
            Items.Clear();
            StatusText = "Invalid folder. Select the folder that contains Remnant2-Win64-Shipping.exe or Remnant2-WinGDK-Shipping.exe and Mods\\Remnant2Unlocker";
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

        var group = _allCategoryGroups.FirstOrDefault(x => x.Types.Contains(type));

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

        await _queueWriter.SpawnAsync(item, StackSize, GetItemLevel(item));

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

        Forms.Clipboard.SetText(BuildSummonCommand(item));

        StatusText = $"Copied summon command: {item.Name}";
    }

    private async Task UnlockGroupAsync()
    {
        RefreshPathState();
        RefreshDiagnostics();

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

        var groupItems = _allItems
            .Where(x => string.Equals(x.Type, SelectedType, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .ToList();

        if (groupItems.Count == 0)
        {
            StatusText = $"No spawnable items found for {SelectedType}";
            return;
        }

        if (groupItems.Count > 50)
        {
            var result = Forms.MessageBox.Show(
                $"This group contains {groupItems.Count} items.\n\n" +
                "Large groups are spawned one item at a time to reduce crash risk.\n\n" +
                "Settings:\n" +
                "• 1 item every 500 ms\n\n" +
                "Start safe group spawn?",
                "Safe Group Spawn",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Warning);

            if (result != Forms.DialogResult.Yes)
            {
                StatusText = $"Group spawn cancelled: {SelectedType}";
                return;
            }
        }

        await _queueWriter.UnlockTypesAsync(new[] { SelectedType }, StackSize);

        StatusText = $"Safe group spawn queued: {SelectedType} ({groupItems.Count} items)";
        GroupSpawnQueued?.Invoke(this, $"Safe Spawn: {SelectedType}");
    }

    private void StartTeleportHotkeyCapture()
    {
        IsCapturingConsoleKey = false;
        IsCapturingDestroyTargetHotkey = false;
        IsCapturingTeleportHotkey = true;
    }

    private void StartConsoleKeyCapture()
    {
        IsCapturingTeleportHotkey = false;
        IsCapturingDestroyTargetHotkey = false;
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
            DestroyTarget = DestroyTargetHotkey,
            Wiki = SelectedWiki,
            MovementSpeedMultiplier = MovementSpeedMultiplier,
            StackSize = StackSize,
            InfiniteHealth = InfiniteHealth,
            InfiniteStamina = InfiniteStamina
        });
    }

    private void RefreshPathState()
    {
        OnPropertyChanged(nameof(IsGamePathValid));
        OnPropertyChanged(nameof(GamePathStatus));
        OnPropertyChanged(nameof(GamePathDisplay));
        OnPropertyChanged(nameof(HasDiagnosticIssue));
        OnPropertyChanged(nameof(DiagnosticHint));
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