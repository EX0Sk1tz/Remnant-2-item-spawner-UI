local json = require("json")
local UEHelpers = require("UEHelpers")

local LootModule = {}

local CHEAT_SETTINGS_FILE = require("paths").File("cheats.json")
local BASELINE_FILE = require("paths").File("loot_originals.txt")
local GUARANTEED_DROP_CHANCE = 100

local guaranteedDropsActive = false
local wasGuaranteedDropsActive = false
local loggedOnce = {}

local function ReadFile(path)
    local handle = io.open(path, "r")
    if not handle then return nil end

    local text = handle:read("*a")
    handle:close()

    return text
end

local function PrintOnce(key, message)
    if loggedOnce[key] == message then
        return
    end

    loggedOnce[key] = message
    print("[Remnant2Unlocker] " .. message)
end

local function PawnIsUsable(obj)
    if not obj then return false end

    local ok, usable = pcall(function()
        if obj.IsValid then
            return obj:IsValid()
        end

        return true
    end)

    return ok and usable == true
end

local function FindLocalPlayerPawn()
    local ok, pawn = pcall(function()
        local controller = UEHelpers.GetPlayerController()

        if controller and controller.Pawn then
            return controller.Pawn
        end

        return FindFirstOf("RemnantPlayerCharacter")
    end)

    if ok and PawnIsUsable(pawn) then
        return pawn
    end

    return nil
end

local function IsPlayerCharacterClass(pawn)
    if not PawnIsUsable(pawn) then
        return false
    end

    local ok, className = pcall(function() return pawn:GetClass():GetFName():ToString() end)

    return ok and className ~= nil and tostring(className):find("Character_.-Player") ~= nil
end

local function SyncSettingsFromDisk()
    local text = ReadFile(CHEAT_SETTINGS_FILE)
    if not text then return end

    local ok, settings = pcall(function()
        return json.decode(text)
    end)

    if not ok or not settings then return end

    local wantGuaranteedDrops = settings.lootFullDropChance == true

    if wantGuaranteedDrops ~= guaranteedDropsActive then
        guaranteedDropsActive = wantGuaranteedDrops
        print("[Remnant2Unlocker] 100% Loot Drop Chance toggled: " .. tostring(guaranteedDropsActive))
    end
end

-- FindAllOf can still hand back a non-nil wrapper for an object that's already gone; the wrapper
-- itself never lies, only IsValid does, so that's the only check worth trusting here.
local function EntryIsLive(obj)
    if obj == nil then return false end
    local ok, live = pcall(function() return obj:IsValid() end)
    return (ok and live) and true or false
end

local function GetObjectPath(obj)
    local ok, fullName = pcall(function() return obj:GetFullName() end)
    if not ok or type(fullName) ~= "string" then return nil end
    return fullName:match("^%S+%s+(.+)$") or fullName
end

local function TryReadField(obj, field)
    local ok, value = pcall(function() return obj[field] end)
    if ok then return value end
    return nil
end

-- The loot tables stay populated on the live game objects even after a script hot-reload clears
-- out this file's own Lua state, so without a copy on disk a reload would treat an already-boosted
-- Chance as the baseline and restoring could never get back to the real vanilla value. That's the
-- only reason this persists to a file instead of just living in memory.
local CapturedDefaults = {}
local baselineNeedsSave = false

local function LoadBaselineFromDisk()
    local handle = io.open(BASELINE_FILE, "r")
    if not handle then return end

    for line in handle:lines() do
        local path, value = line:match("^(.-)\t(-?%d+%.?%d*)$")
        if path and tonumber(value) then
            CapturedDefaults[path] = tonumber(value)
        end
    end

    handle:close()
end

local function SaveBaselineToDisk()
    if not baselineNeedsSave then return end

    local handle = io.open(BASELINE_FILE, "w")
    if not handle then return end

    for path, value in pairs(CapturedDefaults) do
        handle:write(path, "\t", tostring(value), "\n")
    end

    handle:close()
    baselineNeedsSave = false
end

local function ApplyDropChance(obj, path, restore)
    if CapturedDefaults[path] == nil then
        local value = TryReadField(obj, "Chance")
        if type(value) ~= "number" then return false end
        CapturedDefaults[path] = value
        baselineNeedsSave = true
    end

    local baseline = CapturedDefaults[path]
    local want = restore and baseline or math.max(baseline, GUARANTEED_DROP_CHANCE)

    local current = TryReadField(obj, "Chance")
    if type(current) == "number" and current == want then return true end

    local okWrite = pcall(function() obj.Chance = want end)
    if not okWrite then return false end

    local after = TryReadField(obj, "Chance")
    return type(after) == "number" and after == want
end

local function SyncLootToGame()
    -- Only scans while the toggle is actually on, plus one extra pass right after it flips off so
    -- anything boosted gets restored. Once that pass runs there's nothing left to revisit, so this
    -- stops scanning entirely instead of polling forever while switched off.
    local justChanged = guaranteedDropsActive ~= wasGuaranteedDropsActive
    wasGuaranteedDropsActive = guaranteedDropsActive

    if not guaranteedDropsActive and not justChanged then return end

    ExecuteInGameThread(function()
        local pawn = FindLocalPlayerPawn()
        if not IsPlayerCharacterClass(pawn) then return end

        local ok = pcall(function()
            local entries = FindAllOf("SpawnTableItem")
            if not entries then return end

            for _, entry in pairs(entries) do
                if EntryIsLive(entry) then
                    local path = GetObjectPath(entry)
                    if path and not path:find("Default__", 1, true) then
                        ApplyDropChance(entry, path, not guaranteedDropsActive)
                    end
                end
            end
        end)

        if ok then
            pcall(SaveBaselineToDisk)
        else
            PrintOnce("scan_failed", "Loot: SpawnTableItem scan failed")
        end
    end)
end

function LootModule.Start()
    LoadBaselineFromDisk()
    SyncSettingsFromDisk()

    LoopAsync(1000, function()
        SyncSettingsFromDisk()
    end)

    LoopAsync(2000, function()
        SyncLootToGame()
    end)

    print("[Remnant2Unlocker] Loot system initialized")
end

return LootModule
