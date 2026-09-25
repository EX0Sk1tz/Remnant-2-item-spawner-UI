local UEHelpers = require("UEHelpers")
local Spawner = require("spawner")

local Destroy = {}

-- "Nearby" radius in Unreal units (cm). 500 = 5 meters, generous enough to cover a spawn drop
-- scattering a few items around the player without sweeping the whole room.
local NEARBY_RADIUS = 500

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

-- Every loaded UClass has exactly one "class default object" (CDO), always named
-- "Default__<ClassName>" -- a universal, engine-enforced Unreal naming convention, not a
-- guessed game-specific detail. FindAllOf(className) returns the CDO alongside real in-world
-- instances, and calling an actor-instance UFunction like K2_GetActorLocation/K2_DestroyActor on
-- it crashes straight through pcall with "UObject instance is nullptr" (confirmed in testing
-- 2026-07-11) -- it has to be filtered out *before* the call, the same lesson spawner.lua/
-- queue.lua already apply to ExecuteConsoleCommand's context/player validity check.
local function IsDefaultObject(actor)
    local ok, name = pcall(function() return actor:GetFullName() end)

    return ok and name ~= nil and tostring(name):find("Default__", 1, true) ~= nil
end

-- K2_GetActorLocation/K2_DestroyActor are standard native AActor UFunctions, not guessed
-- game-specific fields -- safe to call on any real (non-CDO) actor reference.
local function GetActorLocation(actor)
    if IsDefaultObject(actor) then
        return nil
    end

    local ok, location = pcall(function() return actor:K2_GetActorLocation() end)

    if ok and location then
        return location
    end

    return nil
end

local function DistanceBetween(a, b)
    local dx = (a.X or 0) - (b.X or 0)
    local dy = (a.Y or 0) - (b.Y or 0)
    local dz = (a.Z or 0) - (b.Z or 0)

    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

local function FormatVector(v)
    if not v then return "nil" end

    return string.format("(%.1f, %.1f, %.1f)", v.X or 0, v.Y or 0, v.Z or 0)
end

local function DestroyActor(actor)
    if not IsValidObject(actor) or IsDefaultObject(actor) then
        return false
    end

    local ok = pcall(function() actor:K2_DestroyActor() end)

    return ok
end

function Destroy.DestroyLastSpawned()
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local actors = Spawner.GetLastSpawnedActors()

            if not actors or #actors == 0 then
                print("[Remnant2Unlocker] DestroyLastSpawned: nothing tracked yet")
                return
            end

            local destroyed = 0

            for _, actor in ipairs(actors) do
                if DestroyActor(actor) then
                    destroyed = destroyed + 1
                end
            end

            print("[Remnant2Unlocker] DestroyLastSpawned: destroyed " .. tostring(destroyed) .. " actor(s)")
        end)

        if not ok then
            print("[Remnant2Unlocker] DestroyLastSpawned failed: " .. tostring(err))
        end
    end)
end

function Destroy.DestroyNearbySpawned()
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local player = GetPlayerPawn()

            if not player then
                print("[Remnant2Unlocker] DestroyNearbySpawned: player pawn not found")
                return
            end

            local playerLocation = GetActorLocation(player)

            if not playerLocation then
                print("[Remnant2Unlocker] DestroyNearbySpawned: player location unavailable")
                return
            end

            local classNames = Spawner.GetKnownSpawnedClassNames()
            local destroyed = 0
            local scannedClasses = 0

            print("[Remnant2Unlocker] DestroyNearbySpawned: player at " .. FormatVector(playerLocation) .. ", scanning " .. tostring(#classNames) .. " known spawned classes")

            for _, className in ipairs(classNames) do
                local okList, list = pcall(function() return FindAllOf(className) end)

                if okList and list then
                    scannedClasses = scannedClasses + 1

                    for _, actor in ipairs(list) do
                        if IsValidObject(actor) then
                            local location = GetActorLocation(actor)

                            if location and DistanceBetween(playerLocation, location) <= NEARBY_RADIUS then
                                if DestroyActor(actor) then
                                    destroyed = destroyed + 1
                                end
                            end
                        end
                    end
                end
            end

            print("[Remnant2Unlocker] DestroyNearbySpawned: destroyed " .. tostring(destroyed) .. " actor(s) within " .. tostring(NEARBY_RADIUS) .. " units (scanned " .. tostring(scannedClasses) .. "/" .. tostring(#classNames) .. " classes)")
        end)

        if not ok then
            print("[Remnant2Unlocker] DestroyNearbySpawned failed: " .. tostring(err))
        end
    end)
end

return Destroy
