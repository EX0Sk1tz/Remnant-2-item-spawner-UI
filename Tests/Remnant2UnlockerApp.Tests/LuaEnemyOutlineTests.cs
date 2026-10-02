using MoonSharp.Interpreter;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

// Runs the real enemy_outline.lua against a fake UE4SS: characters with a Mesh.bRenderCustomDepth
// flag, a GameUtil that records SetRenderCustomDepthForActor calls, and a cheats.json in memory.
public class LuaEnemyOutlineTests
{
    private const string CheatsFile = "Mods/Remnant2Unlocker/cheats.json";

    private const string FakeUe4ss = """
        __files = {}
        __loops = {}
        __gameThread = {}
        __thread = "main"
        __nestedGameThreadCalls = 0
        __calls = {}
        __actors = {}
        __nextAddress = 1
        __findAllOfCalls = 0
        __onNewCharacter = nil

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
                return { read = function(self) return content end, close = function(self) end }
            end
        }

        function LoopAsync(ms, fn) __loops[ms] = fn end
        function ExecuteInGameThread(fn)
            if __thread == "game" then __nestedGameThreadCalls = __nestedGameThreadCalls + 1 end
            table.insert(__gameThread, fn)
        end
        function FindAllOf(className)
            __findAllOfCalls = __findAllOfCalls + 1
            if className == "CharacterGunfire" then return __actors end
            return nil
        end
        function NotifyOnNewObject(className, fn)
            if className == "/Script/GunfireRuntime.CharacterGunfire" then __onNewCharacter = fn end
        end

        local function Valid(self) return not self.destroyed end

        __gameUtil = {
            IsValid = Valid,
            SetRenderCustomDepthForActor = function(self, actor, on, index, propagate)
                table.insert(__calls, { name = actor.name, on = on, index = index })
                actor.Mesh.bRenderCustomDepth = on
            end,
        }

        __combatUtil = {
            IsValid = Valid,
            IsEnemy = function(self, a, b) return b.hostile == true end,
        }

        function StaticFindObject(path)
            if path:find("GameUtil", 1, true) then return __gameUtil end
            if path:find("CombatUtil", 1, true) then return __combatUtil end
            return nil
        end

        function MakeActor(name, hostile, alive)
            local address = __nextAddress
            __nextAddress = __nextAddress + 1
            local actor = {
                name = name, hostile = hostile, alive = alive,
                Mesh = { bRenderCustomDepth = false },
                IsValid = Valid,
                GetFullName = function(self) return "Char " .. self.name end,
                GetAddress = function(self) return address end,
                IsAlive = function(self) return self.alive end,
            }
            table.insert(__actors, actor)
            return actor
        end

        -- A character spawning in-game: constructed, then reported through NotifyOnNewObject.
        function Spawn(name, hostile)
            local actor = MakeActor(name, hostile, true)
            if __onNewCharacter then __onNewCharacter(actor) end
            return actor
        end

        function Actor(name)
            for _, a in ipairs(__actors) do if a.name == name then return a end end
        end

        __player = MakeActor("Player", false, true)

        -- One settings sync + one outline tick, then let the game thread run what they queued.
        function Tick()
            __loops[1000]()
            __loops[500]()
            local jobs = __gameThread
            __gameThread = {}
            __thread = "game"
            for _, job in ipairs(jobs) do job() end
            __thread = "main"
        end

        function CallsFor(name)
            local result = {}
            for _, c in ipairs(__calls) do
                if c.name == name then table.insert(result, (c.on and "on" or "off") .. ":" .. c.index) end
            end
            return table.concat(result, ",")
        end
        """;

    private readonly Script _lua;
    private readonly Table _module;

