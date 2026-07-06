local UEHelpers = require("UEHelpers")

local Spawner = {}

local function NormalizePath(path)
    if not path then
        return ""
    end

    local value = tostring(path)
    value = value:gsub("^%s+", ""):gsub("%s+$", "")
    value = value:gsub("|", "")

    if value:lower():sub(1, 7) == "summon " then
        value = value:sub(8)
        value = value:gsub("^%s+", ""):gsub("%s+$", "")
    end

    return value
end

local function ClampNumber(value, fallback, minValue, maxValue)
    local n = tonumber(value) or fallback

    if n < minValue then n = minValue end
    if n > maxValue then n = maxValue end

    return n
end

local function BuildSummonCommand(path, dropQuantity, stackSize, itemLevel)
    local normalizedPath = NormalizePath(path)

    if normalizedPath == "" then
        return nil, "Empty spawn path"
    end

    local drop = ClampNumber(dropQuantity, 1, 1, 999)
    local stack = ClampNumber(stackSize, 1, 1, 999)
    local level = tonumber(itemLevel) or 0

    if level > 0 then
        return string.format("summon %s %d %d %d", normalizedPath, drop, stack, level), nil
    end

    return string.format("summon %s %d %d", normalizedPath, drop, stack), nil
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

local function ExecuteConsoleCommand(command)
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local ksl = UEHelpers.GetKismetSystemLibrary(true)
            local context = UEHelpers.GetWorldContextObject()
            local player = UEHelpers.GetPlayerController()

            -- ksl:ExecuteConsoleCommand is a native UFunction call; an invalid context/player
            -- (e.g. no active world yet, loading screen, death/respawn) makes UE4SS jump through
            -- a null function pointer and crash the whole game. pcall cannot catch that, so the
            -- validity check has to happen before the call, not around it.
            if not IsValidObject(ksl) or not IsValidObject(context) or not IsValidObject(player) then
                print("[Remnant2Unlocker] ExecuteConsoleCommand skipped: world/player context not ready")
                return
            end

            print("[Remnant2Unlocker] ExecuteConsoleCommand: " .. command)

            ksl:ExecuteConsoleCommand(context, command, player)
        end)

        if ok then
            print("[Remnant2Unlocker] ExecuteConsoleCommand called successfully")
        else
            print("[Remnant2Unlocker] ExecuteConsoleCommand failed: " .. tostring(err))
        end
    end)
end

function Spawner.SpawnPath(path, dropQuantity, stackSize, itemLevel)
    local command, err = BuildSummonCommand(path, dropQuantity, stackSize, itemLevel)

    if not command then
        return false, err
    end

    ExecuteConsoleCommand(command)

    return true, command
end

function Spawner.SpawnItem(item, dropQuantity, stackSize)
    if not item then
        return false, "Item is nil"
    end

    local itemLevel = 0

    local path = tostring(item.path or "")
    local name = tostring(item.name or "")
    local itemType = tostring(item.type or "")

    if path:find("RelicFragment_", 1, true)
        or path:lower():find("/items/gems/", 1, true)
        or name:lower():find("relic fragment", 1, true)
        or itemType:lower():find("relic fragment", 1, true) then
        itemLevel = 31
    end

    return Spawner.SpawnPath(item.path, dropQuantity, stackSize, itemLevel)
end

return Spawner