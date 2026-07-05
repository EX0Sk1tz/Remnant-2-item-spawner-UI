local json = require("json")
local UEHelpers = require("UEHelpers")

local Hotkeys = {}

local SETTINGS_PATH = "Mods/Remnant2Unlocker/hotkeys.json"
local RELOAD_INTERVAL_MS = 1000

local activeTeleportKey = "None"
local activeDestroyTargetKey = "None"

local boundTeleportKeys = {}
local boundDestroyTargetKeys = {}

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

local function LoadSettings()
    local content = ReadAllText(SETTINGS_PATH)

    if not content then
        return {
            teleport = "F6",
            destroyTarget = "None"
        }
    end

    local ok, settings = pcall(function()
        return json.decode(content)
    end)

    if not ok or not settings then
        return {
            teleport = "F6",
            destroyTarget = "None"
        }
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

local function ApplySettings(settings)
    local teleportKey = NormalizeKey(settings.teleport)
    local destroyTargetKey = NormalizeKey(settings.destroyTarget)

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
end

function Hotkeys.Start()
    ApplySettings(LoadSettings())

    LoopAsync(RELOAD_INTERVAL_MS, function()
        ApplySettings(LoadSettings())
    end)

    print("[Remnant2Unlocker] Hotkey system initialized")
end

return Hotkeys
