local json = require("json")
local UEHelpers = require("UEHelpers")

local MovementSpeed = {}

local SETTINGS_PATH = require("paths").File("hotkeys.json")

-- Remnant 2 writes MaxWalkSpeed itself on every movement-state change (0 idle, 175 walk, 300 jog,
-- ...) and leaves it alone in between. So any value we read that isn't the one we last wrote is
-- the game's base speed for the current state, and we scale that instead of a fixed base.
-- MaxWalkSpeed is a float, so compare with a tolerance to absorb the double -> float round trip.
local SPEED_TOLERANCE = 0.5

local currentMultiplier = 1.0
local baseSpeed = nil
local lastAppliedSpeed = nil
local loggedSpeeds = {}
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
        loggedSpeeds = {}
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

local function SpeedsMatch(a, b)
    return a ~= nil and b ~= nil and math.abs(a - b) < SPEED_TOLERANCE
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

local function ReadMaxWalkSpeed(movement)
    local ok, speed = pcall(function()
        return movement.MaxWalkSpeed
    end)

    if not ok then return nil end

    return tonumber(speed)
end

local function ResetMovementSpeedIfNeeded()
    if not wasActive then
        return
    end

    ExecuteInGameThread(function()
        local player = GetPlayerPawn()
        local movement = player and GetMovementComponent(player)

        -- Only undo our own write; if the game has since set a new state speed, that's already correct.
        if movement and baseSpeed and SpeedsMatch(ReadMaxWalkSpeed(movement), lastAppliedSpeed) then
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
        baseSpeed = nil
        lastAppliedSpeed = nil
        loggedSpeeds = {}
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

        local currentSpeed = ReadMaxWalkSpeed(movement)

        if currentSpeed == nil then
            if lastError ~= "maxwalkspeed_missing" then
                print("[Remnant2Unlocker] MaxWalkSpeed missing or unreadable")
                lastError = "maxwalkspeed_missing"
            end

            return
        end

        -- 0 means the character is standing still; nothing to scale until the game sets a speed.
        if currentSpeed <= 0 then
            return
        end

        -- A value we didn't write is the game's speed for a new movement state.
        if not SpeedsMatch(currentSpeed, lastAppliedSpeed) then
            baseSpeed = currentSpeed
        end

        local targetSpeed = baseSpeed * currentMultiplier

        if SpeedsMatch(currentSpeed, targetSpeed) then
            return
        end

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
        lastAppliedSpeed = targetSpeed
        lastError = ""

        -- State changes happen constantly; log each base speed once per multiplier.
        if not loggedSpeeds[baseSpeed] then
            loggedSpeeds[baseSpeed] = true
            print("[Remnant2Unlocker] Applied movement speed: " .. tostring(baseSpeed) .. " -> " .. tostring(targetSpeed))
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

    print("[Remnant2Unlocker] Movement speed system initialized")
end

return MovementSpeed
