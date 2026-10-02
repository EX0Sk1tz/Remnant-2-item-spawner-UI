using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class AppSelfInstallerTests
{
    private const string Downloaded = @"C:\Users\Someone\Downloads\Remnant2Unlocker\Remnant2UnlockerApp.exe";

    [Fact]
    public void ShouldOffer_SingleFileFromDownloads_FirstStart()
    {
        Assert.True(AppSelfInstaller.ShouldOfferInstall(true, Downloaded, null, promptHandled: false));
    }

    [Fact]
    public void ShouldOffer_NotForDevBuilds()
    {
        Assert.False(AppSelfInstaller.ShouldOfferInstall(false, Downloaded, null, promptHandled: false));
    }

    [Fact]
    public void ShouldOffer_NotAfterTheUserAnswered()
    {
        Assert.False(AppSelfInstaller.ShouldOfferInstall(true, Downloaded, null, promptHandled: true));
    }

    [Fact]
    public void ShouldOffer_NotWhenRunningFromTheInstallFolder()
    {
        Assert.False(AppSelfInstaller.ShouldOfferInstall(true, @"D:\Apps\R2U\Remnant2UnlockerApp.exe", @"D:\Apps\R2U\", promptHandled: false));
        Assert.False(AppSelfInstaller.ShouldOfferInstall(
            true, AppSelfInstaller.GetTargetExe(AppSelfInstaller.DefaultInstallDir), null, promptHandled: false));
    }

    [Fact]
    public void Install_CopiesTheExe_OverwritesAnOlderOne_AndCreatesTheShortcut()
    {
        using var temp = new FakeGameFolder(withUe4ss: false);
        var source = temp.PathOf(@"download\Remnant2UnlockerApp.exe");
        temp.Write(@"download\Remnant2UnlockerApp.exe", "new version");
        temp.Write(@"install\Remnant2UnlockerApp.exe", "old version");
        var shortcut = temp.PathOf("Remnant 2 Unlocker.lnk");

        var installed = AppSelfInstaller.Install(source, temp.PathOf("install"), shortcut);

        Assert.Equal(temp.PathOf(@"install\Remnant2UnlockerApp.exe"), installed);
        Assert.Equal("new version", File.ReadAllText(installed));
        Assert.True(File.Exists(shortcut));

        // The .lnk stores the target path (as UTF-16 in its link info / string data).
        var lnk = File.ReadAllBytes(shortcut);
        Assert.Contains("Remnant2UnlockerApp.exe", System.Text.Encoding.Unicode.GetString(lnk) + System.Text.Encoding.Default.GetString(lnk));
    }

    [Fact]
    public void Install_AlsoCopiesWpfNativeDllsNextToTheExe()
    {
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"download\Remnant2UnlockerApp.exe", "exe");
        temp.Write(@"download\wpfgfx_cor3.dll", "native");
        temp.Write(@"download\unrelated.dll", "other");

        AppSelfInstaller.Install(temp.PathOf(@"download\Remnant2UnlockerApp.exe"), temp.PathOf("install"), null);

        Assert.Equal("native", File.ReadAllText(temp.PathOf(@"install\wpfgfx_cor3.dll")));
        Assert.False(File.Exists(temp.PathOf(@"install\unrelated.dll")));
    }

    [Fact]
    public void Install_FromTheInstallFolderItself_DoesNotCopy()
    {
        using var temp = new FakeGameFolder(withUe4ss: false);
        temp.Write(@"install\Remnant2UnlockerApp.exe", "same");

        var installed = AppSelfInstaller.Install(temp.PathOf(@"install\Remnant2UnlockerApp.exe"), temp.PathOf("install"), null);

        Assert.Equal("same", File.ReadAllText(installed));
    }
}
