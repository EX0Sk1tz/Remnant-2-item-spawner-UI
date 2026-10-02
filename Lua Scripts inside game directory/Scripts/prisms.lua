local UEHelpers = require("UEHelpers")

-- Prisms: "AddPrism <path>" puts one of the seven unique prisms into the player's inventory.
--
-- "summon" can't do it: it spawns a pickup actor, and prisms have none (confirmed in-game
-- 2026-10-01: Scrap summons, every prism path is rejected). The game's own add function works:
--     InventoryComponent::K2_AddItem(TSubclassOf<Item> ItemBP, int Quantity, int ItemLevel,
--                                    enum Reason, bool bAllowAutoEquip) -> FInventoryResult
-- (signature read from the live game). The arguments are fixed here: UE4SS's ForEachProperty
-- breaks on UE5 function parameters with an error pcall can't catch, so they can't be discovered
-- at runtime.
--
-- A console command handler already runs on the game thread, so this never calls
-- ExecuteInGameThread (see queue.lua's threading rule). The app sends "AddPrism <path>" through
-- the queue's console_command action; group spawn routes Prism entries here too.
--
-- The owned check reads Inventory.Items in place. inventory:GetItems() returns a copy that UE4SS
-- reads after it's freed, which crashed the game (UE4SS.dll+0x673CEF; see inventory_cheats.lua).

local Prisms = {}

local PRISM_FOLDER = "/game/events/paragon/prismstone/uniqueprisms/"

local function Out(Ar, message)
    print("[Remnant2Unlocker] " .. message)
    if Ar then pcall(function() Ar:Log(message) end) end
end

local function IsValidObject(obj)
    if not obj then return false end

    local ok, valid = pcall(function()
        if obj.IsValid then
            return obj:IsValid()
        end

        return true
    end)

    return ok and valid == true
end

local function GetInventory()
    local ok, inventory = pcall(function()
        local pc = UEHelpers.GetPlayerController()
        local pawn = pc and pc.Pawn

        if not IsValidObject(pawn) then
            pawn = FindFirstOf("RemnantPlayerCharacter")
        end

        if not IsValidObject(pawn) then return nil end

        return pawn.Inventory
    end)

    if ok and IsValidObject(inventory) then
        return inventory
    end

    return nil
end

-- "/Game/.../PrismOfGreed.PrismOfGreed_C" -> "PrismOfGreed_C"; nil when it isn't a prism path.
function Prisms.ClassNameFromPath(path)
    if type(path) ~= "string" then return nil end

    local value = path:gsub("^%s+", ""):gsub("%s+$", "")

    if value:lower():sub(1, #PRISM_FOLDER) ~= PRISM_FOLDER then
        return nil
    end

    return value:match("%.(PrismOf[%w_]+_C)$")
end

-- true/false, or nil when the inventory can't be read (then nothing is added).
local function OwnsClass(inventory, className)
    local ok, owned = pcall(function()
        local items = inventory.Items
        local count = items:GetArrayNum()

        for i = 1, count do
            local itemBP = items[i].ItemBP

            if IsValidObject(itemBP) and itemBP:GetFName():ToString() == className then
                return true
            end
        end

        return false
    end)

    if ok then return owned end

    return nil
end

local function FindClass(path)
    local cls = StaticFindObject(path)

    if not IsValidObject(cls) then
        pcall(function() LoadAsset(path) end)
        cls = StaticFindObject(path)
    end

    if IsValidObject(cls) then return cls end

    return nil
end

function Prisms.Add(path, Ar)
    local className = Prisms.ClassNameFromPath(path)

    if not className then
        Out(Ar, "AddPrism: not a prism path: " .. tostring(path))
        return false
    end

    local inventory = GetInventory()

    if not inventory then
        Out(Ar, "AddPrism: player inventory not found (load into a character first)")
        return false
    end

    local owned = OwnsClass(inventory, className)

    if owned == nil then
        Out(Ar, "AddPrism: inventory not readable, nothing added")
        return false
    end

    if owned then
        Out(Ar, "AddPrism: " .. className .. " is already in the inventory")
        return false
    end

    local cls = FindClass(path)

    if not cls then
        Out(Ar, "AddPrism: class not found: " .. path)
        return false
    end

    -- Quantity 1, ItemLevel 0, Reason 0 (first enum value), no auto-equip.
    local ok, err = pcall(function() inventory:K2_AddItem(cls, 1, 0, 0, false) end)

    if not ok then
        Out(Ar, "AddPrism: K2_AddItem failed: " .. tostring(err))
        return false
    end

    if OwnsClass(inventory, className) then
        Out(Ar, "AddPrism: added " .. className)
        return true
    end

    Out(Ar, "AddPrism: K2_AddItem returned, but " .. className .. " isn't in the inventory")
    return false
end

function Prisms.Start()
    -- Always handled, so an error is reported instead of "Command not recognized".
    local function Handler(FullCommand, Parameters, Ar)
        local ok, err = pcall(Prisms.Add, Parameters[1], Ar)

        if not ok then
            Out(Ar, "AddPrism failed: " .. tostring(err))
        end

        return true
    end

    RegisterConsoleCommandHandler("AddPrism", Handler)
    RegisterConsoleCommandHandler("addprism", Handler)

    print("[Remnant2Unlocker] Prisms initialized (AddPrism <path>)")
end

return Prisms
