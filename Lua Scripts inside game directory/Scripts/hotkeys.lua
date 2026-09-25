local json = require("json")
local UEHelpers = require("UEHelpers")
local Destroy = require("destroy")
local CombatActions = require("combat_actions")

local Hotkeys = {}

local SETTINGS_PATH = require("paths").File("hotkeys.json")
local RELOAD_INTERVAL_MS = 1000

local activeTeleportKey = "None"
local activeDestroyTargetKey = "None"
local activeDestroyLastSpawnedKey = "None"
local activeDestroyNearbySpawnedKey = "None"
local activeReplenishCooldownsKey = "None"
local activeFastPlayerActionsKey = "None"

local boundTeleportKeys = {}
local boundDestroyTargetKeys = {}
local boundDestroyLastSpawnedKeys = {}
local boundDestroyNearbySpawnedKeys = {}
local boundReplenishCooldownsKeys = {}
local boundFastPlayerActionsKeys = {}

local function ReadAllText(path)
    local file = io.open(path, "r")
    if not file then return nil end

    local content = file:read("*a")
    file:close()

    return content
end

local function NormalizeKey(key)
    if not key then return "None" end

    local value = tostring(key)
    value = value:gsub("^%s+", ""):gsub("%s+$", "")

    if value == "" then
        return "None"
    end

    return value
end

local function GetKeyEnum(keyName)
    if not keyName or keyName == "None" then
        return nil
    end

    if keyName == "MouseButton4" then
        return Key.XButton1
    end

    if keyName == "MouseButton5" then
        return Key.XButton2
    end

    if keyName == "MiddleMouseButton" then
        return Key.MiddleMouseButton
    end

    return Key[keyName]
end

local function ExecuteConsoleCommand(command)
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local ksl = UEHelpers.GetKismetSystemLibrary(true)
            local context = UEHelpers.GetWorldContextObject()
            local player = UEHelpers.GetPlayerController()

            ksl:ExecuteConsoleCommand(context, command, player)
        end)

        if ok then
            print("[Remnant2Unlocker] Hotkey command executed: " .. command)
        else
            print("[Remnant2Unlocker] Hotkey command failed: " .. tostring(err))
        end
    end)
end

local function Teleport()
    ExecuteConsoleCommand("Teleport")
end

local function DestroyTarget()
    ExecuteConsoleCommand("DestroyTarget")
end

local function DestroyLastSpawned()
    Destroy.DestroyLastSpawned()
end

local function DestroyNearbySpawned()
    Destroy.DestroyNearbySpawned()
end

local function ReplenishCooldowns()
    CombatActions.ReplenishCooldownsAndModPower()
end

local function FastPlayerActions()
    CombatActions.FastPlayerActions()
end

local DEFAULT_SETTINGS = {
    teleport = "None",
    destroyTarget = "None",
    destroyLastSpawned = "None",
    destroyNearbySpawned = "None",
    replenishCooldowns = "None",
    fastPlayerActions = "None"
}

local function LoadSettings()
    local content = ReadAllText(SETTINGS_PATH)

    if not content then
        return DEFAULT_SETTINGS
    end

    local ok, settings = pcall(function()
        return json.decode(content)
    end)

    if not ok or not settings then
        return DEFAULT_SETTINGS
    end

    return settings
end

-- UE4SS's Lua API has no documented "unregister keybind" call, so rebinding a hotkey at
-- runtime can't remove the previous binding. Instead, every registered key's callback
-- checks whether it is still the *currently active* key for its action before doing
-- anything; once the player picks a different key, the old binding becomes a permanent
-- no-op instead of double-firing alongside the new one. This has not been verified
-- in-game against the live UE4SS API -- watch for stacked/duplicate triggers if a key is
-- rebound repeatedly in one session, and fall back to a game/mod restart if so.

local function RegisterTeleportKey(keyName)
    if boundTeleportKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] Teleport hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeTeleportKey == keyName then
            Teleport()
        end
    end)

    boundTeleportKeys[keyName] = true

    print("[Remnant2Unlocker] Teleport hotkey registered: " .. keyName)
end

local function RegisterDestroyTargetKey(keyName)
    if boundDestroyTargetKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] DestroyTarget hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeDestroyTargetKey == keyName then
            DestroyTarget()
        end
    end)

    boundDestroyTargetKeys[keyName] = true

    print("[Remnant2Unlocker] DestroyTarget hotkey registered: " .. keyName)
end

