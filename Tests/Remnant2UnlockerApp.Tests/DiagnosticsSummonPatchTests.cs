using Remnant2UnlockerApp.Models;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class DiagnosticsSummonPatchTests
{
    private static DiagnosticCheckResult? SummonResult(FakeGameFolder game)
    {
        var pathService = new GamePathService();
        pathService.Settings.Win64Path = game.Root; // in memory only, never saved
        var diagnostics = new DiagnosticsService(pathService, new SummonableTraitsService(pathService));

        return diagnostics.Run(summonableTraitsInstalled: false).Results
            .FirstOrDefault(r => r.Title.StartsWith("Stack size & item level support"));
    }

    [Fact]
    public void StockSummon_IsAnInfoEntry()
    {
        using var game = new FakeGameFolder();

        var result = SummonResult(game);

        Assert.NotNull(result);
        Assert.Equal(DiagnosticStatus.Info, result!.Status);
    }

    [Fact]
    public void PatchedSummon_Passes()
    {
        using var game = new FakeGameFolder();
        ModInstaller.InstallOrUpdate(game.Root);

        var result = SummonResult(game);

        Assert.NotNull(result);
        Assert.Equal(DiagnosticStatus.Passed, result!.Status);
    }
}
