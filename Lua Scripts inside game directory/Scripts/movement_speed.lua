local json = require("json")
local UEHelpers = require("UEHelpers")

local MovementSpeed = {}

local SETTINGS_PATH = "Mods/Remnant2Unlocker/hotkeys.json"

local currentMultiplier = 1.0
local baseSpeed = nil
local lastAppliedSpeed = nil
local lastError = ""
local wasActive = false

local function ReadAllText(path)
    local file = io.open(path, "r")
    if not file then return nil end

    local content = file:read("*a")
    file:close()

    return content
end

local function ReloadSettings()
    local content = ReadAllText(SETTINGS_PATH)
    if not content then return end

    local ok, settings = pcall(function()
        return json.decode(content)
    end)

    if not ok or not settings then return end

    local multiplier = tonumber(settings.movementSpeedMultiplier) or 1.0

    if multiplier < 1.0 then multiplier = 1.0 end
    if multiplier > 5.0 then multiplier = 5.0 end

    if multiplier ~= currentMultiplier then
        currentMultiplier = multiplier
        print("[Remnant2Unlocker] Movement speed multiplier updated: " .. tostring(currentMultiplier))
    end
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

local function GetMovementComponent(player)
    if not IsValidObject(player) then
        return nil
    end

    local candidates = {
        "CharacterMovement",
        "MovementComponent",
        "LocomotionComponent"
    }

    for _, propertyName in ipairs(candidates) do
        local ok, component = pcall(function()
            return player[propertyName]
        end)

        if ok and IsValidObject(component) then
            return component
        end
    end

    return nil
end

local function ResetMovementSpeedIfNeeded()
    if not wasActive then
        return
    end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()
        if not player then return end

        local movement = GetMovementComponent(player)
        if not movement then return end

        if baseSpeed then
            local okWrite, err = pcall(function()
                movement.MaxWalkSpeed = baseSpeed
            end)

            if okWrite then
                print("[Remnant2Unlocker] Movement speed reset to base: " .. tostring(baseSpeed))
            else
                print("[Remnant2Unlocker] Failed to reset movement speed: " .. tostring(err))
            end
        end

        wasActive = false
        lastAppliedSpeed = nil
        lastError = ""
    end)
end

local function ApplyMovementSpeed()
    if currentMultiplier <= 1.0 then
        ResetMovementSpeedIfNeeded()
        return
    end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()

        if not player then
            return
        end

        local movement = GetMovementComponent(player)

        if not movement then
            if lastError ~= "movement_missing" then
                print("[Remnant2Unlocker] Movement component missing or invalid")
                lastError = "movement_missing"
            end

            return
        end

        local okRead, currentSpeed = pcall(function()
            return movement.MaxWalkSpeed
        end)

        if not okRead or currentSpeed == nil then
            if lastError ~= "maxwalkspeed_missing" then
                print("[Remnant2Unlocker] MaxWalkSpeed missing or unreadable")
                lastError = "maxwalkspeed_missing"
            end

            return
        end

        local numericSpeed = tonumber(currentSpeed) or 0

        if numericSpeed <= 0 then
            if lastError ~= "invalid_base_speed" then
                print("[Remnant2Unlocker] Ignoring invalid movement base speed: " .. tostring(currentSpeed))
                lastError = "invalid_base_speed"
            end

            return
        end

        if not baseSpeed then
            baseSpeed = tonumber(currentSpeed) or 400
            print("[Remnant2Unlocker] Movement base speed detected: " .. tostring(baseSpeed))
        end

        local targetSpeed = baseSpeed * currentMultiplier

        local okWrite, err = pcall(function()
            movement.MaxWalkSpeed = targetSpeed
        end)

        if not okWrite then
            if lastError ~= tostring(err) then
                print("[Remnant2Unlocker] Failed to apply movement speed: " .. tostring(err))
                lastError = tostring(err)
            end

            return
        end

        wasActive = true
        lastError = ""

        if lastAppliedSpeed ~= targetSpeed then
            lastAppliedSpeed = targetSpeed
            print("[Remnant2Unlocker] Applied movement speed: " .. tostring(targetSpeed))
        end
    end)
end

function MovementSpeed.Start()
    ReloadSettings()

    LoopAsync(1000, function()
        ReloadSettings()
    end)

    LoopAsync(250, function()
        ApplyMovementSpeed()
    end)

    if currentMultiplier > 1.0 then
        print("[Remnant2Unlocker] Movement speed system initialized")
    else
        print("[Remnant2Unlocker] Movement speed system initialized, inactive at multiplier 1.0")
    end
end

return MovementSpeed