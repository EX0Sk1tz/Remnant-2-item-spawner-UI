local json = require("json")
local UEHelpers = require("UEHelpers")

local Cheats = {}

local SETTINGS_PATH = require("paths").File("cheats.json")

local infiniteHealth = false
local infiniteStamina = false
local infiniteAmmo = false
local noFallDamage = false

local AMMO_PROPERTIES = { "HandGunAmmo", "LongGunAmmo", "SpecialAmmo", "HeavyWeaponAmmo" }

local HEALTH_CANDIDATES = { "Health", "CurrentHealth" }
local SUB_VALUE_CANDIDATES = { "CurrentValue", "BaseValue", "Value" }

local FALL_DAMAGE_TRUE_CANDIDATES = { "bIgnoreFallDamage", "IgnoreFallDamage" }
local FALL_DAMAGE_FALSE_CANDIDATES = { "bFallingDamageEnabled" }
local MOVEMENT_CANDIDATES = { "CharacterMovement", "MovementComponent", "LocomotionComponent" }

local MODIFY_INCOMING_DAMAGE_HOOK_NAME =
    "/Game/Characters/Player/Base/Character_Master_Player_Base.Character_Master_Player_Base_C:ModifyIncomingDamage"

local lastLogged = {}
local resourcePeaks = {}
local wasFalling = false
local airborneHealth = nil

local function ReadAllText(path)
    local file = io.open(path, "r")
    if not file then return nil end

    local content = file:read("*a")
    file:close()

    return content
end

local function LogOnce(key, message)
    if lastLogged[key] == message then
        return
    end

    lastLogged[key] = message
    print("[Remnant2Unlocker] " .. message)
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

