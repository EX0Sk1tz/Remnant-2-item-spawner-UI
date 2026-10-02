using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Remnant2UnlockerApp.Services;
using Forms = System.Windows.Forms;

namespace Remnant2UnlockerApp.ViewModels;

public enum WizardPage
{
    Game,
    CloseGame,
    Components,
    Antivirus,
    TestLaunch,
    Done
}

public enum ComponentKind
{
    AllowAssetMods,
    UnlockerMod,
    SummonPatch,
    SummonableTraits,
    ModsEnabled
}

public enum ComponentState
{
    Ok,
    Missing,
    Automatic,
    Optional,
    Skipped,
    Waiting
}

public enum AntivirusState
{
    Checking,
    Ok,
    Removed
}

public enum TestLaunchState
{
    Waiting,
    ModLoaded,
    Connected,
    TimedOut
}

/// <summary>One row of the wizard's component checklist.</summary>
public sealed class ComponentRow : INotifyPropertyChanged
{
    private ComponentState _state = ComponentState.Waiting;
    private string _statusText = "…";

    public ComponentRow(ComponentKind kind, string title, string description, string? nexusUrl = null)
    {
        Kind = kind;
        Title = title;
        Description = description;
        NexusUrl = nexusUrl;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ComponentKind Kind { get; }

    public string Title { get; }

    public string Description { get; }

    public string? NexusUrl { get; }

    public ComponentState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOk));
            OnPropertyChanged(nameof(AcceptsPackage));
            OnPropertyChanged(nameof(CanSkip));
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

    public bool IsOk => State == ComponentState.Ok;

    /// <summary>The row takes a dropped/chosen Nexus download (also to reinstall it when it's already there).</summary>
    public bool AcceptsPackage => NexusUrl != null && State != ComponentState.Skipped;

    public bool CanSkip => Kind == ComponentKind.SummonableTraits && State == ComponentState.Optional;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A detected install in the wizard's list; selected when it's the chosen game folder.</summary>
public sealed class InstallOption : INotifyPropertyChanged
{
    private readonly Action<InstallOption> _onSelected;
    private bool _isSelected;

    public InstallOption(GameInstallCandidate candidate, Action<InstallOption> onSelected)
    {
        Candidate = candidate;
        _onSelected = onSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameInstallCandidate Candidate { get; }

    public string PlatformName => Candidate.PlatformName;

    public string Win64Path => Candidate.Win64Path;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));

            if (value)
                _onSelected(this);
        }
    }

    /// <summary>Follows the game folder without calling back into it.</summary>
    public void Sync(string gamePath)
    {
        var selected = string.Equals(Win64Path.TrimEnd('\\'), gamePath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        if (_isSelected == selected)
            return;

        _isSelected = selected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }
}

