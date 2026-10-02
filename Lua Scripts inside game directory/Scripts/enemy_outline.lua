local json = require("json")
local UEHelpers = require("UEHelpers")

-- Enemy Outlines: the Hunter's Mark red outline, on every living enemy at any range, visible
-- through walls.
--
-- How the Mark does it (found with a probe script, confirmed in-game 2026-09-30): every enemy's
-- CharacterMesh0 already carries CustomDepthStencilValue 254 (the player's is 253); the Mark only
-- switches bRenderCustomDepth on, and the game's post-process draws stencil 254 as the red
-- outline. GameUtil::SetRenderCustomDepthForActor(Actor, bEnabled, Index, bPropagateToInventory)
-- does exactly that for a whole actor (weapons included), so this module calls it on every enemy
-- the Mark didn't reach. Turning it off passes 254 back, so the enemy's stencil stays what the
-- Mark expects.
--
-- Finding the enemies: FindAllOf walks the whole object array, and doing that every tick made the
-- game hitch twice a second. So there is one FindAllOf when the feature turns on, and after that
-- NotifyOnNewObject (which includes subclasses) reports every character as it spawns; the tick
-- only looks at the characters it already knows. If no spawn has ever been reported this session
-- (the notification not working would look the same), it falls back to a rescan every
-- RESCAN_FALLBACK_MS.
--
-- Toggled from the app (cheats.json "enemyOutline", edge-triggered like the other cheats) or with
-- the in-game hotkey (hotkeys.lua -> EnemyOutline.Toggle). The hotkey writes the new value back to
-- cheats.json so the app's switch follows it. It only flips the flag; the next tick does the
-- game-thread work, so nothing here ever queues game-thread work from inside a game-thread callback.
local EnemyOutline = {}

local SETTINGS_PATH = require("paths").File("cheats.json")
local MARK_STENCIL = 254
local TICK_MS = 500
local SETTINGS_RELOAD_MS = 1000
local RESCAN_FALLBACK_MS = 10000

local CHARACTER_CLASS = "/Script/GunfireRuntime.CharacterGunfire"
local GAME_UTIL_PATH = "/Script/GunfireRuntime.Default__GameUtil"
local COMBAT_UTIL_PATH = "/Script/GunfireRuntime.Default__CombatUtil"

local enabled = false
local lastFileValue = nil
local needsScan = false
local spawnNotified = false
local msSinceScan = 0
local known = {}    -- address -> character seen by the scan or a spawn notification
local outlined = {} -- address -> actor we switched on
local gameUtil = nil
local combatUtil = nil

local function ReadAllText(path)
    local file = io.open(path, "r")
    if not file then return nil end

    local content = file:read("*a")
    file:close()

    return content
end

local function WriteAllText(path, content)
    local file = io.open(path, "w")
    if not file then return false end

    file:write(content)
    file:close()

    return true
end

local function IsValidObject(obj)
    if not obj then return false end

    local ok, valid = pcall(function() return obj:IsValid() end)

    return ok and valid == true
end

-- FindAllOf also returns the class default object, and calling an actor UFunction on it crashes
-- straight through pcall (see destroy.lua), so it has to be filtered out first.
local function IsDefaultObject(obj)
    local ok, name = pcall(function() return obj:GetFullName() end)

    return not ok or name == nil or tostring(name):find("Default__", 1, true) ~= nil
end

local function FindStatic(path)
    local ok, obj = pcall(function() return StaticFindObject(path) end)

    if ok and IsValidObject(obj) then return obj end

    return nil
end

local function GetPlayerPawn()
    local ok, pawn = pcall(function()
        local pc = UEHelpers.GetPlayerController()

        if pc and pc.Pawn then return pc.Pawn end

        return nil
    end)

    if ok and IsValidObject(pawn) then return pawn end

    return nil
end

local function AddressOf(obj)
    local ok, address = pcall(function() return obj:GetAddress() end)

    if ok then return address end

    return nil
end

local function Remember(actor)
    if not IsValidObject(actor) or IsDefaultObject(actor) then return end

    local address = AddressOf(actor)

    if address then known[address] = actor end
end

local function ScanAll()
    local ok, characters = pcall(function() return FindAllOf("CharacterGunfire") end)

    if ok and characters then
        for _, actor in pairs(characters) do Remember(actor) end
    end

    needsScan = false
    msSinceScan = 0
end

local function IsOutlineOn(actor)
    local ok, on = pcall(function() return actor.Mesh.bRenderCustomDepth end)

    return ok and on == true
end

local function SetOutline(actor, on)
    return pcall(function() gameUtil:SetRenderCustomDepthForActor(actor, on, MARK_STENCIL, true) end)
end

local function ClearAll()
    local count = 0

    for address, actor in pairs(outlined) do
        if gameUtil and IsValidObject(actor) and SetOutline(actor, false) then
            count = count + 1
        end

        outlined[address] = nil
    end

    return count
end

local function OutlineEnemies()
    local player = GetPlayerPawn()

    if not player then return end

    local playerAddress = AddressOf(player)

    for address, actor in pairs(known) do
        if not IsValidObject(actor) then
            known[address] = nil
            outlined[address] = nil
        elseif address ~= playerAddress then
            local okEnemy, isEnemy = pcall(function() return combatUtil:IsEnemy(player, actor) end)
            local okAlive, isAlive = pcall(function() return actor:IsAlive() end)

            if okEnemy and isEnemy and okAlive and isAlive then
                if not IsOutlineOn(actor) and SetOutline(actor, true) then
                    outlined[address] = actor
                end
            elseif outlined[address] then
                -- Died (or stopped being hostile): drop the outline like the Mark does.
                SetOutline(actor, false)
                outlined[address] = nil
            end
        end
    end
end

-- Runs on the game thread.
local function Tick()
    if not IsValidObject(gameUtil) then gameUtil = FindStatic(GAME_UTIL_PATH) end
    if not IsValidObject(combatUtil) then combatUtil = FindStatic(COMBAT_UTIL_PATH) end

    if enabled then
        if not gameUtil or not combatUtil then return end

        msSinceScan = msSinceScan + TICK_MS

        if needsScan or (not spawnNotified and msSinceScan >= RESCAN_FALLBACK_MS) then ScanAll() end

        OutlineEnemies()
    elseif next(outlined) ~= nil then
        local count = ClearAll()
        print("[Remnant2Unlocker] Enemy Outlines removed from " .. count .. " enemies")
    end
end

local function SetEnabled(value, source)
    if value == enabled then return end

    enabled = value

    if enabled then
        needsScan = true
    else
        known = {}
    end

    print("[Remnant2Unlocker] Enemy Outlines " .. (enabled and "enabled" or "disabled") .. " (" .. source .. ")")
end

-- Rewrites only the enemyOutline value, keeping the rest of the file (and its layout) as the app
-- wrote it.
local function WriteFileValue(value)
    local content = ReadAllText(SETTINGS_PATH) or "{}"
    local text = value and "true" or "false"
    local updated, count = content:gsub('("enemyOutline"%s*:%s*)%a+', "%1" .. text, 1)

    if count == 0 then
        local ok, settings = pcall(function() return json.decode(content) end)

        if not ok or type(settings) ~= "table" then settings = {} end

        settings.enemyOutline = value
        updated = json.encode(settings)
    end

    lastFileValue = value

    if not WriteAllText(SETTINGS_PATH, updated) then
        print("[Remnant2Unlocker] Enemy Outlines: could not write " .. SETTINGS_PATH)
    end
end

local function SyncSettingsFromDisk()
    local content = ReadAllText(SETTINGS_PATH)
    if not content then return end

    local ok, settings = pcall(function() return json.decode(content) end)
    if not ok or type(settings) ~= "table" then return end

    local fileValue = settings.enemyOutline == true

    -- Only a change in the file counts, so the app re-saving its other cheats doesn't matter.
    if fileValue ~= lastFileValue then
        lastFileValue = fileValue
        SetEnabled(fileValue, "app")
    end
end

function EnemyOutline.Toggle()
    SetEnabled(not enabled, "hotkey")
    WriteFileValue(enabled)
end

function EnemyOutline.IsEnabled()
    return enabled
end

function EnemyOutline.Start()
    SyncSettingsFromDisk()

    -- Runs while the object is being constructed: only note it, the tick does the rest.
    local okNotify, notifyErr = pcall(function()
        NotifyOnNewObject(CHARACTER_CLASS, function(actor)
            spawnNotified = true

            if enabled then Remember(actor) end
        end)
    end)

    if not okNotify then
        print("[Remnant2Unlocker] Enemy Outlines: spawn notification unavailable, rescanning instead: " .. tostring(notifyErr))
    end

    LoopAsync(SETTINGS_RELOAD_MS, function()
        SyncSettingsFromDisk()
    end)

    LoopAsync(TICK_MS, function()
        if enabled or next(outlined) ~= nil then
            ExecuteInGameThread(function()
                local ok, err = pcall(Tick)

                if not ok then print("[Remnant2Unlocker] Enemy Outlines tick failed: " .. tostring(err)) end
            end)
        end
    end)

    print("[Remnant2Unlocker] Enemy Outlines initialized")
end

return EnemyOutline