-- GetPlayerPawn() can resolve to a menu/character-preview pawn while still on the loading
-- screen or main menu (confirmed in testing: "component not found on pawn" for Stamina/Ammo
-- fired against something that isn't the real gameplay character). The real player character
-- Blueprint classes are named Character_Player_Female_C/Character_Player_Male_C (both derive
-- from Character_Master_Player_Base_C) -- requiring that substring is a much stronger "we're
-- actually in a level" signal than merely "GetPlayerPawn() returned something".
-- In practice the live class name can also be "Character_Master_Player_C" directly (confirmed
-- via UE4SS logs), which the old plain substring check missed entirely -- use a Lua pattern
-- ("Character_" + anything + "Player") so all observed namings match.
local function IsRealPlayerCharacter(player)
    if not IsValidObject(player) then
        return false
    end

    local ok, className = pcall(function() return player:GetClass():GetFName():ToString() end)

    return ok and className ~= nil and tostring(className):find("Character_.-Player") ~= nil
end

local function GetMovementComponent(player)
    if not IsValidObject(player) then
        return nil
    end

    for _, propertyName in ipairs(MOVEMENT_CANDIDATES) do
        local ok, component = pcall(function()
            return player[propertyName]
        end)

        if ok and IsValidObject(component) then
            return component
        end
    end

    return nil
end

local function IsNumber(value)
    return type(value) == "number"
end

-- Tries each candidate raw property name in turn. For booleans/flags (fall-damage) the raw
-- value is what we want. Never used for stats directly -- see TryReadNumericFirst below.
local function TryWriteFirstRaw(obj, candidates, value)
    if not obj then return false, nil end

    for _, name in ipairs(candidates) do
        local ok = pcall(function() obj[name] = value end)

        if ok then
            return true, name
        end
    end

    return false, nil
end

-- Still used for Health only (unconfirmed real field name) -- reads each candidate property;
-- if the raw value isn't a number, probes common sub-fields once before giving up. Only ever
-- returns a real Lua number (or nil).
local function TryReadNumericFirst(obj, candidates)
    if not obj then return nil, nil end

    for _, name in ipairs(candidates) do
        local ok, rawValue = pcall(function() return obj[name] end)

        if ok and rawValue ~= nil then
            if IsNumber(rawValue) then
                return rawValue, name
            end

            for _, subName in ipairs(SUB_VALUE_CANDIDATES) do
                local okSub, subValue = pcall(function() return rawValue[subName] end)

                if okSub and IsNumber(subValue) then
                    return subValue, name .. "." .. subName
                end
            end
        end
    end

    return nil, nil
end

local function TryWriteNumericFirst(obj, candidates, value)
    if not obj then return false, nil end

    for _, name in ipairs(candidates) do
        local okRead, rawValue = pcall(function() return obj[name] end)

        if okRead and rawValue ~= nil then
            if IsNumber(rawValue) then
                local okWrite = pcall(function() obj[name] = value end)
                if okWrite then return true, name end
            else
                for _, subName in ipairs(SUB_VALUE_CANDIDATES) do
                    local okSubRead, subValue = pcall(function() return rawValue[subName] end)

                    if okSubRead and IsNumber(subValue) then
                        local okSubWrite = pcall(function() rawValue[subName] = value end)
                        if okSubWrite then return true, name .. "." .. subName end
                    end
                end
            end
        end
    end

    return false, nil
end

local function IsFallingMode(movementMode)
    if movementMode == nil then return false end

    local okString, asString = pcall(tostring, movementMode)

    if okString and asString and asString:lower():find("falling") then
        return true
    end

    -- Unreal's EMovementMode enum has MOVE_Falling = 3; fall back to the raw numeric value
    -- if the Lua binding doesn't stringify the enum name.
    return tonumber(movementMode) == 3
end

local function ReloadSettings()
    local content = ReadAllText(SETTINGS_PATH)
    if not content then return end

    local ok, settings = pcall(function()
        return json.decode(content)
    end)

    if not ok or not settings then return end

    local newHealth = settings.infiniteHealth == true
    local newStamina = settings.infiniteStamina == true
    local newAmmo = settings.infiniteAmmo == true
    local newFallDamage = settings.noFallDamage == true

    if newHealth ~= infiniteHealth then
        infiniteHealth = newHealth
        print("[Remnant2Unlocker] Infinite Health toggled: " .. tostring(infiniteHealth))
    end

    if newStamina ~= infiniteStamina then
        infiniteStamina = newStamina
        print("[Remnant2Unlocker] Infinite Stamina toggled: " .. tostring(infiniteStamina))
    end

    if newAmmo ~= infiniteAmmo then
        infiniteAmmo = newAmmo
        print("[Remnant2Unlocker] Infinite Ammo toggled: " .. tostring(infiniteAmmo))
    end

    if newFallDamage ~= noFallDamage then
        noFallDamage = newFallDamage
        print("[Remnant2Unlocker] No Fall Damage toggled: " .. tostring(noFallDamage))
    end
end

local godModeHookRegistered = false
local godModeHookAttempts = 0
local GOD_MODE_MAX_ATTEMPTS = 5

-- Registered unconditionally, regardless of the current infiniteHealth value -- the hook
-- callback itself checks the up-to-date flag every incoming hit, so toggling in the app takes
-- effect immediately with no re-registration needed.
--
-- The target Blueprint function (Character_Master_Player_Base_C:ModifyIncomingDamage) is not
-- loaded into memory yet this early in startup. Confirmed via UE4SS.log: RegisterHook against an
-- unloaded Blueprint UFunction does NOT throw a normal Lua error here -- it aborts the entire
-- main.lua chunk outright, the same "pcall cannot catch this" native-level failure destroy.lua
-- documents for CDO UFunction calls. Wrapping in pcall does not help against that. The mitigation
-- is layered: (1) never attempt until IsRealPlayerCharacter confirms we're actually in a level,
-- not the main menu/character preview (confirmed in testing: a menu-only pawn passed the old
-- "any pawn" check and burned the only attempt), (2) only ever try a bounded number of times
-- (not forever -- a call that can hard-crash the mod should not be retried indefinitely), and
-- (3) this only ever runs from inside the 1s LoopAsync tick (never from Start() directly), so a
-- hard failure here happens well after every other module has already started and can't take
-- them down with it.
local function EnsureGodModeHook()
    if godModeHookRegistered or godModeHookAttempts >= GOD_MODE_MAX_ATTEMPTS then
        return
    end

    local player = GetPlayerPawn()
    if not IsRealPlayerCharacter(player) then
        return
    end

    godModeHookAttempts = godModeHookAttempts + 1

    local ok, err = pcall(function()
        RegisterHook(MODIFY_INCOMING_DAMAGE_HOOK_NAME, function(self, DamageInfo)
            if infiniteHealth then
                local damage = DamageInfo:get()
                damage.Damage = 0
            end
        end)
    end)

    if ok then
        godModeHookRegistered = true
        print("[Remnant2Unlocker] Infinite Health hook registered (ModifyIncomingDamage)")
    elseif godModeHookAttempts >= GOD_MODE_MAX_ATTEMPTS then
        print("[Remnant2Unlocker] Infinite Health hook registration failed after " .. tostring(godModeHookAttempts) .. " attempts, giving up for this session: " .. tostring(err))
    else
        print("[Remnant2Unlocker] Infinite Health hook registration failed (attempt " .. tostring(godModeHookAttempts) .. "/" .. tostring(GOD_MODE_MAX_ATTEMPTS) .. "), will retry: " .. tostring(err))
    end
end

-- Generic "keep this VitalityComponent's Value at its own observed peak" -- works for Stamina
-- and every ammo pool, since they're all the same component/field.
local function ApplyInfiniteResource(player, propertyName, enabled)
    if not enabled then
        resourcePeaks[propertyName] = nil
        return
    end

    local okComponent, component = pcall(function() return player[propertyName] end)

    if not okComponent or not IsValidObject(component) then
        LogOnce(propertyName .. "_missing", "Infinite " .. propertyName .. ": component not found on pawn")
        return
    end

    local okRead, current = pcall(function() return component.Value end)

    if not okRead or not IsNumber(current) then
        LogOnce(propertyName .. "_value_missing", "Infinite " .. propertyName .. ": Value property not numeric")
        return
    end

    local peak = resourcePeaks[propertyName]

    if not peak or current > peak then
        peak = current
        resourcePeaks[propertyName] = peak
    end

    if current < peak then
        local okWrite = pcall(function() component.Value = peak end)

        if okWrite then
            LogOnce(propertyName .. "_restored", "Infinite " .. propertyName .. ": restored to " .. tostring(peak))
        else
            LogOnce(propertyName .. "_write_failed", "Infinite " .. propertyName .. ": write failed")
        end
    end
end

local function ApplyInfiniteStamina()
    if not infiniteStamina then return end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()
        if not player then return end

        ApplyInfiniteResource(player, "Stamina", infiniteStamina)
    end)
