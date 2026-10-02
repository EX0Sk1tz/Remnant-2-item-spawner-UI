using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Remnant2UnlockerApp.Services;
using Forms = System.Windows.Forms;

namespace Remnant2UnlockerApp.Views;

/// <summary>
/// First start of the released exe: install it into a fixed folder (optionally with a desktop
/// shortcut) or keep running it from where it is. <see cref="InstalledExe"/> is set after installing.
/// </summary>
public partial class InstallAppWindow : Window, INotifyPropertyChanged
{
    private string _installDir;
    private bool _createShortcut = true;
    private string _errorText = "";

    public InstallAppWindow(LocalizationService loc, string installDir)
    {
        Loc = loc;
        _installDir = installDir;

        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService Loc { get; }

    public string? InstalledExe { get; private set; }

    public string InstallDir
    {
        get => _installDir;
        set
        {
            _installDir = value?.Trim() ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasExistingInstall));
        }
    }

    public bool HasExistingInstall
    {
        get
        {
            try
            {
                return InstallDir.Length > 0 && File.Exists(AppSelfInstaller.GetTargetExe(InstallDir));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public bool CreateShortcut
    {
        get => _createShortcut;
        set
        {
            _createShortcut = value;
            OnPropertyChanged();
        }
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            _errorText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => ErrorText.Length > 0;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = Loc["Install.Folder"],
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (Directory.Exists(InstallDir))
            dialog.SelectedPath = InstallDir;

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            InstallDir = dialog.SelectedPath;
    }

    private void RunPortable_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var source = Environment.ProcessPath;

        if (source == null || string.IsNullOrWhiteSpace(InstallDir) || !Path.IsPathFullyQualified(InstallDir))
        {
            ErrorText = Loc["Install.InvalidFolder"];
            return;
        }

        try
        {
            InstalledExe = AppSelfInstaller.Install(
                source,
                InstallDir,
                CreateShortcut ? AppSelfInstaller.DesktopShortcutPath : null);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogService.Error($"Self-install into {InstallDir} failed", ex);

            ErrorText = SafeFile.IsAccessDenied(ex) ? Loc["Install.AccessDenied"]
                : ex is IOException ? Loc["Install.InUse"] + "\n" + ex.Message
                : ex.Message;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
