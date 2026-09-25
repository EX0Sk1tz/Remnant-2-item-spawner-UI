local UEHelpers = require("UEHelpers")
local json = require("json")

local InventoryCheats = {}

local INVENTORY_ITEMS_PATH = require("paths").File("inventory_items.json")
local OWNED_ITEMS_PATH = require("paths").File("owned_items.json")

local knownItemMappings = {
    Iron = "Material_Iron_C",
}

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

local function GetPlayerPawn()
    local ok, pawn = pcall(function()
        local pc = UEHelpers.GetPlayerController()

        if pc and pc.Pawn then
            return pc.Pawn
        end

        return FindFirstOf("RemnantPlayerCharacter")
    end)

    if ok and IsValidObject(pawn) then
        return pawn
    end

    return nil
end

local function IsRealPlayerCharacter(player)
    if not IsValidObject(player) then
        return false
    end

    local ok, className = pcall(function() return player:GetClass():GetFName():ToString() end)

    return ok and className ~= nil and tostring(className):find("Character_.-Player") ~= nil
end

local function WriteAllText(path, content)
    local file = io.open(path, "w")
    if not file then return false end

    file:write(content)
    file:close()

    return true
end

-- Returns true only if the whole inventory was walked. Callers that publish a snapshot rely on
-- this to tell "the player owns nothing" apart from "the inventory couldn't be read".
local function RunFnInPlayerInv(fn)
    local player = GetPlayerPawn()

    if not player then
        print("[Remnant2Unlocker] Inventory command: player pawn not found")
        return false
    end

    local ok, result = pcall(function()
        local inventory = player.Inventory
        if not inventory then return false end

        local items = inventory:GetItems()
        if not items then return false end

        for _, item in pairs(items) do
            -- One entry with a missing/unloaded ItemBP must not abort the scan for every other item.
            local okConfig, config = pcall(function()
                local entry = item:get()
                local itemBP = entry.ItemBP
                local fullName = itemBP:GetFullName()

                return {
                    itemBP = itemBP,
                    instanceData = entry.InstanceData,
                    ismaterial = fullName:find("Material") ~= nil,
                    isconsumable = fullName:find("Consumable") ~= nil,
                    isweapon = fullName:find("Weapon") ~= nil,
                }
            end)

            if okConfig and config and fn(config) then
                break
            end
        end

        return true
    end)

    if not ok then
        print("[Remnant2Unlocker] Inventory command failed: " .. tostring(result))
        return false
    end

    return result == true
end

local function LevelUp(Ar)
    local player = GetPlayerPawn()

    if not player then
        print("[Remnant2Unlocker] LevelUp: player pawn not found")
        return
    end

    local ok, err = pcall(function()
        player.Traits:OnLevelUp()
    end)

    if ok then
        print("[Remnant2Unlocker] Level up complete")
        if Ar then Ar:Log("Level up complete") end
    else
        print("[Remnant2Unlocker] LevelUp failed: " .. tostring(err))
    end
end

local function SetAllWeaponLevel(level, Ar)
    RunFnInPlayerInv(function(config)
        if config.isweapon then
            config.instanceData.Level = level

            local message = "Weapon level set to: " .. tostring(level)
            print("[Remnant2Unlocker] " .. message)
            if Ar then Ar:Log(message) end
        end
    end)
end

local function SetInventoryItemQuantity(itemName, quantity, Ar)
    local nameToFind = knownItemMappings[itemName] or itemName
    local found = false

    RunFnInPlayerInv(function(config)
        if config.itemBP:GetFullName():find(nameToFind) then
            local currentAmount = config.instanceData.Quantity
            config.instanceData.Quantity = quantity
            found = true

            local message = itemName .. " set to: " .. tostring(quantity) .. " from " .. tostring(currentAmount)
            print("[Remnant2Unlocker] " .. message)
            if Ar then Ar:Log(message) end

            return true
        end
    end)

    if not found then
        local message = itemName .. " not found in inventory"
        print("[Remnant2Unlocker] " .. message)
        if Ar then Ar:Log(message) end
    end
end

local function LogInventoryItems(logAllItems)
    RunFnInPlayerInv(function(config)
        if not logAllItems then
            if config.ismaterial or config.isconsumable then
                print("[Remnant2Unlocker] Item found: " .. tostring(config.itemBP:GetFName():ToString()))
                print("[Remnant2Unlocker] Instance Data: " .. tostring(config.instanceData:GetFullName()))
            end
        else
            print("[Remnant2Unlocker] Item found: " .. tostring(config.itemBP:GetFName():ToString()))
            print("[Remnant2Unlocker] Instance Data: " .. tostring(config.instanceData:GetFullName()))
        end
    end)
end

