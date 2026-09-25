using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

namespace Remnant2UnlockerApp.Tests;

// Resolves require("name") from in-memory sources instead of the file system, and builds a
// MoonSharp script that looks enough like UE4SS's Lua environment for the mod scripts to load.
internal sealed class LuaModuleLoader : ScriptLoaderBase
{
    private readonly Dictionary<string, string> _modules;

    public LuaModuleLoader(Dictionary<string, string> modules)
    {
        _modules = modules;
        ModulePaths = new[] { "?" };
    }

    public static string ScriptsFolder => Path.Combine(AppContext.BaseDirectory, "TestData", "Scripts");

    public static string ReadScript(string name) => File.ReadAllText(Path.Combine(ScriptsFolder, name + ".lua"));

    public static Script CreateScript(Dictionary<string, string> modules, List<string> printed)
    {
        // MoonSharp's own "json" module would shadow the mod's json.lua on require("json").
        var script = new Script(CoreModules.Preset_Complete & ~CoreModules.Json);

        script.Options.DebugPrint = printed.Add;
        script.Options.ScriptLoader = new LuaModuleLoader(modules);

        return script;
    }

    public override bool ScriptFileExists(string name) => _modules.ContainsKey(name);

    public override object LoadFile(string file, Table globalContext) => _modules[file];
}
