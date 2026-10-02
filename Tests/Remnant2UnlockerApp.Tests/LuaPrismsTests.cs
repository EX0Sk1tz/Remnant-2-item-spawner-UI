using MoonSharp.Interpreter;
using Xunit;

namespace Remnant2UnlockerApp.Tests;

// Runs the real Scripts/prisms.lua under MoonSharp with a fake UE4SS: a player whose inventory
// exposes Items like UE4SS exposes a TArray property (in place, 1-based, GetArrayNum) and a
// K2_AddItem that appends to it. GetItems() throws: it returns a copy UE4SS reads after it's freed,
// which crashed the game, so the script must never call it.
public class LuaPrismsTests
{
    private const string Greed = "/Game/Events/Paragon/PrismStone/UniquePrisms/PrismOfGreed.PrismOfGreed_C";
    private const string Pride = "/Game/Events/Paragon/PrismStone/UniquePrisms/PrismOfPride.PrismOfPride_C";

    private const string FakeUe4ss = """
        __handlers = {}
        __addCalls = {}
        __log = {}
        __pawn = nil

        function RegisterConsoleCommandHandler(name, fn) __handlers[name] = fn end
        function FindFirstOf(name) return nil end
        function LoadAsset(path) end

        local function Name(s) return { ToString = function(self) return s end } end
        local function Valid(self) return true end

        local function MakeClass(className)
            return { IsValid = Valid, GetFName = function(self) return Name(className) end }
        end

        -- Every prism class is loaded, like in the live game.
        function StaticFindObject(path)
            local className = path:match("%.([%w_]+)$")
            if className and className:find("^PrismOf") then return MakeClass(className) end
            return nil
        end

        function MakePlayer(ownedClassNames)
            local array = {}
            for i, className in ipairs(ownedClassNames) do array[i] = { ItemBP = MakeClass(className) } end
            array.GetArrayNum = function(self) return #self end

            local inventory = {
                IsValid = Valid,
                Items = array,
                GetItems = function(self) error("GetItems() must not be used: read Inventory.Items in place") end,
                K2_AddItem = function(self, cls, quantity, level, reason, autoEquip)
                    table.insert(__addCalls, { cls = cls:GetFName():ToString(), quantity = quantity, level = level, reason = reason, autoEquip = autoEquip })
                    table.insert(array, { ItemBP = cls })
                end,
            }

            return { IsValid = Valid, Inventory = inventory }
        end

        function RunConsole(name, arg)
            local ar = { Log = function(self, message) table.insert(__log, message) end }
            return __handlers[name](name .. " " .. tostring(arg), { arg }, ar)
        end
        """;

    private readonly Script _lua;
    private readonly List<string> _printed = new();

    public LuaPrismsTests()
    {
        _lua = LuaModuleLoader.CreateScript(new Dictionary<string, string>
        {
            ["UEHelpers"] = "return { GetPlayerController = function() return { Pawn = __pawn } end }",
        }, _printed);

        _lua.DoString(FakeUe4ss);
        _lua.DoString(LuaModuleLoader.ReadScript("prisms")).Table.Get("Start").Function.Call();
    }

    [Fact]
    public void AddPrism_AddsAMissingPrismWithTheGamesAddFunction()
    {
        _lua.DoString("__pawn = MakePlayer({ 'PrismOfVoracity_C' })");

        Assert.True(RunConsole("AddPrism", Greed));

        var call = _lua.Globals.Get("__addCalls").Table.Get(1).Table;
        Assert.Equal("PrismOfGreed_C", call.Get("cls").String);
        Assert.Equal(1, call.Get("quantity").Number);
        Assert.Equal(0, call.Get("level").Number);
        Assert.False(call.Get("autoEquip").Boolean);
        Assert.Contains("added PrismOfGreed_C", LastConsoleLog());
    }

    [Fact]
    public void AddPrism_SkipsAPrismThePlayerAlreadyHas()
    {
        _lua.DoString("__pawn = MakePlayer({ 'PrismOfGreed_C' })");

        RunConsole("AddPrism", Greed);

        Assert.Equal(0, AddCallCount());
        Assert.Contains("already in the inventory", LastConsoleLog());
    }

    [Fact]
    public void LowerCaseCommand_Works()
    {
        _lua.DoString("__pawn = MakePlayer({})");

        RunConsole("addprism", Pride);

        Assert.Equal(1, AddCallCount());
    }

    [Theory]
    [InlineData("/Game/World_Base/Items/Materials/Scraps/Material_Scraps.Material_Scraps_C")]
    [InlineData("/Game/Events/Paragon/PrismStone/UniquePrisms/NotAPrism.NotAPrism_C")]
    [InlineData("")]
    public void AddPrism_RefusesAnythingThatIsntAPrism(string path)
    {
        _lua.DoString("__pawn = MakePlayer({})");

        Assert.True(RunConsole("AddPrism", path));

        Assert.Equal(0, AddCallCount());
        Assert.Contains("not a prism path", LastConsoleLog());
    }

    [Fact]
    public void NoCharacterLoaded_ReportsItAndAddsNothing()
    {
        _lua.DoString("__pawn = nil");

        Assert.True(RunConsole("AddPrism", Greed));

        Assert.Equal(0, AddCallCount());
        Assert.Contains("inventory not found", LastConsoleLog());
    }

    // Matches the real path case-insensitively, as UE does.
    [Fact]
    public void PathCase_DoesNotMatter()
    {
        _lua.DoString("__pawn = MakePlayer({})");

        RunConsole("AddPrism", Greed.ToLowerInvariant().Replace("prismofgreed_c", "PrismOfGreed_C"));

        Assert.Equal(1, AddCallCount());
    }

    private bool RunConsole(string command, string arg) =>
        _lua.Globals.Get("RunConsole").Function.Call(command, arg).Boolean;

    private int AddCallCount() => _lua.Globals.Get("__addCalls").Table.Length;

    private string LastConsoleLog()
    {
        var log = _lua.Globals.Get("__log").Table;
        return log.Length == 0 ? "(nothing logged) " + string.Join(" | ", _printed) : log.Get(log.Length).String;
    }
}
