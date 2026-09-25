local UEHelpers = require("UEHelpers")

local DebugDump = {}

-- Broad net of plausible names for how this game might expose Health/Stamina, since the
-- direct "Health"/"MaxHealth" names on the pawn turned out to be struct/object references
-- whose real sub-field we haven't found yet. Press F9 in-game to dump what each of these
-- actually resolves to (type + value, plus common sub-fields) into UE4SS.log.
local CANDIDATE_NAMES = {
    "Health", "CurrentHealth", "MaxHealth", "MaximumHealth",
    "Stamina", "CurrentStamina", "MaxStamina", "MaximumStamina",
    "HealthComponent", "StaminaComponent", "StatusComponent", "ResourceComponent",
    "AttributeSet", "AttributeSetBase", "HealthAttributeSet", "CharacterAttributeSet",
    "Attributes", "AbilitySystemComponent", "ASC",
}

local SUB_FIELD_NAMES = { "CurrentValue", "BaseValue", "Value", "Health", "MaxHealth", "Stamina", "MaxStamina" }

-- Confirmed via Live View: player.StatsComp is a real StatsComponent exposing a name-based
-- GetStat(name)/HasStat(name) API rather than plain properties. "Stamina"/"MaxStamina" are
-- included here purely as a cross-check -- if GetStat("Stamina") matches the value we already
-- get from VitalityComponent.Value, that confirms this API path is trustworthy for Health too.
local STAT_NAME_CANDIDATES = {
    "Health", "MaxHealth", "CurrentHealth",
    "Stamina", "MaxStamina",
    "Vitality", "HP", "PlayerHealth", "Life", "MaxLife",
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

local function DescribeValue(value)
    local t = type(value)

    if t == "number" or t == "boolean" or t == "string" then
        return t .. ": " .. tostring(value)
    end

    if t == "nil" then
        return "nil"
    end

    -- table/userdata (likely a UObject or struct wrapper) -- probe common sub-fields
    local subInfo = {}

    for _, subName in ipairs(SUB_FIELD_NAMES) do
        local ok, subValue = pcall(function() return value[subName] end)

        if ok and subValue ~= nil then
            table.insert(subInfo, subName .. "=" .. tostring(subValue) .. " (" .. type(subValue) .. ")")
        end
    end

    if #subInfo > 0 then
        return t .. " [" .. table.concat(subInfo, ", ") .. "]"
    end

    return t .. " (no matching sub-fields, raw: " .. tostring(value) .. ")"
end

local function DumpPlayerCandidates()
    local player = GetPlayerPawn()

    if not player then
        print("[Remnant2Unlocker][Debug] No player pawn found")
        return
    end

    local okName, fullName = pcall(function() return player:GetFullName() end)
    print("[Remnant2Unlocker][Debug] Player: " .. (okName and tostring(fullName) or "?"))

    for _, name in ipairs(CANDIDATE_NAMES) do
        local ok, value = pcall(function() return player[name] end)

        if ok and value ~= nil then
            print("[Remnant2Unlocker][Debug] " .. name .. " = " .. DescribeValue(value))
        end
    end

    print("[Remnant2Unlocker][Debug] Dump complete")
end

local function DumpStatsComponentCandidates()
    local player = GetPlayerPawn()

    if not player then
        print("[Remnant2Unlocker][Debug] No player pawn found")
        return
    end

    local okComp, statsComp = pcall(function() return player.StatsComp end)

    if not okComp or not IsValidObject(statsComp) then
        print("[Remnant2Unlocker][Debug] StatsComp not found or invalid")
        return
    end

    for _, statName in ipairs(STAT_NAME_CANDIDATES) do
        local okHas, hasStat = pcall(function() return statsComp:HasStat(statName) end)
        local hasInfo = okHas and tostring(hasStat) or ("call failed: " .. tostring(hasStat))

        local okGet, value = pcall(function() return statsComp:GetStat(statName) end)
        local getInfo

        if okGet then
            getInfo = tostring(value) .. " (" .. type(value) .. ")"
        else
            getInfo = "call failed: " .. tostring(value)
        end

        print("[Remnant2Unlocker][Debug] StatsComp '" .. statName .. "': HasStat=" .. hasInfo .. ", GetStat=" .. getInfo)
    end

    print("[Remnant2Unlocker][Debug] StatsComp dump complete")
end

function DebugDump.Start()
    RegisterKeyBind(Key.F9, function()
        ExecuteInGameThread(function()
            local ok, err = pcall(DumpPlayerCandidates)

            if not ok then
                print("[Remnant2Unlocker][Debug] Dump failed: " .. tostring(err))
            end

            local okStats, statsErr = pcall(DumpStatsComponentCandidates)

            if not okStats then
                print("[Remnant2Unlocker][Debug] StatsComp dump failed: " .. tostring(statsErr))
            end
        end)
    end)

    print("[Remnant2Unlocker] Debug dump ready (press F9 in-game to dump player stat candidates to this log)")
end

return DebugDump