-- Writes materials/consumables (same filter as log_inventory_items false) to inventory_items.json
-- so the app can show a clickable list instead of the user having to guess the exact blueprint
-- name/formatting for Set Inventory Item Quantity (confirmed painful in testing: "Root Ganglia"
-- and "Root_Ganglia" both failed, only the exact "RootGanglia" substring matched). Quantity is
-- read defensively per item -- equipment-type instance data (Dragon Heart, Liquid Escape) may not
-- expose it the same way materials do, so a missing/unreadable Quantity just falls back to 0
-- rather than aborting the whole scan.
--
-- The same pass also writes owned_items.json: the class name of every item in the inventory
-- (e.g. "Weapon_AlphaOmega_C"), which the app matches against items.json to show what the player
-- is still missing. scannedAt is Unix seconds so the app can tell a live scan from a stale one.
local function ScanInventoryItems()
    local entries = {}
    local owned = {}
    local seen = {}

    local scanned = RunFnInPlayerInv(function(config)
        local okName, name = pcall(function() return config.itemBP:GetFName():ToString() end)

        if not okName or not name then
            return
        end

        name = tostring(name)

        if not seen[name] then
            seen[name] = true
            table.insert(owned, name)
        end

        if config.ismaterial or config.isconsumable then
            local okQty, quantity = pcall(function() return config.instanceData.Quantity end)

            table.insert(entries, {
                name = name,
                quantity = (okQty and tonumber(quantity)) or 0,
            })
        end
    end)

    -- Keep the last good snapshot when the read fails: an empty owned list would make the app
    -- report the player's whole collection as missing.
    if not scanned then
        return false
    end

    local okEntries, encodedEntries = pcall(function() return json.encode(entries) end)

    if okEntries then
        WriteAllText(INVENTORY_ITEMS_PATH, encodedEntries)
    else
        print("[Remnant2Unlocker] Could not encode inventory_items.json: " .. tostring(encodedEntries))
    end

    local okOwned, encodedOwned = pcall(function()
        return json.encode({ scannedAt = math.floor(os.time()), items = owned })
    end)

    if okOwned then
        WriteAllText(OWNED_ITEMS_PATH, encodedOwned)
    else
        print("[Remnant2Unlocker] Could not encode owned_items.json: " .. tostring(encodedOwned))
    end

    return true
end

function InventoryCheats.Start()
    RegisterConsoleCommandHandler("levelup", function(FullCommand, Parameters, Ar)
        local levels = tonumber(Parameters[1]) or 1

        for _ = 1, levels do
            LevelUp(Ar)
        end

        return true
    end)

    RegisterConsoleCommandHandler("set_all_weapon_level", function(FullCommand, Parameters, Ar)
        local level = tonumber(Parameters[1])

        if not level then
            print("[Remnant2Unlocker] set_all_weapon_level: invalid level")
            return false
        end

        SetAllWeaponLevel(level, Ar)
        return true
    end)

    RegisterConsoleCommandHandler("set_inventory_item_quantity", function(FullCommand, Parameters, Ar)
        local itemName = Parameters[1]
        local quantity = tonumber(Parameters[2])

        if not itemName then
            print("[Remnant2Unlocker] set_inventory_item_quantity: invalid item name")
            return false
        end

        if not quantity then
            print("[Remnant2Unlocker] set_inventory_item_quantity: invalid quantity")
            return false
        end

        SetInventoryItemQuantity(itemName, quantity, Ar)
        ScanInventoryItems()
        return true
    end)

    RegisterConsoleCommandHandler("log_inventory_items", function(FullCommand, Parameters, Ar)
        local logAllItems = Parameters[1] == "true"

        print("[Remnant2Unlocker] Logging inventory items")
        if Ar then Ar:Log("Logging inventory items") end

        LogInventoryItems(logAllItems)
        ScanInventoryItems()
        return true
    end)

    -- Immediate rescan for the app's "Scan Inventory" button, instead of waiting for the loop below.
    RegisterConsoleCommandHandler("scan_inventory", function(FullCommand, Parameters, Ar)
        local message = ScanInventoryItems()
            and "Inventory scanned"
            or "Inventory scan failed: player inventory not readable (load into a character first)"

        print("[Remnant2Unlocker] " .. message)
        if Ar then Ar:Log(message) end
        return true
    end)

    -- Auto-scan for the app's item picker (see ScanInventoryItems above). Gated on
    -- IsRealPlayerCharacter so it stays idle on the loading screen/main menu, then refreshes
    -- every 5s -- cheap since it only iterates the player's own inventory, not the whole world.
    LoopAsync(5000, function()
        local player = GetPlayerPawn()

        if IsRealPlayerCharacter(player) then
            ScanInventoryItems()
        end
    end)

    print("[Remnant2Unlocker] Inventory cheats initialized (levelup, set_all_weapon_level, set_inventory_item_quantity, log_inventory_items, scan_inventory)")
end

return InventoryCheats
