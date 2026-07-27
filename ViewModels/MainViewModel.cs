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
    private readonly InventoryItemsService _inventoryItemsService;
    private readonly WeaponModBoostSettingsService _weaponModBoostSettingsService;
    private readonly DiagnosticsService _diagnosticsService;
    private DiagnosticReport _diagnosticReport = DiagnosticReport.Empty;
    private string _categorySearchText = "";
    private readonly List<CategoryGroup> _allCategoryGroups = new();
    private readonly SummonableTraitsService _summonableTraitsService;
    private readonly FavoritesService _favoritesService;
    private bool _isSummonableTraitsInstalled;


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
    private bool _isCapturingDestroyLastSpawnedHotkey;
    private bool _isCapturingDestroyNearbySpawnedHotkey;
    private bool _isCapturingReplenishCooldownsHotkey;
    private bool _isCapturingFastPlayerActionsHotkey;
    private string _teleportHotkey = "F6";
    private string _consoleKey = "F10";
    private string _destroyTargetHotkey = "None";
    private string _destroyLastSpawnedHotkey = "None";
    private string _destroyNearbySpawnedHotkey = "None";
    private string _replenishCooldownsHotkey = "F1";
    private string _fastPlayerActionsHotkey = "F2";
    private double _movementSpeedMultiplier = 1.0;
    private double _fovValue = 90.0;
    private bool _infiniteHealth;
    private bool _infiniteStamina;
    private bool _infiniteAmmo;
    private bool _noFallDamage;
    private bool _enemyEsp;
    private int _stackSize = 1;
    private string _languageCode = "en";
    private int _levelUpCount = 1;
    private int _setWeaponLevelValue = 10;
    private string _inventoryItemName = "";
    private int _inventoryItemQuantity = 1;
    private bool _logAllInventoryItems;

    private bool _isUpdateAvailable;
    private string? _latestVersionText;
    private string? _updateDownloadUrl;
    private string? _updateReleaseNotes;
    private bool _isUpdating;

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
        _inventoryItemsService = new InventoryItemsService(_pathService);
        _weaponModBoostSettingsService = new WeaponModBoostSettingsService(_pathService);
        _summonableTraitsService = new SummonableTraitsService(_pathService);
        _favoritesService = new FavoritesService();
        _diagnosticsService = new DiagnosticsService(_pathService, _summonableTraitsService);
        RefreshDiagnostics();

        var cheatSettings = _cheatSettingsService.Load();
        _infiniteHealth = cheatSettings.InfiniteHealth;
        _infiniteStamina = cheatSettings.InfiniteStamina;
        _infiniteAmmo = cheatSettings.InfiniteAmmo;
        _noFallDamage = cheatSettings.NoFallDamage;
        _enemyEsp = cheatSettings.EnemyEsp;

        var settings = _hotkeySettingsService.Load();

        _alwaysOnTop = settings.AlwaysOnTop;
        _teleportHotkey = string.IsNullOrWhiteSpace(settings.Teleport) ? "F6" : settings.Teleport;
        _consoleKey = string.IsNullOrWhiteSpace(settings.ConsoleKey) ? "F10" : settings.ConsoleKey;
        _destroyTargetHotkey = string.IsNullOrWhiteSpace(settings.DestroyTarget) ? "None" : settings.DestroyTarget;
        _destroyLastSpawnedHotkey = string.IsNullOrWhiteSpace(settings.DestroyLastSpawned) ? "None" : settings.DestroyLastSpawned;
        _destroyNearbySpawnedHotkey = string.IsNullOrWhiteSpace(settings.DestroyNearbySpawned) ? "None" : settings.DestroyNearbySpawned;
        _replenishCooldownsHotkey = string.IsNullOrWhiteSpace(settings.ReplenishCooldowns) ? "F1" : settings.ReplenishCooldowns;
        _fastPlayerActionsHotkey = string.IsNullOrWhiteSpace(settings.FastPlayerActions) ? "F2" : settings.FastPlayerActions;
        _selectedWiki = string.IsNullOrWhiteSpace(settings.Wiki) ? "wiki.gg" : settings.Wiki;
        _movementSpeedMultiplier = settings.MovementSpeedMultiplier <= 0 ? 1.0 : settings.MovementSpeedMultiplier;
        _fovValue = settings.FovValue <= 0 ? 90.0 : settings.FovValue;
        _stackSize = settings.StackSize <= 0 ? 1 : settings.StackSize;
        _languageCode = string.IsNullOrWhiteSpace(settings.Language) ? "en" : settings.Language;

        Loc = new LocalizationService(_languageCode);

        _allCategoryGroups = new List<CategoryGroup>
        {
            new() { Name = "Weapons", Types = new List<string> { "Bow", "Handgun", "Long Gun", "Melee" } },
            new() { Name = "Armor", Types = new List<string> { "Body", "Gloves", "Head", "Legs" } },
            new() { Name = "Accessories", Types = new List<string> { "Amulet", "Ring" } },
            new() { Name = "Traits", Types = new List<string> { "Archetype Trait", "Core Trait", "Trait", "Trait Point" } },
            new() { Name = "Items", Types = new List<string> { "Concoction", "Consumable", "Curative", "Grenade", "Relic" } },
            new() { Name = "Materials", Types = new List<string> { "Crafting Material", "Currency", "Engram Material", "Upgrade Material" } },
            new() { Name = "Other", Types = new List<string> { "Mutator", "Prism Fragment", "Special" } }
        };

        CategoryGroups = new ObservableCollection<CategoryGroup>();

        ApplyCategoryFilter();

        SelectTypeCommand = new RelayCommand<string>(SelectType);
        ToggleFavoriteCommand = new RelayCommand<RemnantItem>(ToggleFavorite);
        UnlockGroupCommand = new RelayCommand(async () => await UnlockGroupAsync());
        ReloadCommand = new RelayCommand(async () => await LoadAsync());
        SelectGamePathCommand = new RelayCommand(async () => await SelectGamePathAsync());
        StartTeleportHotkeyCaptureCommand = new RelayCommand(StartTeleportHotkeyCapture);
        StartConsoleKeyCaptureCommand = new RelayCommand(StartConsoleKeyCapture);
        StartDestroyTargetHotkeyCaptureCommand = new RelayCommand(StartDestroyTargetHotkeyCapture);
        StartDestroyLastSpawnedHotkeyCaptureCommand = new RelayCommand(StartDestroyLastSpawnedHotkeyCapture);
        StartDestroyNearbySpawnedHotkeyCaptureCommand = new RelayCommand(StartDestroyNearbySpawnedHotkeyCapture);
        StartReplenishCooldownsHotkeyCaptureCommand = new RelayCommand(StartReplenishCooldownsHotkeyCapture);
        StartFastPlayerActionsHotkeyCaptureCommand = new RelayCommand(StartFastPlayerActionsHotkeyCapture);
        LevelUpCommand = new RelayCommand(async () => await LevelUpAsync());
        SetAllWeaponLevelCommand = new RelayCommand(async () => await SetAllWeaponLevelAsync());
        SetInventoryItemQuantityCommand = new RelayCommand(async () => await SetInventoryItemQuantityAsync());
        LogInventoryItemsCommand = new RelayCommand(async () => await LogInventoryItemsAsync());
        ApplyAllWeaponModBoostsCommand = new RelayCommand(async () => await ApplyAllWeaponModBoostsAsync());
        UpdateNowCommand = new RelayCommand(async () => await ApplyUpdateAsync(), () => !IsUpdating);
        ShowUpdatePreviewCommand = new RelayCommand(() => UpdatePreviewRequested?.Invoke(this, EventArgs.Empty), () => !IsUpdating);

        InitializeWeaponModBoostGroups();

        RefreshPathState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<string>? GroupSpawnQueued;

    public event EventHandler? UpdatePreviewRequested;

    public ObservableCollection<RemnantItem> Items { get; } = new();

    public ObservableCollection<InventoryItemEntry> InventoryItems { get; } = new();

    public ObservableCollection<WeaponModBoostGroup> WeaponModBoostGroups { get; } = new();

    public ObservableCollection<CategoryGroup> CategoryGroups { get; }

    public RelayCommand<string> SelectTypeCommand { get; }

    public RelayCommand<RemnantItem> ToggleFavoriteCommand { get; }

    public RelayCommand UnlockGroupCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand SelectGamePathCommand { get; }

    public RelayCommand StartTeleportHotkeyCaptureCommand { get; }

    public RelayCommand StartConsoleKeyCaptureCommand { get; }

    public RelayCommand StartDestroyTargetHotkeyCaptureCommand { get; }

    public RelayCommand StartDestroyLastSpawnedHotkeyCaptureCommand { get; }

    public RelayCommand StartDestroyNearbySpawnedHotkeyCaptureCommand { get; }

    public RelayCommand StartReplenishCooldownsHotkeyCaptureCommand { get; }

    public RelayCommand StartFastPlayerActionsHotkeyCaptureCommand { get; }

    public RelayCommand LevelUpCommand { get; }

    public RelayCommand SetAllWeaponLevelCommand { get; }

    public RelayCommand SetInventoryItemQuantityCommand { get; }

    public RelayCommand LogInventoryItemsCommand { get; }

    public RelayCommand ApplyAllWeaponModBoostsCommand { get; }

    public RelayCommand UpdateNowCommand { get; }

    public RelayCommand ShowUpdatePreviewCommand { get; }

    public List<string> WikiOptions { get; } = new()
    {
        "wiki.gg",
        "Fextralife"
    };

    public DiagnosticReport DiagnosticReport => _diagnosticReport;

    public BridgeStatusService BridgeStatusService => _bridgeStatusService;

    public QueueWriter QueueWriter => _queueWriter;

    public string VersionText { get; } = $"v{UpdateCheckService.GetCurrentVersion()}";

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            _isUpdateAvailable = value;
            OnPropertyChanged();
        }
    }

    public string? LatestVersionText
    {
        get => _latestVersionText;
        private set
        {
            _latestVersionText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdateBadgeText));
        }
    }

    public string? ReleaseNotes => _updateReleaseNotes;

    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            _isUpdating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdateBadgeText));
            UpdateNowCommand.RaiseCanExecuteChanged();
            ShowUpdatePreviewCommand.RaiseCanExecuteChanged();
        }
    }

    public string UpdateBadgeText => IsUpdating
        ? Loc["Main.Updating"]
        : $"{VersionText} → {LatestVersionText}";

    public bool HasDiagnosticIssue => _diagnosticReport.HasIssues;

    public string DiagnosticHint => _diagnosticReport.Hint;

    public bool IsSummonableTraitsInstalled
    {
        get => _isSummonableTraitsInstalled;
        private set
        {
            _isSummonableTraitsInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SummonableTraitsStatus));
        }
    }

    public string SummonableTraitsStatus => IsSummonableTraitsInstalled
        ? "Summonable Traits detected"
        : "Summonable Traits not installed";

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
    private void RefreshSummonableTraitsState()
    {
        IsSummonableTraitsInstalled = _summonableTraitsService.IsInstalled();

        foreach (var item in _allItems)
            item.IsSummonableTraitsInstalled = IsSummonableTraitsInstalled;

        foreach (var item in Items)
            item.IsSummonableTraitsInstalled = IsSummonableTraitsInstalled;
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
                InfiniteStamina = InfiniteStamina,
                InfiniteAmmo = InfiniteAmmo,
                NoFallDamage = NoFallDamage,
                EnemyEsp = EnemyEsp
            });

            StatusText = $"Cheat settings saved. Health={InfiniteHealth}, Stamina={InfiniteStamina}, Ammo={InfiniteAmmo}, NoFallDamage={NoFallDamage}, EnemyEsp={EnemyEsp}";
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

    public void SetDestroyLastSpawnedHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        DestroyLastSpawnedHotkey = key;
        IsCapturingDestroyLastSpawnedHotkey = false;

        SaveHotkeySettings();

        StatusText = DestroyLastSpawnedHotkey == "None"
            ? "DestroyLastSpawned hotkey cleared"
            : $"DestroyLastSpawned hotkey saved: {DestroyLastSpawnedHotkey}";
    }

    public void SetDestroyNearbySpawnedHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        DestroyNearbySpawnedHotkey = key;
        IsCapturingDestroyNearbySpawnedHotkey = false;

        SaveHotkeySettings();

        StatusText = DestroyNearbySpawnedHotkey == "None"
            ? "DestroyNearbySpawned hotkey cleared"
            : $"DestroyNearbySpawned hotkey saved: {DestroyNearbySpawnedHotkey}";
    }

    public void SetReplenishCooldownsHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        ReplenishCooldownsHotkey = key;
        IsCapturingReplenishCooldownsHotkey = false;

        SaveHotkeySettings();

        StatusText = ReplenishCooldownsHotkey == "None"
            ? "Replenish Cooldowns hotkey cleared"
            : $"Replenish Cooldowns hotkey saved: {ReplenishCooldownsHotkey}";
    }

    public void SetFastPlayerActionsHotkey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        FastPlayerActionsHotkey = key;
        IsCapturingFastPlayerActionsHotkey = false;

        SaveHotkeySettings();

        StatusText = FastPlayerActionsHotkey == "None"
            ? "Fast Player Actions hotkey cleared"
            : $"Fast Player Actions hotkey saved: {FastPlayerActionsHotkey}";
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

    public bool InfiniteAmmo
    {
        get => _infiniteAmmo;
        set
        {
            if (_infiniteAmmo == value)
                return;

            _infiniteAmmo = value;
            OnPropertyChanged();

            StatusText = InfiniteAmmo
                ? "Infinite Ammo enabled"
                : "Infinite Ammo disabled";

            SaveCheatSettings();
        }
    }

    public bool NoFallDamage
    {
        get => _noFallDamage;
        set
        {
            if (_noFallDamage == value)
                return;

            _noFallDamage = value;
            OnPropertyChanged();

            StatusText = NoFallDamage
                ? "No Fall Damage enabled"
                : "No Fall Damage disabled";

            SaveCheatSettings();
        }
    }

    public bool EnemyEsp
    {
        get => _enemyEsp;
        set
        {
            if (_enemyEsp == value)
                return;

            _enemyEsp = value;
            OnPropertyChanged();

            StatusText = EnemyEsp
                ? "Enemy ESP enabled"
                : "Enemy ESP disabled";

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

    public LocalizationService Loc { get; }

    public List<LanguageOption> AvailableLanguages { get; } = new()
    {
        new LanguageOption("en", "English"),
        new LanguageOption("de", "Deutsch")
    };

    public string LanguageCode
    {
        get => _languageCode;
        set
        {
            if (_languageCode == value)
                return;

            _languageCode = value;
            OnPropertyChanged();
            Loc.SetLanguage(value);
            SaveHotkeySettings();
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

    public bool IsCapturingDestroyLastSpawnedHotkey
    {
        get => _isCapturingDestroyLastSpawnedHotkey;
        set
        {
            _isCapturingDestroyLastSpawnedHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyLastSpawnedHotkeyDisplay));
        }
    }

    public bool IsCapturingDestroyNearbySpawnedHotkey
    {
        get => _isCapturingDestroyNearbySpawnedHotkey;
        set
        {
            _isCapturingDestroyNearbySpawnedHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyNearbySpawnedHotkeyDisplay));
        }
    }

    public bool IsCapturingReplenishCooldownsHotkey
    {
        get => _isCapturingReplenishCooldownsHotkey;
        set
        {
            _isCapturingReplenishCooldownsHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReplenishCooldownsHotkeyDisplay));
        }
    }

    public bool IsCapturingFastPlayerActionsHotkey
    {
        get => _isCapturingFastPlayerActionsHotkey;
        set
        {
            _isCapturingFastPlayerActionsHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FastPlayerActionsHotkeyDisplay));
        }
    }

    // All 7 hotkey capture buttons share one active-capture slot -- starting a new capture
    // must clear every other one, or two buttons could show "Press key..." at once.
    private void ResetHotkeyCaptureFlags()
    {
        IsCapturingTeleportHotkey = false;
        IsCapturingConsoleKey = false;
        IsCapturingDestroyTargetHotkey = false;
        IsCapturingDestroyLastSpawnedHotkey = false;
        IsCapturingDestroyNearbySpawnedHotkey = false;
        IsCapturingReplenishCooldownsHotkey = false;
        IsCapturingFastPlayerActionsHotkey = false;
    }

    private void StartDestroyTargetHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingDestroyTargetHotkey = true;
    }

    private void StartDestroyLastSpawnedHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingDestroyLastSpawnedHotkey = true;
    }

    private void StartDestroyNearbySpawnedHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingDestroyNearbySpawnedHotkey = true;
    }

    private void StartReplenishCooldownsHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingReplenishCooldownsHotkey = true;
    }

    private void StartFastPlayerActionsHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingFastPlayerActionsHotkey = true;
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

    public string DestroyLastSpawnedHotkey
    {
        get => _destroyLastSpawnedHotkey;
        set
        {
            _destroyLastSpawnedHotkey = string.IsNullOrWhiteSpace(value) ? "None" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyLastSpawnedHotkeyDisplay));
        }
    }

    public string DestroyNearbySpawnedHotkey
    {
        get => _destroyNearbySpawnedHotkey;
        set
        {
            _destroyNearbySpawnedHotkey = string.IsNullOrWhiteSpace(value) ? "None" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DestroyNearbySpawnedHotkeyDisplay));
        }
    }

    public string ReplenishCooldownsHotkey
    {
        get => _replenishCooldownsHotkey;
        set
        {
            _replenishCooldownsHotkey = string.IsNullOrWhiteSpace(value) ? "None" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReplenishCooldownsHotkeyDisplay));
        }
    }

    public string FastPlayerActionsHotkey
    {
        get => _fastPlayerActionsHotkey;
        set
        {
            _fastPlayerActionsHotkey = string.IsNullOrWhiteSpace(value) ? "None" : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FastPlayerActionsHotkeyDisplay));
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

    public string DestroyLastSpawnedHotkeyDisplay => IsCapturingDestroyLastSpawnedHotkey
        ? "Press key..."
        : DestroyLastSpawnedHotkey;

    public string DestroyNearbySpawnedHotkeyDisplay => IsCapturingDestroyNearbySpawnedHotkey
        ? "Press key..."
        : DestroyNearbySpawnedHotkey;

    public string ReplenishCooldownsHotkeyDisplay => IsCapturingReplenishCooldownsHotkey
        ? "Press key..."
        : ReplenishCooldownsHotkey;

    public string FastPlayerActionsHotkeyDisplay => IsCapturingFastPlayerActionsHotkey
        ? "Press key..."
        : FastPlayerActionsHotkey;

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

    public double FovValue
    {
        get => _fovValue;
        set
        {
            var rounded = Math.Round(value, 0);

            if (rounded < 60.0)
                rounded = 60.0;

            if (rounded > 120.0)
                rounded = 120.0;

            _fovValue = rounded;

            OnPropertyChanged();
            OnPropertyChanged(nameof(FovDisplay));

            SaveHotkeySettings();

            StatusText = $"FOV saved: {FovDisplay}";
        }
    }

    public string FovDisplay => $"{FovValue:0}°";

    public int LevelUpCount
    {
        get => _levelUpCount;
        set
        {
            var clamped = value < 1 ? 1 : value;

            if (_levelUpCount == clamped)
                return;

            _levelUpCount = clamped;
            OnPropertyChanged();
        }
    }

    public int SetWeaponLevelValue
    {
        get => _setWeaponLevelValue;
        set
        {
            var clamped = value < 1 ? 1 : value;

            if (_setWeaponLevelValue == clamped)
                return;

            _setWeaponLevelValue = clamped;
            OnPropertyChanged();
        }
    }

    public string InventoryItemName
    {
        get => _inventoryItemName;
        set
        {
            _inventoryItemName = value ?? "";
            OnPropertyChanged();
        }
    }

    public int InventoryItemQuantity
    {
        get => _inventoryItemQuantity;
        set
        {
            var clamped = value < 0 ? 0 : value;

            if (_inventoryItemQuantity == clamped)
                return;

            _inventoryItemQuantity = clamped;
            OnPropertyChanged();
        }
    }

    public bool LogAllInventoryItems
    {
        get => _logAllInventoryItems;
        set
        {
            _logAllInventoryItems = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ToastMessage> Toasts => ToastService.Toasts;

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

            foreach (var item in _allItems)
                item.IsFavorite = _favoritesService.IsFavorite(item.Path);

            RefreshSummonableTraitsState();

            ApplyFilter();

            _ = LoadImagesInBackgroundAsync();

            AppLogService.Info($"Reload requested ({_allItems.Count} items loaded from items.json)");

            await _queueWriter.ReloadItemsAsync();

            StatusText = $"Loaded {_allItems.Count} items";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;

            AppLogService.Error("LoadAsync failed", ex);
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

        if (type == "All" || type == "Favorites")
        {
            SelectedGroup = type;
            return;
        }

        var group = _allCategoryGroups.FirstOrDefault(x => x.Types.Contains(type));

        if (group != null)
            SelectedGroup = group.Name;
    }

    private void ToggleFavorite(RemnantItem? item)
    {
        if (item == null)
            return;

        item.IsFavorite = !item.IsFavorite;
        _favoritesService.SetFavorite(item.Path, item.IsFavorite);

        if (SelectedType == "Favorites" && !item.IsFavorite)
        {
            Items.Remove(item);
            StatusText = $"{Items.Count} items shown";
        }
    }

    private void ApplyFilter()
    {
        Items.Clear();

        IEnumerable<RemnantItem> query = _allItems;

        if (SelectedType == "Favorites")
        {
            query = query.Where(x => x.IsFavorite);
        }
        else if (!string.IsNullOrWhiteSpace(SelectedType) && SelectedType != "All")
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
        {
            item.IsSummonableTraitsInstalled = IsSummonableTraitsInstalled;
            Items.Add(item);
        }

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
            ShowBlockedToast(StatusText);
            return;
        }

        if (item.IsTraitEntry && !item.IsTraitPoint)
        {
            StatusText = IsSummonableTraitsInstalled
                ? "Use Spawn Trait or Add Trait for trait entries"
                : "Trait spawn blocked: Summonable Traits mod is not installed";

            ShowBlockedToast(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            StatusText = $"Spawn blocked: missing path for {item.Name}";
            ShowBlockedToast(StatusText);
            return;
        }

        var itemLevel = GetItemLevel(item);

        AppLogService.Info($"Spawn requested: {item.Name} (path={item.Path}, stack={StackSize}, level={itemLevel})");

        await _queueWriter.SpawnAsync(item, StackSize, itemLevel);

        StatusText = $"Spawn sent: {item.Name}";
        ShowSpawnedToast("Spawn sent", item.Name);

        _ = LogBridgeOutcomeAsync($"Spawn '{item.Name}'");
    }

    public async Task ForceConsoleSpawnItemAsync(RemnantItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                StatusText = $"Force spawn blocked: missing path for {item.Name}";
                ShowBlockedToast(StatusText);
                return;
            }

            AppLogService.Info($"Force console spawn requested: {item.Name} (path={item.Path})");

            await _consoleSpawnService.SpawnViaConsoleAsync(item, ConsoleKey);

            StatusText = $"Force spawn sent: {item.Name}";
            ShowSpawnedToast("Force spawn sent", item.Name);

            AppLogService.Info($"Force console spawn sent: {item.Name}");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;

            ToastService.Show("Force spawn failed", ex.Message, ToastType.Error, 5000);

            AppLogService.Warn($"Force console spawn failed: {item.Name}", ex);
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

    public async Task SummonTraitAsync(RemnantItem item)
    {
        RefreshPathState();
        RefreshSummonableTraitsState();

        if (!IsGamePathValid)
        {
            StatusText = "Trait spawn blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        if (!item.IsTraitEntry || item.IsTraitPoint)
        {
            StatusText = "Trait spawn blocked: selected item is not a trait";
            ShowBlockedToast(StatusText);
            return;
        }

        if (!IsSummonableTraitsInstalled)
        {
            StatusText = "Trait spawn blocked: Summonable Traits mod is not installed";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"SummonTrait requested: {item.Name} (path={item.Path})");

        await _queueWriter.SendConsoleCommandAsync($"SummonTrait {item.Path}");

        StatusText = $"SummonTrait sent: {item.Name}";
        ShowSpawnedToast("SummonTrait sent", item.Name);

        _ = LogBridgeOutcomeAsync($"SummonTrait '{item.Name}'");
    }

    public async Task AddTraitAsync(RemnantItem item)
    {
        RefreshPathState();
        RefreshSummonableTraitsState();

        if (!IsGamePathValid)
        {
            StatusText = "AddTrait blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        if (!item.IsTraitEntry || item.IsTraitPoint)
        {
            StatusText = "AddTrait blocked: selected item is not a trait";
            ShowBlockedToast(StatusText);
            return;
        }

        if (!IsSummonableTraitsInstalled)
        {
            StatusText = "AddTrait blocked: Summonable Traits mod is not installed";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"AddTrait requested: {item.Name} (path={item.Path})");

        await _queueWriter.SendConsoleCommandAsync($"AddTrait {item.Path}");

        StatusText = $"AddTrait sent: {item.Name}";
        ShowSpawnedToast("AddTrait sent", item.Name);

        _ = LogBridgeOutcomeAsync($"AddTrait '{item.Name}'");
    }

    public async Task LevelUpAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Level Up blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"Level Up requested: {LevelUpCount}x");

        await _queueWriter.SendConsoleCommandAsync($"levelup {LevelUpCount}");

        StatusText = $"Level Up sent: {LevelUpCount}x";
        ShowSpawnedToast("Level Up sent", $"{LevelUpCount}x");

        _ = LogBridgeOutcomeAsync($"Level Up {LevelUpCount}x");
    }

    public async Task SetAllWeaponLevelAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Set All Weapon Level blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"Set All Weapon Level requested: {SetWeaponLevelValue}");

        await _queueWriter.SendConsoleCommandAsync($"set_all_weapon_level {SetWeaponLevelValue}");

        StatusText = $"Set All Weapon Level sent: {SetWeaponLevelValue}";
        ShowSpawnedToast("Set All Weapon Level sent", $"{SetWeaponLevelValue}");

        _ = LogBridgeOutcomeAsync($"Set All Weapon Level {SetWeaponLevelValue}");
    }

    public async Task SetInventoryItemQuantityAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Set Inventory Item Quantity blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(InventoryItemName))
        {
            StatusText = "Set Inventory Item Quantity blocked: item name is empty";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"Set Inventory Item Quantity requested: {InventoryItemName}={InventoryItemQuantity}");

        await _queueWriter.SendConsoleCommandAsync($"set_inventory_item_quantity {InventoryItemName} {InventoryItemQuantity}");

        StatusText = $"Set Inventory Item Quantity sent: {InventoryItemName}={InventoryItemQuantity}";
        ShowSpawnedToast("Set Inventory Item Quantity sent", $"{InventoryItemName}={InventoryItemQuantity}");

        _ = LogBridgeOutcomeAsync($"Set Inventory Item Quantity {InventoryItemName}={InventoryItemQuantity}");
    }

    public async Task LogInventoryItemsAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Log Inventory Items blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        AppLogService.Info($"Log Inventory Items requested (allItems={LogAllInventoryItems})");

        await _queueWriter.SendConsoleCommandAsync($"log_inventory_items {(LogAllInventoryItems ? "true" : "false")}");

        StatusText = "Log Inventory Items sent - check UE4SS.log";
        ShowSpawnedToast("Log Inventory Items sent", LogAllInventoryItems ? "all items" : "materials/consumables");

        await LogBridgeOutcomeAsync("Log Inventory Items");

        // The Lua handler also rescans inventory_items.json (see inventory_cheats.lua), so refresh
        // the picker now instead of making the user reopen Settings to see newly-found items.
        RefreshInventoryItems();
    }

    // Field lists mirror exactly what each mod's "boosted:" log line prints (see WeaponMods/mod_*.lua) --
    // NumChargesConsumedOnUse is deliberately left out even where it appears in a log line, since every
    // mod hardcodes it to 0 ("free to cast") by design rather than as a tunable value.
    private void InitializeWeaponModBoostGroups()
    {
        var saved = _weaponModBoostSettingsService.Load();

        WeaponModBoostGroup AddGroup(string key, string consoleCommand, params (string Field, double Default)[] fields)
        {
            var group = new WeaponModBoostGroup(key, key, consoleCommand);
            saved.TryGetValue(key, out var savedFields);

            foreach (var (field, defaultValue) in fields)
            {
                var value = savedFields != null && savedFields.TryGetValue(field, out var savedValue)
                    ? savedValue
                    : defaultValue;

                group.Fields.Add(new WeaponModBoostField(field, value));
            }

            group.ApplyCommand = new RelayCommand(async () => await ApplyWeaponModBoostAsync(group));

            WeaponModBoostGroups.Add(group);
            return group;
        }

        AddGroup("HotShot", "boost_hotshot",
            ("FireDuration", 10), ("FireBaseDamage", 100), ("ModDuration", 10));

        AddGroup("Sandstorm", "boost_sandstorm",
            ("CycloneDuration", 10), ("CycloneBaseRadius", 10), ("CycloneHomingRadius", 10),
            ("CycloneDPS", 10), ("CycloneDamageFrequency", 10));

        AddGroup("ConcussiveShot", "boost_concussiveshot",
            ("BlastDamage", 10), ("MaxRange", 10), ("BaseKnockbackDistance", 10), ("AOERadius", 10), ("MaxCharges", 10));

        AddGroup("Helix", "boost_helix",
            ("MaxCharges", 10), ("ImpactDamage", 10), ("SideWinderDamage", 10), ("SideWinderCount", 10));

        AddGroup("StatisBeam", "boost_statisbeam",
            ("Damage", 10), ("Duration", 10), ("RequiredHitDuration", 10));

        AddGroup("VoltaicRondure", "boost_voltaicrondure",
            ("PulseDelay", 0.01), ("OrbDamage", 10), ("ProjectileLifetime", 1), ("EffectRadius", 3),
            ("ShockDamage", 10), ("ShockDuration", 2));

        AddGroup("Scrapshot", "boost_scrapshot",
            ("MaxCharges", 10), ("BlastRadius", 10), ("DOTDamage", 10), ("CaltropDuration", 10),
            ("BleedDamage", 10), ("BleedDuration", 10));

        AddGroup("RottedArrow", "boost_rottedarrow",
            ("MaxCharges", 10), ("WeakSpotMod", 10), ("ImpactDamage", 10), ("DOTDamage", 10),
            ("CloudDuration", 10), ("BlastRadius", 10), ("CloudDamagePerSecond", 10));
    }

    private void SaveWeaponModBoostSettings()
    {
        var settings = new Dictionary<string, Dictionary<string, double>>();

        foreach (var group in WeaponModBoostGroups)
        {
            var fields = new Dictionary<string, double>();

            foreach (var field in group.Fields)
                fields[field.Key] = field.Value;

            settings[group.Key] = fields;
        }

        _weaponModBoostSettingsService.Save(settings);
    }

    private async Task ApplyWeaponModBoostAsync(WeaponModBoostGroup group)
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = $"{group.DisplayName} boost blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        SaveWeaponModBoostSettings();

        AppLogService.Info($"{group.DisplayName} boost requested");

        await _queueWriter.SendConsoleCommandAsync(group.ConsoleCommand);

        StatusText = $"{group.DisplayName} boost sent - check UE4SS.log";
        ShowSpawnedToast($"{group.DisplayName} boost sent", "check UE4SS.log");

        _ = LogBridgeOutcomeAsync($"{group.DisplayName} boost");
    }

    public async Task ApplyAllWeaponModBoostsAsync()
    {
        RefreshPathState();

        if (!IsGamePathValid)
        {
            StatusText = "Boost All Weapon Mods blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        SaveWeaponModBoostSettings();

        AppLogService.Info("Boost All Weapon Mods requested");

        await _queueWriter.SendConsoleCommandAsync("boost_weapon_mods");

        StatusText = "Boost All Weapon Mods sent - check UE4SS.log";
        ShowSpawnedToast("Boost All Weapon Mods sent", "check UE4SS.log");

        _ = LogBridgeOutcomeAsync("Boost All Weapon Mods");
    }

    // The Lua side (inventory_cheats.lua) writes inventory_items.json on a loop while a real
    // gameplay character exists, so this just re-reads whatever snapshot is currently on disk --
    // call it whenever the Settings window opens rather than polling continuously from here.
    public void RefreshInventoryItems()
    {
        var items = _inventoryItemsService.Load();

        InventoryItems.Clear();

        foreach (var item in items)
            InventoryItems.Add(item);
    }

    private static void ShowBlockedToast(string message)
    {
        ToastService.Show("Blocked", message, ToastType.Warning, 3500);
    }

    private static void ShowSpawnedToast(string title, string itemName)
    {
        ToastService.Show(title, itemName, ToastType.Success, 2200);
    }

    private async Task LogBridgeOutcomeAsync(string action, int delayMs = 700)
    {
        await Task.Delay(delayMs);

        var status = _bridgeStatusService.Read();

        if (!string.IsNullOrWhiteSpace(status.Error))
            AppLogService.Warn($"{action} -> {status.LastMessage} (bridge error: {status.Error})");
        else
            AppLogService.Info($"{action} -> {status.LastMessage}");
    }

    private async Task UnlockGroupAsync()
    {
        RefreshPathState();
        RefreshDiagnostics();
        RefreshSummonableTraitsState();

        if (!IsGamePathValid)
        {
            StatusText = "Group spawn blocked: game path is not configured";
            ShowBlockedToast(StatusText);
            return;
        }

        if (SelectedType == "All" || SelectedType == "Favorites")
        {
            StatusText = "Select a subcategory before spawning a group";
            ShowBlockedToast(StatusText);
            return;
        }

        var groupItems = _allItems
            .Where(x => string.Equals(x.Type, SelectedType, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .ToList();

        if (groupItems.Count == 0)
        {
            StatusText = $"No spawnable items found for {SelectedType}";
            ShowBlockedToast(StatusText);
            return;
        }

        var isTraitGroup = groupItems.All(x => x.IsTraitEntry && !x.IsTraitPoint);

        if (isTraitGroup && !IsSummonableTraitsInstalled)
        {
            StatusText = "Group spawn blocked: Summonable Traits mod is not installed";
            ShowBlockedToast(StatusText);
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

        AppLogService.Info($"Group spawn requested: {SelectedType} ({groupItems.Count} items, stack={StackSize})");

        await _queueWriter.UnlockTypesAsync(new[] { SelectedType }, StackSize);

        StatusText = $"Safe group spawn queued: {SelectedType} ({groupItems.Count} items)";
        GroupSpawnQueued?.Invoke(this, $"Safe Spawn: {SelectedType}");
    }

    private void StartTeleportHotkeyCapture()
    {
        ResetHotkeyCaptureFlags();
        IsCapturingTeleportHotkey = true;
    }

    private void StartConsoleKeyCapture()
    {
        ResetHotkeyCaptureFlags();
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
            DestroyLastSpawned = DestroyLastSpawnedHotkey,
            DestroyNearbySpawned = DestroyNearbySpawnedHotkey,
            ReplenishCooldowns = ReplenishCooldownsHotkey,
            FastPlayerActions = FastPlayerActionsHotkey,
            Wiki = SelectedWiki,
            MovementSpeedMultiplier = MovementSpeedMultiplier,
            FovValue = FovValue,
            StackSize = StackSize,
            Language = LanguageCode
        });
    }

    public void SetUpdateAvailable(string latestVersion, string? downloadUrl, string? releaseNotes)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return;

        _updateDownloadUrl = downloadUrl;
        _updateReleaseNotes = releaseNotes;
        LatestVersionText = latestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? latestVersion : $"v{latestVersion}";
        IsUpdateAvailable = true;
    }

    private async Task ApplyUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_updateDownloadUrl) || IsUpdating)
            return;

        IsUpdating = true;

        try
        {
            await AppUpdateService.DownloadAndApplyUpdateAsync(_updateDownloadUrl, _pathService);
        }
        finally
        {
            IsUpdating = false;
        }
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

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public async void Execute(object? parameter)
    {
        try
        {
            if (_asyncExecute != null)
                await _asyncExecute();

            _execute?.Invoke();
        }
        catch (Exception ex)
        {
            // Execute is async void (required by ICommand) so an unhandled exception here
            // would otherwise crash the whole app with no way for the caller to catch it.
            AppLogService.Error("A command failed", ex);

            ToastService.Show(
                "Something went wrong",
                $"{ex.Message}\n\nDetails were written to the app log.",
                ToastType.Error,
                6000);
        }
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