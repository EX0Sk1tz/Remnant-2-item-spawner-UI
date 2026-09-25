using System.Text.Json;
using MoonSharp.Interpreter;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

// Runs the real queue.lua + spawner.lua with a fake clock and a fake game thread that only gets
// to run queued work every so often. Regression test for the "Spawn Missing" crash: with a fixed
// 100ms timer, spawns were queued faster than the game thread finished them and then ran
// back-to-back. The batch must wait for each spawn to finish before sending the next one.
public class LuaSafeSpawnPacingTests
{
    private const string StatusFile = "Mods/Remnant2Unlocker/status.json";

    private const string FakeUe4ss = """
        __files = { ["Mods/Remnant2Unlocker/items.json"] = "[]" }
        __now = 0
        __delayed = {}
        __gameThread = {}
        __commands = {}
        __actors = {}
        __findAllOfCalls = 0

        -- Which fake thread is running: "async" inside ExecuteWithDelay callbacks, "game" inside
        -- ExecuteInGameThread callbacks.
        __thread = "main"
        __nestedGameThreadCalls = 0

        io = {
            open = function(path, mode)
                if mode == "w" then
                    local parts = {}
                    return {
                        write = function(self, s) table.insert(parts, s) end,
                        close = function(self) __files[path] = table.concat(parts) end,
                    }
                end

                local content = __files[path]
                if content == nil then return nil end
                return {
                    read = function(self) return content end,
                    lines = function(self) return content:gmatch("[^\n]+") end,
                    close = function(self) end,
                }
            end
        }

        __decodes = 0

        function ExecuteWithDelay(ms, fn) table.insert(__delayed, { due = __now + ms, fn = fn }) end
        -- Queuing game-thread work from inside a game-thread callback crashed the real UE4SS.
        function ExecuteInGameThread(fn)
            if __thread == "game" then __nestedGameThreadCalls = __nestedGameThreadCalls + 1 end
            table.insert(__gameThread, fn)
        end
        function FindAllOf(className)
            __findAllOfCalls = __findAllOfCalls + 1
            return __actors[className]
        end

        local function Valid() return true end

        -- "summon <path> ..." creates one actor of the path's class, like the real console command.
        __ksl = {
            IsValid = Valid,
            ExecuteConsoleCommand = function(self, context, command, player)
                table.insert(__commands, command)
                local className = command:match("%.([%w_]+)")
                __actors[className] = __actors[className] or {}
                local name = className .. "_" .. tostring(#__actors[className] + 1)
                table.insert(__actors[className], { IsValid = Valid, GetFullName = function(self) return name end })
            end,
        }

        -- Advances the fake clock in 10ms steps. The game thread only drains its queue every
        -- gameThreadEveryMs (nil = never, i.e. a loading screen). Returns the largest number of
        -- callbacks ever waiting on the game thread at once.
        function Advance(totalMs, gameThreadEveryMs)
            local maxBacklog = #__gameThread
            local nextGameTick = __now + (gameThreadEveryMs or 0)

            for _ = 1, totalMs / 10 do
                __now = __now + 10

                local ran = true
                while ran do
                    ran = false
                    for i, job in ipairs(__delayed) do
                        if job.due <= __now then
                            table.remove(__delayed, i)
                            __thread = "async"
                            job.fn()
                            __thread = "main"
                            ran = true
                            break
                        end
                    end
                end

                if #__gameThread > maxBacklog then maxBacklog = #__gameThread end

                if gameThreadEveryMs and __now >= nextGameTick then
                    nextGameTick = __now + gameThreadEveryMs
                    local jobs = __gameThread
                    __gameThread = {}
                    __thread = "game"
                    for _, job in ipairs(jobs) do job() end
                    __thread = "main"
                end
            end

            return maxBacklog
        end

        -- Same shape the app sends: the paths in spawn_list.txt, the command only names the file.
        function QueueSpawnMany(id, count, delayMs)
            local lines = {}
            for i = 1, count do
                table.insert(lines, "/Game/Items/Ring_" .. i .. ".Ring_" .. i .. "_C")
            end
            __files["Mods/Remnant2Unlocker/spawn_list.txt"] = table.concat(lines, "\r\n") .. "\r\n"
            __files["Mods/Remnant2Unlocker/command_queue.json"] = string.format(
                '{"id":%d,"action":"spawn_many_safe","pathsFile":"spawn_list.txt","stackSize":1,"delayMs":%d}',
                id, delayMs)
        end
        """;

    private readonly Script _lua;
    private readonly List<string> _printed = new();

