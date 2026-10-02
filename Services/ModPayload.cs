using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// One file of the in-game mod that is embedded in the app. <see cref="Component"/> is the mod folder
/// it belongs to under the UE4SS Mods folder ("Remnant2Unlocker" or "ConsoleCommandsMod"),
/// <see cref="RelativePath"/> its path inside that folder, using '\'.
/// </summary>
public sealed class ModPayloadFile
{
    public ModPayloadFile(string component, string relativePath, byte[] content)
    {
        Component = component;
        RelativePath = relativePath;
        Content = content;
        Sha256 = ModPayload.HashBytes(content);
    }

    public string Component { get; }

    public string RelativePath { get; }

    public byte[] Content { get; }

    public string Sha256 { get; }

    public string GetTargetPath(string modsFolder) => Path.Combine(modsFolder, Component, RelativePath);

    public override string ToString() => $"{Component}\\{RelativePath}";
}

/// <summary>
/// The mod files embedded into the app (see the ModPayload items in the csproj). Copied byte for
/// byte, so line endings and encodings match the repo copy exactly.
/// </summary>
public static class ModPayload
{
    public const string ResourcePrefix = "ModPayload/";

    public const string UnlockerComponent = "Remnant2Unlocker";

    /// <summary>UE4SS's own ConsoleCommandsMod; we only replace its summon handler.</summary>
    public const string ConsoleCommandsComponent = "ConsoleCommandsMod";

    public static readonly string SummonPatchRelativePath = Path.Combine("Scripts", "summon_unloaded_assets.lua");

    private static readonly Lazy<IReadOnlyList<ModPayloadFile>> LazyFiles = new(Load);

    public static IReadOnlyList<ModPayloadFile> Files => LazyFiles.Value;

    public static ModPayloadFile? SummonPatch => Files.FirstOrDefault(f =>
        f.Component == ConsoleCommandsComponent
        && f.RelativePath.Equals(SummonPatchRelativePath, StringComparison.OrdinalIgnoreCase));

    public static string HashBytes(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    public static string? HashFile(string path)
    {
        if (!File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static IReadOnlyList<ModPayloadFile> Load()
    {
        var assembly = typeof(ModPayload).Assembly;
        var files = new List<ModPayloadFile>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;

            // %(RecursiveDir) in the csproj contributes '\', the rest of the name uses '/'.
            var parts = name[ResourcePrefix.Length..]
                .Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                continue;

            files.Add(new ModPayloadFile(parts[0], Path.Combine(parts[1..]), ReadResource(assembly, name)));
        }

        return files
            .OrderBy(f => f.Component, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded mod file missing: {name}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