    public LuaEnemyOutlineTests()
    {
        _lua = LuaModuleLoader.CreateScript(new Dictionary<string, string>
        {
            ["json"] = LuaModuleLoader.ReadScript("json"),
            ["UEHelpers"] = "return { GetPlayerController = function() return { Pawn = __player } end }",
            ["paths"] = "return { File = function(name) return 'Mods/Remnant2Unlocker/' .. name end }",
        }, new List<string>());

        _lua.DoString(FakeUe4ss);
        _lua.DoString("""
            MakeActor("Zombie", true, true)
            MakeActor("Ambusher", true, true)
            MakeActor("Ally", false, true)
            MakeActor("Corpse", true, false)
            MakeActor("Default__Char_Zombie_C", true, true)
            """);

        _module = _lua.DoString(LuaModuleLoader.ReadScript("enemy_outline")).Table;
        _module.Get("Start").Function.Call();
    }

    private void SetFileEnabled(bool enabled) =>
        _lua.DoString($"__files['{CheatsFile}'] = '{{\"noRecoil\": false, \"enemyOutline\": {(enabled ? "true" : "false")}}}'");

    private void Tick() => _lua.Globals.Get("Tick").Function.Call();

    private string CallsFor(string name) => _lua.Globals.Get("CallsFor").Function.Call(name).String;

    private string ReadCheatsFile() => _lua.DoString($"return __files['{CheatsFile}']").String;

    private bool IsOutlined(string name) => _lua.DoString($"return Actor('{name}').Mesh.bRenderCustomDepth").Boolean;

    [Fact]
    public void Off_TouchesNothing()
    {
        SetFileEnabled(false);
        Tick();
        Tick();

        Assert.Equal(0, _lua.DoString("return #__calls").Number);
    }

    [Fact]
    public void On_OutlinesOnlyLivingEnemiesWithTheMarkStencil()
    {
        SetFileEnabled(true);
        Tick();

        Assert.Equal("on:254", CallsFor("Zombie"));
        Assert.Equal("on:254", CallsFor("Ambusher"));
        Assert.Equal("", CallsFor("Player"));
        Assert.Equal("", CallsFor("Ally"));
        Assert.Equal("", CallsFor("Corpse"));
        Assert.Equal("", CallsFor("Default__Char_Zombie_C"));
    }

    [Fact]
    public void On_DoesNotReapplyWhileTheOutlineIsStillOn()
    {
        SetFileEnabled(true);
        Tick();
        Tick();
        Tick();

        Assert.Equal("on:254", CallsFor("Zombie"));
    }

    [Fact]
    public void On_ReappliesWhenTheGameTurnsTheOutlineOff()
    {
        // E.g. a Hunter's Mark expiring on an enemy we had already outlined.
        SetFileEnabled(true);
        Tick();
        _lua.DoString("Actor('Zombie').Mesh.bRenderCustomDepth = false");
        Tick();

        Assert.Equal("on:254,on:254", CallsFor("Zombie"));
    }

    [Fact]
    public void Off_RemovesOurOutlinesButLeavesTheMarksAlone()
    {
        _lua.DoString("Actor('Ambusher').Mesh.bRenderCustomDepth = true"); // marked by the real Hunter's Mark
        SetFileEnabled(true);
        Tick();
        SetFileEnabled(false);
        Tick();

        Assert.Equal("on:254,off:254", CallsFor("Zombie"));
        Assert.Equal("", CallsFor("Ambusher"));
        Assert.False(IsOutlined("Zombie"));
        Assert.True(IsOutlined("Ambusher"));
    }

    [Fact]
    public void EnemyDying_DropsItsOutline()
    {
        SetFileEnabled(true);
        Tick();
        _lua.DoString("Actor('Zombie').alive = false");
        Tick();

        Assert.Equal("on:254,off:254", CallsFor("Zombie"));
    }

    [Fact]
    public void SpawnedEnemies_AreOutlinedOnTheNextTick()
    {
        SetFileEnabled(true);
        Tick();
        _lua.DoString("Spawn('Reinforcement', true)");
        Tick();

        Assert.Equal("on:254", CallsFor("Reinforcement"));
    }

