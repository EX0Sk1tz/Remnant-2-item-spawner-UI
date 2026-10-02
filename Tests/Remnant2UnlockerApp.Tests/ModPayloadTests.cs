using System.IO;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

public class ModPayloadTests
{
    // Guards the csproj globs: a new script in the repo must end up in the app, byte for byte.
    [Fact]
    public void Payload_ContainsEveryRepoScriptByteForByte()
    {
        var scriptsDir = Path.Combine(RepoPaths.ModSource, "Scripts");
        var repoFiles = Directory.GetFiles(scriptsDir, "*", SearchOption.AllDirectories);

        Assert.NotEmpty(repoFiles);

        foreach (var file in repoFiles)
        {
            var relative = Path.Combine("Scripts", Path.GetRelativePath(scriptsDir, file));
            var entry = ModPayload.Files.SingleOrDefault(f =>
                f.Component == ModPayload.UnlockerComponent
                && f.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));

            Assert.True(entry != null, $"{relative} is not embedded in the app");
            Assert.Equal(File.ReadAllBytes(file), entry!.Content);
        }
    }

    [Theory]
    [InlineData("items.json")]
    [InlineData("enabled.txt")]
    public void Payload_ContainsModRootFile(string name)
    {
        var entry = ModPayload.Files.Single(f => f.Component == ModPayload.UnlockerComponent && f.RelativePath == name);

        Assert.Equal(File.ReadAllBytes(Path.Combine(RepoPaths.ModSource, name)), entry.Content);
    }

    [Theory]
    [InlineData("cheats.json")]
    [InlineData("hotkeys.json")]
    [InlineData("status.json")]
    [InlineData("command_queue.json")]
    public void Payload_DoesNotContainRuntimeFiles(string name)
    {
        Assert.DoesNotContain(ModPayload.Files, f => Path.GetFileName(f.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Payload_OnlyHasKnownComponents()
    {
        Assert.All(ModPayload.Files, f =>
            Assert.Contains(f.Component, new[] { ModPayload.UnlockerComponent, ModPayload.ConsoleCommandsComponent }));
    }

    // The quantity/level-aware summon that runs in-game today, shipped exactly (CRLF, no BOM).
    [Fact]
    public void SummonPatch_IsTheKnownFile()
    {
        var patch = ModPayload.SummonPatch;

        Assert.NotNull(patch);
        Assert.Equal("6D72FC6EF7E1AC4D88206E44FD57DF0E548DCFE587ED5E48A0E5A5D24D9A5597", patch!.Sha256);
    }

    [Fact]
    public void LuaFiles_UseCrlfAndNoBom()
    {
        foreach (var file in ModPayload.Files.Where(f => f.RelativePath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.False(file.Content.Length >= 3 && file.Content[0] == 0xEF && file.Content[1] == 0xBB && file.Content[2] == 0xBF, $"{file} has a BOM");

            for (var i = 0; i < file.Content.Length; i++)
            {
                if (file.Content[i] == (byte)'\n')
                    Assert.True(i > 0 && file.Content[i - 1] == (byte)'\r', $"{file} has a bare LF");
            }
        }
    }
}
