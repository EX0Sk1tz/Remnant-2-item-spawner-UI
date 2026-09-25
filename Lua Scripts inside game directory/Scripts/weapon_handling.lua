local json = require("json")

local WeaponHandlingModule = {}

local CHEAT_SETTINGS_FILE = require("paths").File("cheats.json")
local RANGED_WEAPON_PROFILE_CLASS = "/Script/GunfireRuntime.RangedWeaponProfile"

-- A full ForEachUObject walk costs roughly a second on this build (measured against ~450k live
-- objects), so it can only run occasionally -- its only job is picking up weapons acquired later
-- in the same session without a loading screen or a toggle flip. Flipping a group on/off already
-- triggers its own immediate scan/restore (see HandleActivityChange), so this periodic pass is
-- just a rare safety net, not how weapons normally get covered.
local FULL_SCAN_PERIOD_MS = 60000
local REAPPLY_PERIOD_MS = 2000

local PropertyGroups = {
    recoil = { "RecoilVertical", "RecoilHorizontalMin", "RecoilHorizontalMax" },
    spread = { "FiringSpreadIncrement", "FiringSpreadAimMoveMin", "FiringSpreadAimMoveMax",
               "FiringSpreadAimMin", "FiringSpreadAimMax" },
}

local GroupActive = { recoil = false, spread = false }
local wasAnyGroupActive = false
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

local function FactorFor(group)
    return GroupActive[group] and 0 or 1
end

local function IsAnyGroupActive()
    return GroupActive.recoil or GroupActive.spread
end

-- CapturedDefaults[path][property] holds the vanilla value the first time this module sees that
-- property. Every write is computed from that stored value rather than whatever is currently on
-- the object, so toggling a group on and off repeatedly can never compound the result.
local CapturedDefaults = {}

-- Only the string paths of discovered class-default objects are kept, never a direct reference:
-- unloading a level destroys those objects, and a held reference could go on reporting IsValid()
-- true for a slot that's since been reused for something else entirely.
local KnownProfilePaths = {}

local cachedProfileClass = nil

local function GetObjectPath(obj)
    local ok, fullName = pcall(function() return obj:GetFullName() end)
    if not ok or type(fullName) ~= "string" then return nil end
    return fullName:match("^%S+%s+(.+)$") or fullName
end

local function FindProfileClass()
    if cachedProfileClass then return cachedProfileClass end

    local ok, class = pcall(function() return StaticFindObject(RANGED_WEAPON_PROFILE_CLASS) end)
    if ok and class then
        local okValid, valid = pcall(function() return class:IsValid() end)
        if okValid and valid then cachedProfileClass = class end
    end

    return cachedProfileClass
end

local function ApplyGroupsToProfile(obj)
    local okValid, valid = pcall(function() return obj:IsValid() end)
    if not okValid or not valid then return end

    local key = GetObjectPath(obj)
    if not key then return end

    if key:find("Default__", 1, true) then KnownProfilePaths[key] = true end

    local snapshot = CapturedDefaults[key]
    if not snapshot then snapshot = {}; CapturedDefaults[key] = snapshot end

    for group, properties in pairs(PropertyGroups) do
        local factor = FactorFor(group)
        for _, property in ipairs(properties) do
            -- A property belonging to a group that's currently left alone is never read: doing so
            -- would risk capturing some other mod's value and treating it as this game's vanilla one.
            if snapshot[property] == nil and factor ~= 1 then
                local ok, value = pcall(function() return obj:GetPropertyValue(property) end)
                if ok and type(value) == "number" and value == value then snapshot[property] = value end
            end

            -- Writing only happens for a property already captured above, so a factor of 1 just
            -- restores whatever this module previously changed and never touches anything it was
            -- never responsible for in the first place.
            if snapshot[property] ~= nil then
                pcall(function() obj:SetPropertyValue(property, snapshot[property] * factor) end)
            end
        end
    end
end

