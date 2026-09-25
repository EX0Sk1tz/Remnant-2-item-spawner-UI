using MoonSharp.Interpreter;
using Remnant2UnlockerApp.Services;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

// Runs the real Scripts/inventory_cheats.lua (and json.lua) under MoonSharp, with just enough of
// UE4SS faked out to drive it: a player pawn with an Inventory, console command registration,
// LoopAsync, and an in-memory io.open. What the script writes is then fed through the app's own
// parser, so both halves of the owned_items.json contract are checked together.
public class LuaInventoryScanTests
{
    private const string OwnedItemsFile = "Mods/Remnant2Unlocker/owned_items.json";
    private const string InventoryItemsFile = "Mods/Remnant2Unlocker/inventory_items.json";

    private const string FakeUe4ss = """
        __files = {}
        __handlers = {}
        __loops = {}
        __pawn = nil
        __log = {}

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

        function RegisterConsoleCommandHandler(name, fn) __handlers[name] = fn end
        function LoopAsync(ms, fn) table.insert(__loops, fn) end
        function ExecuteInGameThread(fn) fn() end
        function FindFirstOf(name) return nil end

        local function Name(s) return { ToString = function(self) return s end } end

        function MakeItem(className, quantity)
            local itemBP = {
                GetFullName = function(self) return "BlueprintGeneratedClass /Game/Items/" .. className end,
                GetFName = function(self) return Name(className) end,
            }
            local entry = { ItemBP = itemBP, InstanceData = { Quantity = quantity or 1 } }
            return { get = function(self) return entry end }
        end

        -- An inventory slot whose ItemBP isn't loaded: GetFullName on nil throws.
        function MakeBrokenItem()
            return { get = function(self) return { ItemBP = nil } end }
        end

        function MakePlayer(items)
            local player = {
                IsValid = function(self) return true end,
                GetClass = function(self) return { GetFName = function(self) return Name("Character_Master_Player_C") end } end,
            }
            if items ~= nil then
                player.Inventory = { GetItems = function(self) return items end }
            end
            return player
        end

        function RunConsole(name)
            local ar = { Log = function(self, message) table.insert(__log, message) end }
            return __handlers[name](name, {}, ar)
        end
        """;

    private readonly Script _lua;
    private readonly List<string> _printed = new();

    public LuaInventoryScanTests()
    {
        _lua = LuaModuleLoader.CreateScript(new Dictionary<string, string>
        {
            ["json"] = LuaModuleLoader.ReadScript("json"),
            ["UEHelpers"] = "return { GetPlayerController = function() return { Pawn = __pawn } end }",
            ["paths"] = "return { ModDir = 'Mods/Remnant2Unlocker/', File = function(name) return 'Mods/Remnant2Unlocker/' .. name end }",
        }, _printed);

        _lua.DoString(FakeUe4ss);

        var module = _lua.DoString(LuaModuleLoader.ReadScript("inventory_cheats"));
        module.Table.Get("Start").Function.Call();
    }

    [Fact]
    public void ScanInventory_WritesEveryItemClassForTheApp()
    {
        SetInventory("MakeItem('Weapon_AlphaOmega_C')", "MakeItem('Material_Iron_C', 12)", "MakeItem('Weapon_AlphaOmega_C')");

        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        Assert.True(RunConsole("scan_inventory"));

        var snapshot = OwnedItemsService.Parse(ReadFile(OwnedItemsFile), DateTimeOffset.MinValue);

        Assert.NotNull(snapshot);
        Assert.Equal(new[] { "material_iron_c", "weapon_alphaomega_c" }, snapshot!.ClassKeys.OrderBy(x => x));
        Assert.InRange(snapshot.ScannedAt, before, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Contains("Inventory scanned", LastConsoleLog());
    }

    [Fact]
    public void ScanInventory_StillWritesTheMaterialPickerFile()
    {
        SetInventory("MakeItem('Weapon_AlphaOmega_C')", "MakeItem('Material_Iron_C', 12)");

        RunConsole("scan_inventory");

        var json = ReadFile(InventoryItemsFile);
        Assert.Contains("\"Material_Iron_C\"", json);
        Assert.Contains("12", json);
        Assert.DoesNotContain("Weapon_AlphaOmega_C", json);
    }

    [Fact]
    public void ScanInventory_OneUnreadableSlotDoesNotHideTheRest()
    {
        SetInventory("MakeItem('Ring_A_C')", "MakeBrokenItem()", "MakeItem('Amulet_B_C')");

        RunConsole("scan_inventory");

        var snapshot = OwnedItemsService.Parse(ReadFile(OwnedItemsFile), DateTimeOffset.MinValue);
        Assert.Equal(new[] { "amulet_b_c", "ring_a_c" }, snapshot!.ClassKeys.OrderBy(x => x));
    }

    [Fact]
    public void ScanInventory_EmptyInventory_WritesAnEmptyButValidSnapshot()
    {
        SetInventory();

        RunConsole("scan_inventory");

        var snapshot = OwnedItemsService.Parse(ReadFile(OwnedItemsFile), DateTimeOffset.MinValue);
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot!.ClassKeys);
    }

    [Theory]
    [InlineData("__pawn = nil")]                  // main menu / loading screen
    [InlineData("__pawn = MakePlayer(nil)")]      // pawn without an Inventory component
    public void ScanInventory_WhenTheInventoryCantBeRead_KeepsTheLastSnapshot(string setup)
    {
        SetInventory("MakeItem('Ring_A_C')");
        RunConsole("scan_inventory");
        var lastGood = ReadFile(OwnedItemsFile);

        _lua.DoString(setup);
        RunConsole("scan_inventory");

        Assert.Equal(lastGood, ReadFile(OwnedItemsFile));
        Assert.Contains("Inventory scan failed", LastConsoleLog());
    }

    [Fact]
    public void AutoScanLoop_WritesTheSnapshotWhileAPlayerIsLoaded()
    {
        var loop = _lua.Globals.Get("__loops").Table.Get(1).Function;

        loop.Call();
        Assert.True(_lua.Globals.Get("__files").Table.Get(OwnedItemsFile).IsNil());

        SetInventory("MakeItem('Weapon_AlphaOmega_C')");
        loop.Call();

        var snapshot = OwnedItemsService.Parse(ReadFile(OwnedItemsFile), DateTimeOffset.MinValue);
        Assert.Contains("weapon_alphaomega_c", snapshot!.ClassKeys);
    }

    private void SetInventory(params string[] itemExpressions)
    {
        _lua.DoString($"__pawn = MakePlayer({{ {string.Join(", ", itemExpressions)} }})");
    }

    private bool RunConsole(string command) =>
        _lua.Globals.Get("RunConsole").Function.Call(command).Boolean;

    private string ReadFile(string path)
    {
        var value = _lua.Globals.Get("__files").Table.Get(path);
        Assert.False(value.IsNil(), $"{path} was not written. Script output:\n{string.Join("\n", _printed)}");
        return value.String;
    }

    private string LastConsoleLog()
    {
        var log = _lua.Globals.Get("__log").Table;
        return log.Length == 0 ? "" : log.Get(log.Length).String;
    }
}