/// <summary>
/// The setup wizard: pick the game, make sure it's closed, install/repair every component, check
/// that antivirus didn't eat UE4SS, and confirm the bridge with a test launch.
/// </summary>
public sealed class SetupWizardViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan AntivirusDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TestLaunchTimeout = TimeSpan.FromMinutes(3);

    private readonly GamePathService _pathService;
    private readonly DispatcherTimer _closeGameTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _antivirusTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _testLaunchTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private WizardPage _page = WizardPage.Game;
    private string _gamePath = "";
    private bool _isSearching;
    private bool _isBusy;
    private string _resultText = "";
    private bool _resultIsError;
    private bool _showAdminRestart;
    private bool _traitsSkipped;

    private DateTime? _ue4ssInstalledAtUtc;
    private List<string> _ue4ssKeyFiles = new();
    private AntivirusState _antivirusState;
    private List<string> _missingAfterAntivirus = new();

    private DateTime _testLaunchStart;
    private TestLaunchState _testLaunchState;

    public SetupWizardViewModel(GamePathService pathService, LocalizationService loc, string? gamePathOverride = null)
    {
        _pathService = pathService;
        Loc = loc;

        Components = new ObservableCollection<ComponentRow>
        {
            new(ComponentKind.AllowAssetMods, loc["Wizard.CompAmm"], loc["Wizard.CompAmmDesc"], PackageInstaller.AllowAssetModsUrl),
            new(ComponentKind.UnlockerMod, loc["Wizard.CompMod"], loc["Wizard.CompModDesc"]),
            new(ComponentKind.SummonPatch, loc["Wizard.CompSummon"], loc["Wizard.CompSummonDesc"]),
            new(ComponentKind.SummonableTraits, loc["Wizard.CompTraits"], loc["Wizard.CompTraitsDesc"], PackageInstaller.SummonableTraitsUrl),
            new(ComponentKind.ModsEnabled, loc["Wizard.CompEnabled"], loc["Wizard.CompEnabledDesc"])
        };

        NextCommand = new RelayCommand(async () => await NextAsync(), () => CanGoNext);
        BackCommand = new RelayCommand(Back, () => CanGoBack);
        BrowseCommand = new RelayCommand(Browse);
        InstallRepairCommand = new RelayCommand(async () => await InstallRepairAsync(), () => !IsBusy);
        RestoreBackupCommand = new RelayCommand(async () => await RestoreBackupAsync(), () => !IsBusy);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        LaunchGameCommand = new RelayCommand(LaunchGame);
        RecheckAntivirusCommand = new RelayCommand(CheckAntivirus);
        OpenWindowsSecurityCommand = new RelayCommand(() => OpenUrl("windowsdefender://threat"));

        _closeGameTimer.Tick += async (_, _) => await PollCloseGameAsync();
        _antivirusTimer.Tick += (_, _) => PollAntivirus();
        _testLaunchTimer.Tick += async (_, _) => await PollTestLaunchAsync();

        var initial = gamePathOverride ?? _pathService.Win64Path;

        if (!string.IsNullOrWhiteSpace(initial))
            GamePath = GameLocator.NormalizeSelection(initial);

        _ = SearchGamesAsync(preferFound: string.IsNullOrWhiteSpace(initial));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the wizard is done (Finish) or wants the app to shut down (admin restart).</summary>
    public event EventHandler<bool>? CloseRequested;

    public LocalizationService Loc { get; }

    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand InstallRepairCommand { get; }
    public RelayCommand RestoreBackupCommand { get; }
    public RelayCommand RestartAsAdminCommand { get; }
    public RelayCommand LaunchGameCommand { get; }
    public RelayCommand RecheckAntivirusCommand { get; }
    public RelayCommand OpenWindowsSecurityCommand { get; }

    public ObservableCollection<InstallOption> DetectedInstalls { get; } = new();

    public ObservableCollection<ComponentRow> Components { get; }

    // ---------------- Navigation ----------------

    public WizardPage Page
    {
        get => _page;
        private set
        {
            _page = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGamePage));
            OnPropertyChanged(nameof(IsCloseGamePage));
            OnPropertyChanged(nameof(IsComponentsPage));
            OnPropertyChanged(nameof(IsAntivirusPage));
            OnPropertyChanged(nameof(IsTestLaunchPage));
            OnPropertyChanged(nameof(IsDonePage));
            OnPropertyChanged(nameof(NextText));
            OnPropertyChanged(nameof(StepText));
            RefreshCommands();
        }
    }

    public bool IsGamePage => Page == WizardPage.Game;
    public bool IsCloseGamePage => Page == WizardPage.CloseGame;
    public bool IsComponentsPage => Page == WizardPage.Components;
    public bool IsAntivirusPage => Page == WizardPage.Antivirus;
    public bool IsTestLaunchPage => Page == WizardPage.TestLaunch;
    public bool IsDonePage => Page == WizardPage.Done;

    public string NextText => Page switch
    {
        WizardPage.Done => Loc["Wizard.Finish"],
        WizardPage.TestLaunch when TestLaunchState != TestLaunchState.Connected => Loc["Wizard.SkipTest"],
        _ => Loc["Wizard.Next"]
    };

    public string StepText => string.Format(Loc["Wizard.Step"], Page switch
    {
        WizardPage.Game => 1,
        WizardPage.CloseGame or WizardPage.Components => 2,
        WizardPage.Antivirus => 3,
        WizardPage.TestLaunch => 4,
        _ => 5
    }, 5);

    public bool CanGoNext => !IsBusy && Page switch
    {
        WizardPage.Game => IsGamePathValid,
        WizardPage.CloseGame => false,
        WizardPage.Components => AreRequiredComponentsOk,
        WizardPage.Antivirus => AntivirusState != AntivirusState.Checking,
        _ => true
    };

    public bool CanGoBack => !IsBusy && Page is WizardPage.Components or WizardPage.CloseGame;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    private async Task NextAsync()
    {
        switch (Page)
        {
            case WizardPage.Game:
                _pathService.SetWin64Path(GamePath);
                AppLogService.Info($"Setup wizard: game folder {GamePath}");
                await GoToComponentsOrCloseGameAsync();
                break;

            case WizardPage.Components:
                if (_ue4ssInstalledAtUtc != null)
                {
                    Page = WizardPage.Antivirus;
                    StartAntivirusCheck();
                }
                else
                {
                    StartTestLaunch();
                }

                break;

            case WizardPage.Antivirus:
                StartTestLaunch();
                break;

            case WizardPage.TestLaunch:
                _testLaunchTimer.Stop();
                Page = WizardPage.Done;
                break;

            case WizardPage.Done:
                CloseRequested?.Invoke(this, true);
                break;
        }
    }

    private void Back()
    {
        _closeGameTimer.Stop();
        ResultText = "";
        ShowAdminRestart = false;
        Page = WizardPage.Game;
    }

    private async Task GoToComponentsOrCloseGameAsync()
    {
        if (GameProcess.IsRunning())
        {
            Page = WizardPage.CloseGame;
            _closeGameTimer.Start();
            return;
        }

        Page = WizardPage.Components;
        await RefreshComponentsAsync();
    }

    private async Task PollCloseGameAsync()
    {
        if (GameProcess.IsRunning())
            return;

        _closeGameTimer.Stop();
        Page = WizardPage.Components;
        await RefreshComponentsAsync();
    }

    // ---------------- Page 1: game folder ----------------

    public string GamePath
    {
        get => _gamePath;
        set
        {
            _gamePath = value?.Trim() ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGamePathValid));
            OnPropertyChanged(nameof(GamePathStatusText));
            RefreshCommands();

            foreach (var option in DetectedInstalls)
                option.Sync(_gamePath);
        }
    }

    public bool IsGamePathValid => GamePathService.HasSupportedGameExe(GamePath);

    public string GamePathStatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(GamePath))
                return Loc["Wizard.GameNotSelected"];

            if (!IsGamePathValid)
                return Loc["Wizard.GameInvalid"];

            var platform = GamePathService.IsGamePassPath(GamePath) ? "Game Pass"
                : GameLocator.IsSteamLibraryPath(GamePath) ? "Steam"
                : "Steam / Epic";

            return string.Format(Loc["Wizard.GameValid"], platform);
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            _isSearching = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NoInstallsFound));
        }
    }

    public bool NoInstallsFound => !IsSearching && DetectedInstalls.Count == 0;

    private async Task SearchGamesAsync(bool preferFound)
    {
        IsSearching = true;

        try
        {
            var found = await Task.Run(GameLocator.FindInstalls);

            DetectedInstalls.Clear();

            foreach (var install in found)
            {
                var option = new InstallOption(install, selected => GamePath = selected.Win64Path);
                option.Sync(GamePath);
                DetectedInstalls.Add(option);
            }

            if (preferFound && string.IsNullOrWhiteSpace(GamePath) && found.Count > 0)
                GamePath = found[0].Win64Path;
        }
        catch (Exception ex)
        {
            AppLogService.Error("Setup wizard: game search failed", ex);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void Browse()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = Loc["Wizard.BrowseDescription"],
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (Directory.Exists(GamePath))
            dialog.SelectedPath = GamePath;

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
            return;

        GamePath = GameLocator.NormalizeSelection(dialog.SelectedPath);
    }

    // ---------------- Page 2: components ----------------

    public string ResultText
    {
        get => _resultText;
        private set
        {
            _resultText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasResult));
        }
    }

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultText);

    public bool ResultIsError
    {
        get => _resultIsError;
        private set
        {
            _resultIsError = value;
            OnPropertyChanged();
        }
    }

    public bool ShowAdminRestart
    {
        get => _showAdminRestart;
        private set
        {
            _showAdminRestart = value;
            OnPropertyChanged();
        }
    }

    public bool AreRequiredComponentsOk => Components
        .Where(c => c.Kind != ComponentKind.SummonableTraits)
        .All(c => c.IsOk);

    private ComponentRow Row(ComponentKind kind) => Components.First(c => c.Kind == kind);

    public async Task RefreshComponentsAsync()
    {
        var win64 = GamePath;

        var snapshot = await Task.Run(() => new
        {
            Ue4ss = PackageInstaller.IsUe4ssInstalled(win64),
            Amm = PackageInstaller.IsAllowModsModInstalled(win64),
            Status = ModInstaller.GetStatus(win64),
            Traits = SummonableTraitsService.FindMainLua(GamePathService.ResolveModsFolder(win64)) != null
        });

        var amm = Row(ComponentKind.AllowAssetMods);

        if (snapshot.Ue4ss && snapshot.Amm)
            Set(amm, ComponentState.Ok, Loc["Wizard.StatusInstalled"]);
        else
            Set(amm, ComponentState.Missing, snapshot.Ue4ss ? Loc["Wizard.StatusAmmMissing"] : Loc["Wizard.StatusUe4ssMissing"]);

        var modFiles = snapshot.Status.Files.Where(f => f.File.Component == ModPayload.UnlockerComponent).ToList();
        var outOfDate = modFiles.Count(f => f.State != ModFileState.UpToDate);
        var mod = Row(ComponentKind.UnlockerMod);

        if (!snapshot.Status.IsModInstalled)
            Set(mod, ComponentState.Automatic, Loc["Wizard.StatusModNotInstalled"]);
        else if (outOfDate > 0)
            Set(mod, ComponentState.Automatic, string.Format(Loc["Wizard.StatusModOutdated"], outOfDate));
        else
            Set(mod, ComponentState.Ok, string.Format(Loc["Wizard.StatusModUpToDate"], UpdateCheckService.GetCurrentVersion()));

        var summon = Row(ComponentKind.SummonPatch);

        if (!snapshot.Status.IsConsoleCommandsModPresent)
            Set(summon, ComponentState.Waiting, Loc["Wizard.StatusNeedsUe4ss"]);
        else if (snapshot.Status.IsSummonPatchApplied)
            Set(summon, ComponentState.Ok, Loc["Wizard.StatusApplied"]);
        else
            Set(summon, ComponentState.Automatic, Loc["Wizard.StatusSummonPending"]);

        var traits = Row(ComponentKind.SummonableTraits);

        if (snapshot.Traits)
            Set(traits, ComponentState.Ok, Loc["Wizard.StatusInstalled"]);
        else if (_traitsSkipped)
            Set(traits, ComponentState.Skipped, Loc["Wizard.StatusSkipped"]);
        else
            Set(traits, ComponentState.Optional, Loc["Wizard.StatusOptional"]);

        var enabled = Row(ComponentKind.ModsEnabled);

        if (snapshot.Status.NotEnabledMods.Count == 0)
            Set(enabled, ComponentState.Ok, Loc["Wizard.StatusAllEnabled"]);
        else
            Set(enabled, ComponentState.Automatic, string.Format(Loc["Wizard.StatusToEnable"], string.Join(", ", snapshot.Status.NotEnabledMods)));

        OnPropertyChanged(nameof(AreRequiredComponentsOk));
        OnPropertyChanged(nameof(CanInstallRepair));
        RefreshCommands();
    }

    /// <summary>Install / Repair does something only when an automatic part isn't done yet.</summary>
    public bool CanInstallRepair => Components.Any(c => c.State == ComponentState.Automatic);

    private static void Set(ComponentRow row, ComponentState state, string text)
    {
        row.State = state;
        row.StatusText = text;
    }

    public void SkipTraits()
    {
        _traitsSkipped = true;
        Set(Row(ComponentKind.SummonableTraits), ComponentState.Skipped, Loc["Wizard.StatusSkipped"]);
    }

    private bool EnsureGameClosed()
    {
        if (!GameProcess.IsRunning())
            return true;

        ShowResult(Loc["Wizard.CloseGameFirst"], isError: true);
        return false;
    }

    private async Task InstallRepairAsync()
    {
        if (!EnsureGameClosed())
            return;

        var win64 = GamePath;
        IsBusy = true;

        try
        {
            var result = await Task.Run(() => ModInstaller.InstallOrUpdate(win64));

            ShowAdminRestart = result.AccessDenied;

            if (result.Success)
            {
                var text = string.Format(Loc["Wizard.InstallDone"], result.ChangedCount);

                if (result.ConfigEdit?.Changed == true)
                    text += " " + string.Format(Loc["Wizard.InstallEnabled"], string.Join(", ", result.ConfigEdit.Flipped.Concat(result.ConfigEdit.Added)));

                if (result.BackupFolder != null)
                    text += " " + string.Format(Loc["Wizard.BackupAt"], result.BackupFolder);

                if (result.ExtraFiles.Count > 0)
                    text += " " + string.Format(Loc["Wizard.ExtraFiles"], result.ExtraFiles.Count);

                ShowResult(text, isError: false);
            }
            else
            {
                var firstError = result.Files.FirstOrDefault(f => f.Outcome == ModFileOutcome.Failed)?.Error ?? result.ConfigError;
                ShowResult(
                    (result.AccessDenied ? Loc["Wizard.AccessDenied"] : string.Format(Loc["Wizard.InstallFailed"], result.FailedCount))
                    + (firstError != null ? $"\n{firstError}" : ""),
                    isError: true);
            }
        }
        finally
        {
            IsBusy = false;
            await RefreshComponentsAsync();
        }
    }

    public async Task InstallPackageAsync(ComponentKind kind, string source)
    {
        if (IsBusy || !EnsureGameClosed())
            return;

        var win64 = GamePath;
        IsBusy = true;
        ShowResult(Loc["Wizard.Installing"], isError: false);

        try
        {
            var result = await Task.Run(() => kind == ComponentKind.AllowAssetMods
                ? PackageInstaller.InstallAllowAssetMods(win64, source)
                : PackageInstaller.InstallSummonableTraits(win64, source));

            ShowAdminRestart = result.AccessDenied;

            if (!result.Success)
            {
                ShowResult(DescribePackageError(result), isError: true);
                return;
            }

            if (kind == ComponentKind.AllowAssetMods)
            {
                _ue4ssInstalledAtUtc = DateTime.UtcNow;
                _ue4ssKeyFiles = new[]
                    {
                        Path.Combine(win64, "UE4SS.dll"),
                        Path.Combine(win64, "ue4ss", "UE4SS.dll"),
                        Path.Combine(win64, "dwmapi.dll")
                    }
                    .Where(File.Exists)
                    .ToList();
            }
            else
            {
                _traitsSkipped = false;
            }

            var text = string.Format(Loc["Wizard.PackageInstalled"], result.FilesWritten);

            if (result.ModSync is { ChangedCount: > 0 } sync)
                text += " " + string.Format(Loc["Wizard.PackageModSynced"], sync.ChangedCount);

            if ((result.BackupFolder ?? result.ModSync?.BackupFolder) is { } backupFolder)
                text += " " + string.Format(Loc["Wizard.BackupAt"], backupFolder);

            if (result.HasLegacyXinputProxy)
                text += "\n" + Loc["Wizard.LegacyXinput"];

            var syncFailed = result.ModSync is { Success: false };
            ShowResult(syncFailed ? text + "\n" + Loc["Wizard.SyncAfterPackageFailed"] : text, isError: syncFailed);
        }
        catch (Exception ex)
        {
            AppLogService.Error("Setup wizard: package install failed", ex);
            ShowResult(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
            await RefreshComponentsAsync();
        }
    }

    private string DescribePackageError(PackageInstallResult result)
    {
        var text = Loc[$"Wizard.PackageError.{result.Error}"];

        return string.IsNullOrWhiteSpace(result.ErrorDetail) || result.Error is PackageError.LooksLikeAllowAssetMods
            or PackageError.LooksLikeSummonableTraits or PackageError.NotAllowAssetMods or PackageError.NotSummonableTraits
            or PackageError.AllowModsModWithoutUe4ss
            ? text
            : $"{text}\n{result.ErrorDetail}";
    }

    private async Task RestoreBackupAsync()
    {
        if (!EnsureGameClosed())
            return;

        var result = System.Windows.MessageBox.Show(
            Loc["Wizard.RestoreConfirm"],
            Loc["Wizard.Title"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        var win64 = GamePath;
        IsBusy = true;

        try
        {
            var restored = await Task.Run(() => ModInstaller.RestoreLatestBackup(win64));

            ShowResult(restored.Count == 0
                ? Loc["Wizard.RestoreNone"]
                : string.Format(Loc["Wizard.RestoreDone"], restored.Count), isError: false);
        }
        catch (Exception ex)
        {
            AppLogService.Error("Setup wizard: restore failed", ex);
            ShowAdminRestart = SafeFile.IsAccessDenied(ex);
            ShowResult(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
            await RefreshComponentsAsync();
        }
    }

    private void ShowResult(string text, bool isError)
    {
        ResultText = text;
        ResultIsError = isError;
    }

    private void RestartAsAdmin()
    {
        var exe = Environment.ProcessPath;

        if (exe == null)
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add("--setup");

        if (!string.IsNullOrWhiteSpace(GamePath))
        {
            startInfo.ArgumentList.Add("--game-path");
            startInfo.ArgumentList.Add(GamePath);
        }

        try
        {
            Process.Start(startInfo);
            AppLogService.Info("Setup wizard: restarting as administrator");
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // 1223 = the user said no at the UAC prompt.
            ShowResult(ex.NativeErrorCode == 1223 ? Loc["Wizard.AdminCancelled"] : ex.Message, isError: true);
        }
    }

    // ---------------- Page 3: antivirus ----------------

    public AntivirusState AntivirusState
    {
        get => _antivirusState;
        private set
        {
            _antivirusState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAntivirusChecking));
            OnPropertyChanged(nameof(IsAntivirusOk));
            OnPropertyChanged(nameof(IsAntivirusRemoved));
            RefreshCommands();
        }
    }

    public bool IsAntivirusChecking => AntivirusState == AntivirusState.Checking;
    public bool IsAntivirusOk => AntivirusState == AntivirusState.Ok;
    public bool IsAntivirusRemoved => AntivirusState == AntivirusState.Removed;

    public string AntivirusMissingText => string.Join("\n", _missingAfterAntivirus);

    private void StartAntivirusCheck()
    {
        AntivirusState = AntivirusState.Checking;
        _antivirusTimer.Start();
    }

    private void PollAntivirus()
    {
        if (_ue4ssInstalledAtUtc != null && DateTime.UtcNow - _ue4ssInstalledAtUtc < AntivirusDelay)
            return;

        _antivirusTimer.Stop();
        CheckAntivirus();
    }

    private void CheckAntivirus()
    {
        _missingAfterAntivirus = _ue4ssKeyFiles.Where(f => !File.Exists(f)).ToList();

        if (!PackageInstaller.IsUe4ssInstalled(GamePath) && _missingAfterAntivirus.Count == 0)
            _missingAfterAntivirus.Add(Path.Combine(GamePath, "UE4SS.dll"));

        OnPropertyChanged(nameof(AntivirusMissingText));
        AntivirusState = _missingAfterAntivirus.Count == 0 ? AntivirusState.Ok : AntivirusState.Removed;

        if (AntivirusState == AntivirusState.Removed)
            AppLogService.Warn($"Setup wizard: files gone after installing UE4SS (antivirus?): {string.Join(", ", _missingAfterAntivirus)}");
    }

    // ---------------- Page 4: test launch ----------------

    public TestLaunchState TestLaunchState
    {
        get => _testLaunchState;
        private set
        {
            _testLaunchState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTestWaiting));
            OnPropertyChanged(nameof(IsTestModLoaded));
            OnPropertyChanged(nameof(IsTestConnected));
            OnPropertyChanged(nameof(IsTestTimedOut));
            OnPropertyChanged(nameof(NextText));
        }
    }

    public bool IsTestWaiting => TestLaunchState == TestLaunchState.Waiting;
    public bool IsTestModLoaded => TestLaunchState == TestLaunchState.ModLoaded;
    public bool IsTestConnected => TestLaunchState == TestLaunchState.Connected;
    public bool IsTestTimedOut => TestLaunchState == TestLaunchState.TimedOut;

    public bool CanLaunchViaSteam => GameLocator.IsSteamLibraryPath(GamePath);

    private void StartTestLaunch()
    {
        Page = WizardPage.TestLaunch;
        OnPropertyChanged(nameof(CanLaunchViaSteam));
        RestartTestWatch();
    }

    private void RestartTestWatch()
    {
        _testLaunchStart = DateTime.Now;
        TestLaunchState = TestLaunchState.Waiting;
        _testLaunchTimer.Start();
    }

    private void LaunchGame()
    {
        RestartTestWatch();
        OpenUrl($"steam://rungameid/{GameLocator.SteamAppId}");
        AppLogService.Info("Setup wizard: test launch via Steam");
    }

    private async Task PollTestLaunchAsync()
    {
        var win64 = GamePath;
        var since = _testLaunchStart;

        var (modLoaded, connected) = await Task.Run(() => CheckBridge(win64, since));

        if (connected)
        {
            _testLaunchTimer.Stop();
            TestLaunchState = TestLaunchState.Connected;
            AppLogService.Info("Setup wizard: test launch -- bridge connected");
            return;
        }

        if (modLoaded)
            TestLaunchState = TestLaunchState.ModLoaded;

        if (DateTime.Now - _testLaunchStart > TestLaunchTimeout)
        {
            _testLaunchTimer.Stop();
            TestLaunchState = TestLaunchState.TimedOut;
            AppLogService.Warn($"Setup wizard: test launch timed out (mod loaded: {modLoaded})");
        }
    }

    /// <summary>
    /// modLoaded: UE4SS.log (rewritten at each game start) was written since <paramref name="since"/>
    /// and has our main.lua line. connected: status.json was written since then with ready = true.
    /// </summary>
    public static (bool ModLoaded, bool Connected) CheckBridge(string win64Path, DateTime since)
    {
        var modLoaded = false;

        foreach (var log in new[] { Path.Combine(win64Path, "UE4SS.log"), Path.Combine(win64Path, "ue4ss", "UE4SS.log") })
        {
            try
            {
                if (!File.Exists(log) || File.GetLastWriteTime(log) < since)
                    continue;

                using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);

                if (reader.ReadToEnd().Contains("[Remnant2Unlocker] main.lua loaded", StringComparison.Ordinal))
                    modLoaded = true;
            }
            catch (IOException)
            {
                // Being written right now; the next poll reads it.
            }
        }

        var statusPath = Path.Combine(GamePathService.ResolveModsFolder(win64Path), ModPayload.UnlockerComponent, "status.json");
        var connected = false;

        try
        {
            if (File.Exists(statusPath) && File.GetLastWriteTime(statusPath) >= since)
            {
                using var stream = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                connected = doc.RootElement.TryGetProperty("ready", out var ready) && ready.ValueKind == System.Text.Json.JsonValueKind.True;
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            // Caught mid-write; the next poll reads it.
        }

        return (modLoaded, connected);
    }

    // ---------------- helpers ----------------

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogService.Warn($"Could not open {url}", ex);
        }
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        NextCommand?.RaiseCanExecuteChanged();
        BackCommand?.RaiseCanExecuteChanged();
        InstallRepairCommand?.RaiseCanExecuteChanged();
        RestoreBackupCommand?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _closeGameTimer.Stop();
        _antivirusTimer.Stop();
        _testLaunchTimer.Stop();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
