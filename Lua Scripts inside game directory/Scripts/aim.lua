local json = require("json")
local UEHelpers = require("UEHelpers")

local AimModule = {}

local CHEAT_SETTINGS_FILE = require("paths").File("cheats.json")
local GUNFIRE_SETTINGS_OBJECT_PATH = "/Script/GunfireRuntime.Default__GunfireSettings"

-- Magic Bullets only ever asks for one strength value; the cone angle is the only part the
-- player actually configures. Vanilla ships at 0.5, so this is what "on" switches to.
local MAGIC_BULLETS_STRENGTH = 1.0
local DEFAULT_ASSIST_CONE = 5.0
local MIN_ASSIST_CONE, MAX_ASSIST_CONE = 0.0, 45.0

local magicBulletsActive = false
local wasMagicBulletsActive = false
local assistConeDegrees = DEFAULT_ASSIST_CONE

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

local function ObjectIsAlive(obj)
    if not obj then return false end

    local ok, alive = pcall(function()
        if obj.IsValid then
            return obj:IsValid()
        end

        return true
    end)

    return ok and alive == true
end

local function FindLocalPlayerPawn()
    local ok, pawn = pcall(function()
        local controller = UEHelpers.GetPlayerController()

        if controller and controller.Pawn then
            return controller.Pawn
        end

        return FindFirstOf("RemnantPlayerCharacter")
    end)

    if ok and ObjectIsAlive(pawn) then
        return pawn
    end

    return nil
end

local function IsPlayerCharacterClass(pawn)
    if not ObjectIsAlive(pawn) then
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

    local wantMagicBullets = settings.aimMagicBulletsEnabled == true

    local wantCone = tonumber(settings.aimFov) or DEFAULT_ASSIST_CONE
    if wantCone < MIN_ASSIST_CONE then wantCone = MIN_ASSIST_CONE end
    if wantCone > MAX_ASSIST_CONE then wantCone = MAX_ASSIST_CONE end

    if wantMagicBullets ~= magicBulletsActive then
        magicBulletsActive = wantMagicBullets
        print("[Remnant2Unlocker] Magic Bullets toggled: " .. tostring(magicBulletsActive))
    end

    if wantCone ~= assistConeDegrees then
        assistConeDegrees = wantCone
        print("[Remnant2Unlocker] Aim FOV updated: " .. tostring(assistConeDegrees))
    end
end

local function GetObjectPath(obj)
    local ok, fullName = pcall(function() return obj:GetFullName() end)
    if not ok or type(fullName) ~= "string" then return nil end
    return fullName:match("^%S+%s+(.+)$") or fullName
end

local function FindGunfireSettings()
    local ok, settings = pcall(function() return StaticFindObject(GUNFIRE_SETTINGS_OBJECT_PATH) end)
    if ok and settings then return settings end
    return nil
end

-- The game rebuilds this component alongside the character every time a level loads, so it has
-- to be looked up again on every apply pass instead of cached once.
local function FindPlayerTargetingComponent()
    local match

    pcall(function()
        local components = FindAllOf("TargetingComponent")
        if not components then return end

        for _, component in pairs(components) do
            local path = GetObjectPath(component)
            if path and not path:find("Default__", 1, true) and path:find("Player", 1, true) then
                match = component
                return
            end
        end
    end)

    return match
end

-- CapturedDefaults[objectKey][field] holds whatever value this module saw before it first wrote
-- to that field. Writes are always computed from that snapshot rather than the field's current
-- value, and every write is read back afterward to confirm it actually landed.
local CapturedDefaults = {}

local function ApplyNumericField(obj, key, field, targetValue, restore)
    local snapshot = CapturedDefaults[key]
    if not snapshot then snapshot = {}; CapturedDefaults[key] = snapshot end

    if snapshot[field] == nil then
        local ok, current = pcall(function() return obj[field] end)
        if ok and type(current) == "number" then snapshot[field] = current else return false end
    end

    local want = restore and snapshot[field] or targetValue
    if type(want) ~= "number" then return false end

    local okCurrent, current = pcall(function() return obj[field] end)
    if okCurrent and type(current) == "number" and math.abs(current - want) < 0.0001 then
        return true
    end

    local okWrite = pcall(function() obj[field] = want end)
    if not okWrite then return false end

    local okRead, written = pcall(function() return obj[field] end)
    return okRead and type(written) == "number" and math.abs(written - want) < 0.0001
end

local function SyncAimToGame()
    -- Only worth touching the game objects while Magic Bullets is on, plus one extra pass right
    -- after it's switched off so the vanilla values get put back. After that pass there is
    -- nothing left to do, so this backs off entirely instead of polling GunfireSettings forever.
    local justChanged = magicBulletsActive ~= wasMagicBulletsActive
    wasMagicBulletsActive = magicBulletsActive

    if not magicBulletsActive and not justChanged then return end

    ExecuteInGameThread(function()
        local gunfireSettings = FindGunfireSettings()

        if gunfireSettings then
            ApplyNumericField(gunfireSettings, "GunfireSettings", "KeyboardAndMouseAimAdjustScale",
                MAGIC_BULLETS_STRENGTH, not magicBulletsActive)
        else
            PrintOnce("no_gunfire_settings", "Aim: GunfireSettings not found")
        end

        local pawn = FindLocalPlayerPawn()
        if not IsPlayerCharacterClass(pawn) then return end

        local targeting = FindPlayerTargetingComponent()
        if not targeting then
            PrintOnce("no_targeting_component", "Aim: no player TargetingComponent yet -- cone not set")
            return
        end

        local key = GetObjectPath(targeting) or "PlayerTargeting"
        ApplyNumericField(targeting, key, "AimAdjustAngle", assistConeDegrees, not magicBulletsActive)
    end)
end

function AimModule.Start()
    SyncSettingsFromDisk()

    LoopAsync(1000, function()
        SyncSettingsFromDisk()
    end)

    LoopAsync(2000, function()
        SyncAimToGame()
    end)

    print("[Remnant2Unlocker] Aim system initialized")
end

return AimModule