    public LuaSafeSpawnPacingTests()
    {
        _lua = LuaModuleLoader.CreateScript(new Dictionary<string, string>
        {
            ["json"] = LuaModuleLoader.ReadScript("json"),
            ["spawner"] = LuaModuleLoader.ReadScript("spawner"),
            ["paths"] = "return { ModDir = 'Mods/Remnant2Unlocker/', File = function(name) return 'Mods/Remnant2Unlocker/' .. name end }",
            ["UEHelpers"] = """
                local valid = { IsValid = function() return true end }
                return {
                    GetKismetSystemLibrary = function() return __ksl end,
                    GetWorldContextObject = function() return valid end,
                    GetPlayerController = function() return valid end,
                }
                """,
        }, _printed);

        _lua.DoString(FakeUe4ss);
        _lua.DoString(LuaModuleLoader.ReadScript("queue")).Table.Get("Start").Function.Call();
    }

    [Fact]
    public void SlowGameThread_NeverGetsMoreThanOneSpawnAtATime()
    {
        _lua.DoString("QueueSpawnMany(1, 10, 100)");

        // Each spawn needs up to 150ms of game-thread time -- slower than the 100ms delay.
        var maxBacklog = Advance(10_000, gameThreadEveryMs: 150);

        Assert.Equal(1, maxBacklog);
        Assert.Equal(10, Commands().Count);

        var status = Status();
        Assert.Equal(10, status.GetProperty("processedCount").GetInt32());
        Assert.Contains("finished", status.GetProperty("lastMessage").GetString());
    }

    [Fact]
    public void FastGameThread_StillWaitsTheDelayBetweenSpawns()
    {
        _lua.DoString("QueueSpawnMany(1, 5, 300)");

        // 5 items, 300ms apart once each finishes: can't all be done after 1s, must be after 3s.
        Advance(1_000, gameThreadEveryMs: 10);
        Assert.InRange(Commands().Count, 1, 4);

        Advance(2_000, gameThreadEveryMs: 10);
        Assert.Equal(5, Commands().Count);
    }

    [Fact]
    public void PausedGameThread_PilesNothingUp_AndTheBatchResumes()
    {
        _lua.DoString("QueueSpawnMany(1, 10, 100)");
        Advance(1_000, gameThreadEveryMs: 50);

        var sentBeforePause = Commands().Count;

        // Loading screen: the game thread runs nothing for 15s. Neither the bridge's own checks
        // nor the batch may queue up more work in the meantime.
        var maxBacklog = Advance(15_000, gameThreadEveryMs: null);

        // Still just the one summon waiting, however long the pause.
        Assert.Equal(1, maxBacklog);
        Assert.Equal(sentBeforePause, Commands().Count);

        Advance(10_000, gameThreadEveryMs: 50);

        Assert.Equal(10, Commands().Count);
        Assert.Contains("finished", Status().GetProperty("lastMessage").GetString());
    }

    // Running the bridge on the game thread made it queue game-thread work from inside a
    // game-thread callback, and the real game crashed on the first console command. Every kind of
    // command must queue its game-thread work from the async thread only.
    [Fact]
    public void NoCommand_QueuesGameThreadWorkFromInsideAGameThreadCallback()
    {
        _lua.DoString("QueueSpawnMany(1, 3, 100)");
        Advance(3_000, gameThreadEveryMs: 50);

        _lua.DoString("""__files["Mods/Remnant2Unlocker/command_queue.json"] = '{"id":2,"action":"spawn","path":"/Game/Items/Ring_X.Ring_X_C"}'""");
        Advance(1_000, gameThreadEveryMs: 50);

        _lua.DoString("""__files["Mods/Remnant2Unlocker/command_queue.json"] = '{"id":3,"action":"console_command","command":"scan_inventory"}'""");
        Advance(1_000, gameThreadEveryMs: 50);

        Assert.Equal(5, Commands().Count);
        Assert.Equal("scan_inventory", Commands().Last());
        Assert.Equal(0, (int)_lua.Globals.Get("__nestedGameThreadCalls").Number);
    }