local function ScanAllProfiles()
    local class = FindProfileClass()
    if not class then
        PrintOnce("class_not_found", "Weapon Handling: RangedWeaponProfile class not found")
        return
    end

    local ok = pcall(function()
        ForEachUObject(function(obj)
            if obj then
                local isMatch, matched = pcall(function() return obj:IsA(class) end)
                if isMatch and matched then ApplyGroupsToProfile(obj) end
            end
        end)
    end)

    if not ok then
        PrintOnce("scan_failed", "Weapon Handling: object scan failed")
    end
end

local function ReapplyKnownProfiles()
    for path in pairs(KnownProfilePaths) do
        local ok, obj = pcall(function() return StaticFindObject(path) end)
        if ok and obj then ApplyGroupsToProfile(obj) end
    end
end

-- Fires the moment a group is switched on or off, instead of leaving the player to wait up to
-- FULL_SCAN_PERIOD_MS to see the change (or its restore) take effect.
--
-- Turning on used to always trigger a full ForEachUObject walk here, every single time -- on this
-- build that's a multi-second freeze, and it paid that cost again on every single toggle even
-- when every profile was already known from an earlier scan this session. Now it only pays for
-- the expensive walk the first time (nothing in KnownProfilePaths yet); every later toggle just
-- reapplies to what's already known, which is near-instant. New weapons encountered after that
-- first scan are still picked up -- just via the periodic FULL_SCAN_PERIOD_MS top-up instead of
-- the toggle itself, so there can be up to that long a delay for a genuinely new weapon type
-- before it's covered, in exchange for every toggle after the first being instant.
local function HandleActivityChange(isActive)
    if not isActive then
        ExecuteInGameThread(ReapplyKnownProfiles)
        return
    end

    if next(KnownProfilePaths) == nil then
        ExecuteInGameThread(ScanAllProfiles)
    else
        ExecuteInGameThread(ReapplyKnownProfiles)
    end
end

local function SyncSettingsFromDisk()
    local text = ReadFile(CHEAT_SETTINGS_FILE)
    if not text then return end

    local ok, settings = pcall(function() return json.decode(text) end)
    if not ok or not settings then return end

    local wantRecoil = settings.noRecoil == true
    local wantSpread = settings.noSpread == true

    if wantRecoil ~= GroupActive.recoil then
        GroupActive.recoil = wantRecoil
        print("[Remnant2Unlocker] No Recoil toggled: " .. tostring(GroupActive.recoil))
    end

    if wantSpread ~= GroupActive.spread then
        GroupActive.spread = wantSpread
        print("[Remnant2Unlocker] No Spread toggled: " .. tostring(GroupActive.spread))
    end

    local isActive = IsAnyGroupActive()
    if isActive ~= wasAnyGroupActive then
        wasAnyGroupActive = isActive
        HandleActivityChange(isActive)
    end
end

function WeaponHandlingModule.Start()
    SyncSettingsFromDisk()
    wasAnyGroupActive = IsAnyGroupActive()

    -- Covers "already enabled in cheats.json when this module starts" (e.g. right after a game
    -- restart), which the edge-triggered check inside SyncSettingsFromDisk alone would never catch
    -- since there's no false-to-true transition to see in that case.
    if wasAnyGroupActive then
        ExecuteInGameThread(ScanAllProfiles)
    end

    LoopAsync(1000, function()
        SyncSettingsFromDisk()
    end)

    -- Cheap: only re-resolves paths already known. Skipped entirely once both groups are off,
    -- since HandleActivityChange already restored everything the instant they were turned off and
    -- there is nothing left here worth revisiting.
    LoopAsync(REAPPLY_PERIOD_MS, function()
        if IsAnyGroupActive() then
            ExecuteInGameThread(ReapplyKnownProfiles)
        end
    end)

    -- Rare top-up scan, see the FULL_SCAN_PERIOD_MS comment above. Also skipped entirely while off.
    LoopAsync(FULL_SCAN_PERIOD_MS, function()
        if IsAnyGroupActive() then
            ExecuteInGameThread(ScanAllProfiles)
        end
    end)

    print("[Remnant2Unlocker] Weapon handling system initialized")
end

return WeaponHandlingModule