end

local function ApplyInfiniteAmmo()
    if not infiniteAmmo then return end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()
        if not player then return end

        for _, propertyName in ipairs(AMMO_PROPERTIES) do
            ApplyInfiniteResource(player, propertyName, infiniteAmmo)
        end
    end)
end

-- Tier 1: try a direct ignore-fall-damage flag. Tier 2 (always runs alongside, cheap insurance):
-- detect a falling->grounded transition via CharacterMovement and restore health lost on landing.
local function ApplyNoFallDamage()
    if not noFallDamage then
        wasFalling = false
        airborneHealth = nil
        return
    end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()
        if not player then return end

        local flagOkTrue = TryWriteFirstRaw(player, FALL_DAMAGE_TRUE_CANDIDATES, true)
        local flagOkFalse = TryWriteFirstRaw(player, FALL_DAMAGE_FALSE_CANDIDATES, false)

        if flagOkTrue or flagOkFalse then
            LogOnce("fall_flag_applied", "No Fall Damage: direct flag applied")
        end

        local movement = GetMovementComponent(player)

        if not movement then
            LogOnce("fall_movement_missing", "No Fall Damage: movement component not found for landing-detection fallback")
            return
        end

        local okMode, movementMode = pcall(function() return movement.MovementMode end)
        local isFalling = okMode and IsFallingMode(movementMode)

        if isFalling then
            local currentHealth = TryReadNumericFirst(player, HEALTH_CANDIDATES)

            if currentHealth then
                airborneHealth = currentHealth
            end

            wasFalling = true
        elseif wasFalling then
            if airborneHealth then
                local currentHealth = TryReadNumericFirst(player, HEALTH_CANDIDATES)

                if currentHealth and currentHealth < airborneHealth then
                    TryWriteNumericFirst(player, HEALTH_CANDIDATES, airborneHealth)
                    LogOnce("fall_health_restored", "No Fall Damage: restored health after landing (" .. tostring(airborneHealth) .. ")")
                end
            end

            wasFalling = false
            airborneHealth = nil
        end
    end)
end

function Cheats.Start()
    ReloadSettings()

    -- EnsureGodModeHook is intentionally NOT called here (synchronously) -- see its own comment.
    -- It only ever runs from inside this 1s loop, after Start() has already fully returned, so a
    -- hard failure there can no longer take the rest of this module's LoopAsync registrations
    -- (or any later SafeStart'd module in main.lua) down with it.
    LoopAsync(1000, function()
        ReloadSettings()
        EnsureGodModeHook()
    end)

    LoopAsync(250, function()
        ApplyInfiniteStamina()
        ApplyInfiniteAmmo()
    end)

    LoopAsync(100, function()
        ApplyNoFallDamage()
    end)

    print("[Remnant2Unlocker] Cheats system initialized")
end

return Cheats