    // FindAllOf walks every object in the game; running it each tick made the game hitch.
    [Fact]
    public void WhileOn_ScansOnceAndThenOnlyFollowsSpawns()
    {
        SetFileEnabled(true);
        Tick();
        _lua.DoString("Spawn('Reinforcement', true)");

        for (var i = 0; i < 60; i++)
            Tick();

        Assert.Equal(1, _lua.DoString("return __findAllOfCalls").Number);
        Assert.Equal("on:254", CallsFor("Reinforcement"));
    }

    [Fact]
    public void WithoutSpawnNotifications_FallsBackToARescanEveryTenSeconds()
    {
        SetFileEnabled(true);
        Tick();
        _lua.DoString("MakeActor('Unreported', true, true)");

        for (var i = 0; i < 19; i++)
            Tick();

        Assert.Equal("", CallsFor("Unreported"));

        Tick();

        Assert.Equal("on:254", CallsFor("Unreported"));
        Assert.Equal(2, _lua.DoString("return __findAllOfCalls").Number);
    }

    [Fact]
    public void Off_DoesNotTrackSpawns()
    {
        SetFileEnabled(false);
        Tick();
        _lua.DoString("Spawn('Reinforcement', true)");
        Tick();

        Assert.Equal(0, _lua.DoString("return __findAllOfCalls").Number);
        Assert.Equal("", CallsFor("Reinforcement"));
    }

    // The app's switch follows the in-game hotkey by watching cheats.json.
    [Fact]
    public void HotkeyToggle_WritesOnlyEnemyOutlineBackToCheatsJson()
    {
        _lua.DoString($"__files['{CheatsFile}'] = '{{\\n  \"noRecoil\": true,\\n  \"enemyOutline\": false\\n}}'");
        Tick();

        _module.Get("Toggle").Function.Call();

        Assert.Equal("{\n  \"noRecoil\": true,\n  \"enemyOutline\": true\n}", ReadCheatsFile());

        _module.Get("Toggle").Function.Call();

        Assert.Equal("{\n  \"noRecoil\": true,\n  \"enemyOutline\": false\n}", ReadCheatsFile());
    }

    [Fact]
    public void HotkeyToggle_AddsTheValueWhenTheFileHasNone()
    {
        _lua.DoString($"__files['{CheatsFile}'] = '{{\"noRecoil\": true}}'");

        _module.Get("Toggle").Function.Call();

        Assert.Contains("\"enemyOutline\":true", ReadCheatsFile());
        Assert.Contains("\"noRecoil\":true", ReadCheatsFile());
    }

    [Fact]
    public void HotkeyToggle_IsNotUndoneByTheNextSettingsSync()
    {
        SetFileEnabled(false);
        Tick();

        _module.Get("Toggle").Function.Call();
        Tick();
        Tick();

        Assert.True(_module.Get("IsEnabled").Function.Call().Boolean);
        Assert.Equal("on:254", CallsFor("Zombie"));

        // A change in the app still wins.
        SetFileEnabled(true);
        Tick();
        SetFileEnabled(false);
        Tick();

        Assert.False(_module.Get("IsEnabled").Function.Call().Boolean);
        Assert.False(IsOutlined("Zombie"));
    }

    [Fact]
    public void DestroyedActors_AreSkippedAndForgotten()
    {
        SetFileEnabled(true);
        Tick();
        _lua.DoString("Actor('Zombie').destroyed = true");
        SetFileEnabled(false);
        Tick();

        Assert.Equal("on:254", CallsFor("Zombie"));
    }

    [Fact]
    public void NeverQueuesGameThreadWorkFromTheGameThread()
    {
        SetFileEnabled(true);
        Tick();
        _module.Get("Toggle").Function.Call();
        Tick();

        Assert.Equal(0, _lua.DoString("return __nestedGameThreadCalls").Number);
    }
}