    [Fact]
    public void BatchSpawns_SkipTheFindAllOfScans_ButSingleSpawnsKeepThem()
    {
        _lua.DoString("QueueSpawnMany(1, 5, 100)");
        Advance(5_000, gameThreadEveryMs: 50);

        Assert.Equal(5, Commands().Count);
        Assert.Equal(0, (int)_lua.Globals.Get("__findAllOfCalls").Number);

        // DestroyNearbySpawned still learns the classes.
        var spawnedClasses = _lua.Globals.Get("__files").Table.Get("Mods/Remnant2Unlocker/spawned_classes.json").String;
        Assert.Contains("Ring_5_C", spawnedClasses);

        _lua.DoString("""__files["Mods/Remnant2Unlocker/command_queue.json"] = '{"id":2,"action":"spawn","path":"/Game/Items/Ring_X.Ring_X_C"}'""");
        Advance(1_000, gameThreadEveryMs: 50);

        Assert.True(_lua.Globals.Get("__findAllOfCalls").Number > 0);
    }

    [Fact]
    public void Cancel_StopsBeforeTheNextSpawn()
    {
        _lua.DoString("QueueSpawnMany(1, 10, 100)");
        Advance(600, gameThreadEveryMs: 50);

        var sentBeforeCancel = Commands().Count;
        _lua.DoString("""__files["Mods/Remnant2Unlocker/command_queue.json"] = '{"id":2,"action":"cancel"}'""");
        Advance(2_000, gameThreadEveryMs: 50);

        Assert.InRange(Commands().Count, sentBeforeCancel, sentBeforeCancel + 1);
        Assert.Contains("cancelled", Status().GetProperty("lastMessage").GetString());
    }

    [Fact]
    public void SpawnList_PathsAreTrimmedAndSpawnedInOrder()
    {
        _lua.DoString("QueueSpawnMany(1, 3, 100)");
        Advance(3_000, gameThreadEveryMs: 50);

        Assert.Equal(
            new[]
            {
                "summon /Game/Items/Ring_1.Ring_1_C 1 1",
                "summon /Game/Items/Ring_2.Ring_2_C 1 1",
                "summon /Game/Items/Ring_3.Ring_3_C 1 1",
            },
            Commands());
    }

    [Fact]
    public void InlinePaths_StillWork()
    {
        _lua.DoString("""
            __files["Mods/Remnant2Unlocker/command_queue.json"] =
                '{"id":1,"action":"spawn_many_safe","paths":["/Game/Items/Ring_A.Ring_A_C"],"delayMs":100}'
            """);
        Advance(2_000, gameThreadEveryMs: 50);

        Assert.Equal(new[] { "summon /Game/Items/Ring_A.Ring_A_C 1 1" }, Commands());
    }

    [Theory]
    [InlineData("missing.txt", "Missing spawn list")]
    [InlineData("../items.json", "Invalid spawn list name")]
    [InlineData("Scripts/main.lua", "Invalid spawn list name")]
    public void BadSpawnList_FailsCleanlyAndFreesTheBridge(string fileName, string expectedError)
    {
        _lua.DoString($$"""
            __files["Mods/Remnant2Unlocker/command_queue.json"] =
                '{"id":1,"action":"spawn_many_safe","pathsFile":"{{fileName}}","delayMs":100}'
            """);
        Advance(1_000, gameThreadEveryMs: 50);

        Assert.Empty(Commands());
        Assert.Equal(expectedError, Status().GetProperty("error").GetString());
        Assert.False(Status().GetProperty("busy").GetBoolean());

        // The bridge must accept the next command.
        _lua.DoString("QueueSpawnMany(2, 1, 100)");
        Advance(1_000, gameThreadEveryMs: 50);
        Assert.Single(Commands());
    }

    [Fact]
    public void UnchangedCommandFile_IsNotParsedAgainEveryTick()
    {
        _lua.DoString("QueueSpawnMany(1, 1, 100)");
        Advance(1_000, gameThreadEveryMs: 50);

        // Count json.decode calls from here on, while the same command file stays in place.
        _lua.DoString("""
            local json = require("json")
            local decode = json.decode
            json.decode = function(...) __decodes = __decodes + 1; return decode(...) end
            """);
        Advance(2_000, gameThreadEveryMs: 50);

        Assert.Equal(0, (int)_lua.Globals.Get("__decodes").Number);
    }

    private int Advance(int totalMs, int? gameThreadEveryMs) =>
        (int)_lua.Globals.Get("Advance").Function
            .Call(totalMs, gameThreadEveryMs.HasValue ? DynValue.NewNumber(gameThreadEveryMs.Value) : DynValue.Nil)
            .Number;

    private List<string> Commands() =>
        _lua.Globals.Get("__commands").Table.Values.Select(x => x.String).ToList();

    private JsonElement Status()
    {
        var json = _lua.Globals.Get("__files").Table.Get(StatusFile).String;
        Assert.False(string.IsNullOrEmpty(json), "status.json was not written. Script output:\n" + string.Join("\n", _printed));
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