local function RegisterDestroyLastSpawnedKey(keyName)
    if boundDestroyLastSpawnedKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] DestroyLastSpawned hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeDestroyLastSpawnedKey == keyName then
            DestroyLastSpawned()
        end
    end)

    boundDestroyLastSpawnedKeys[keyName] = true

    print("[Remnant2Unlocker] DestroyLastSpawned hotkey registered: " .. keyName)
end

local function RegisterDestroyNearbySpawnedKey(keyName)
    if boundDestroyNearbySpawnedKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] DestroyNearbySpawned hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeDestroyNearbySpawnedKey == keyName then
            DestroyNearbySpawned()
        end
    end)

    boundDestroyNearbySpawnedKeys[keyName] = true

    print("[Remnant2Unlocker] DestroyNearbySpawned hotkey registered: " .. keyName)
end

local function RegisterReplenishCooldownsKey(keyName)
    if boundReplenishCooldownsKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] ReplenishCooldowns hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeReplenishCooldownsKey == keyName then
            ReplenishCooldowns()
        end
    end)

    boundReplenishCooldownsKeys[keyName] = true

    print("[Remnant2Unlocker] ReplenishCooldowns hotkey registered: " .. keyName)
end

local function RegisterFastPlayerActionsKey(keyName)
    if boundFastPlayerActionsKeys[keyName] then
        return
    end

    local keyEnum = GetKeyEnum(keyName)

    if not keyEnum then
        print("[Remnant2Unlocker] FastPlayerActions hotkey not bound: " .. tostring(keyName))
        return
    end

    RegisterKeyBind(keyEnum, function()
        if activeFastPlayerActionsKey == keyName then
            FastPlayerActions()
        end
    end)

    boundFastPlayerActionsKeys[keyName] = true

    print("[Remnant2Unlocker] FastPlayerActions hotkey registered: " .. keyName)
end

local function ApplySettings(settings)
    local teleportKey = NormalizeKey(settings.teleport)
    local destroyTargetKey = NormalizeKey(settings.destroyTarget)
    local destroyLastSpawnedKey = NormalizeKey(settings.destroyLastSpawned)
    local destroyNearbySpawnedKey = NormalizeKey(settings.destroyNearbySpawned)
    local replenishCooldownsKey = NormalizeKey(settings.replenishCooldowns)
    local fastPlayerActionsKey = NormalizeKey(settings.fastPlayerActions)

    if teleportKey ~= activeTeleportKey then
        activeTeleportKey = teleportKey
        RegisterTeleportKey(teleportKey)
        print("[Remnant2Unlocker] Teleport hotkey updated: " .. teleportKey)
    end

    if destroyTargetKey ~= activeDestroyTargetKey then
        activeDestroyTargetKey = destroyTargetKey
        RegisterDestroyTargetKey(destroyTargetKey)
        print("[Remnant2Unlocker] DestroyTarget hotkey updated: " .. destroyTargetKey)
    end

    if destroyLastSpawnedKey ~= activeDestroyLastSpawnedKey then
        activeDestroyLastSpawnedKey = destroyLastSpawnedKey
        RegisterDestroyLastSpawnedKey(destroyLastSpawnedKey)
        print("[Remnant2Unlocker] DestroyLastSpawned hotkey updated: " .. destroyLastSpawnedKey)
    end

    if destroyNearbySpawnedKey ~= activeDestroyNearbySpawnedKey then
        activeDestroyNearbySpawnedKey = destroyNearbySpawnedKey
        RegisterDestroyNearbySpawnedKey(destroyNearbySpawnedKey)
        print("[Remnant2Unlocker] DestroyNearbySpawned hotkey updated: " .. destroyNearbySpawnedKey)
    end

    if replenishCooldownsKey ~= activeReplenishCooldownsKey then
        activeReplenishCooldownsKey = replenishCooldownsKey
        RegisterReplenishCooldownsKey(replenishCooldownsKey)
        print("[Remnant2Unlocker] ReplenishCooldowns hotkey updated: " .. replenishCooldownsKey)
    end

    if fastPlayerActionsKey ~= activeFastPlayerActionsKey then
        activeFastPlayerActionsKey = fastPlayerActionsKey
        RegisterFastPlayerActionsKey(fastPlayerActionsKey)
        print("[Remnant2Unlocker] FastPlayerActions hotkey updated: " .. fastPlayerActionsKey)
    end
end

function Hotkeys.Start()
    ApplySettings(LoadSettings())

    LoopAsync(RELOAD_INTERVAL_MS, function()
        ApplySettings(LoadSettings())
    end)

    print("[Remnant2Unlocker] Hotkey system initialized")
end

return Hotkeys
