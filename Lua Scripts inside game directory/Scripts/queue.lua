local Spawner = require("spawner")
local json = require("json")

local Queue = {}

local MOD_DIR = "Mods/Remnant2Unlocker/"
local ITEMS_PATH = MOD_DIR .. "items.json"
local QUEUE_PATH = MOD_DIR .. "command_queue.json"
local STATUS_PATH = MOD_DIR .. "status.json"

local lastProcessedId = 0
local isBusy = false
local cancelRequested = false
local items = {}

local safeQueue = {}
local safeIndex = 1
local safeDelayMs = 500
local safeLabel = "Group"
local safeStackSize = 1

local status = {
    ready = false,
    busy = false,
    lastCommandId = 0,
    lastAction = "none",
    lastMessage = "Starting bridge",
    lastSpawned = nil,
    processedCount = 0,
    totalCount = 0,
    error = nil
}

local function GetCommandValue(command, lowerName, upperName)
    if not command then
        return nil
    end

    if command[lowerName] ~= nil then
        return command[lowerName]
    end

    return command[upperName]
end

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

local function DecodeJson(content)
    if not content or content == "" then return nil end

    local ok, decoded = pcall(function()
        return json.decode(content)
    end)

    if not ok then
        print("[Remnant2Unlocker] JSON decode failed: " .. tostring(decoded))
        return nil
    end

    return decoded
end

local function EncodeJson(value)
    local ok, encoded = pcall(function()
        return json.encode(value)
    end)

    if not ok then
        print("[Remnant2Unlocker] JSON encode failed: " .. tostring(encoded))
        return "{}"
    end

    return encoded
end

local function SaveStatus()
    status.busy = isBusy
    WriteAllText(STATUS_PATH, EncodeJson(status))
end

-- Tick() calls SetStatus every 200ms while the queue file is missing/invalid; without this
-- dedup, an idle/misconfigured install would flood UE4SS.log with an identical line 5x/sec.
local lastLoggedMessage = nil

local function SetStatus(message, errorMessage)
    status.lastMessage = message
    status.error = errorMessage
    SaveStatus()

    if message ~= lastLoggedMessage then
        print("[Remnant2Unlocker] " .. message)
        lastLoggedMessage = message
    end
end

local function ExecuteConsoleCommand(command)
    if not command or tostring(command) == "" then
        SetStatus("Console command blocked", "Empty console command")
        return
    end

    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local UEHelpers = require("UEHelpers")
            local ksl = UEHelpers.GetKismetSystemLibrary(true)
            local context = UEHelpers.GetWorldContextObject()
            local player = UEHelpers.GetPlayerController()

            print("[Remnant2Unlocker] ExecuteConsoleCommand: " .. tostring(command))

            ksl:ExecuteConsoleCommand(context, tostring(command), player)
        end)

        if ok then
            print("[Remnant2Unlocker] ExecuteConsoleCommand called successfully")
        else
            print("[Remnant2Unlocker] ExecuteConsoleCommand failed: " .. tostring(err))
        end
    end)
end

local function LoadItems()
    local content = ReadAllText(ITEMS_PATH)

    if not content then
        items = {}
        status.totalCount = 0
        status.ready = false
        SetStatus("items.json not found", "Missing items.json")
        return
    end

    local decoded = DecodeJson(content)

    if not decoded then
        items = {}
        status.totalCount = 0
        status.ready = false
        SetStatus("items.json could not be parsed", "Invalid items.json")
        return
    end

    items = decoded
    status.totalCount = #items
    status.ready = true

    SetStatus("Loaded " .. tostring(#items) .. " items", nil)
end

local function BaselineQueueId()
    local content = ReadAllText(QUEUE_PATH)
    local command = DecodeJson(content)

    local commandId = tonumber(GetCommandValue(command, "id", "Id")) or 0

    if commandId > 0 then
        lastProcessedId = commandId
        print("[Remnant2Unlocker] Queue baseline id: " .. tostring(lastProcessedId))
    end
end

local function FindItemByName(name)
    if not name then return nil end

    local query = tostring(name):lower()

    for _, item in ipairs(items) do
        if item.name and tostring(item.name):lower() == query then
            return item
        end
    end

    return nil
end

local function GetItemsForTypes(types)
    local lookup = {}
    local result = {}

    if not types then return result end

    for _, itemType in ipairs(types) do
        lookup[tostring(itemType):lower()] = true
    end

    for _, item in ipairs(items) do
        local itemType = tostring(item.type or ""):lower()

        if lookup[itemType] and item.path then
            table.insert(result, item)
        end
    end

    return result
end

local function ClampNumber(value, fallback, minValue, maxValue)
    local n = tonumber(value) or fallback

    if n < minValue then n = minValue end
    if n > maxValue then n = maxValue end

    return n
end

local function IsRelicFragment(entry)
    if not entry then return false end

    local path = tostring(entry.path or entry.Path or "")
    local name = tostring(entry.name or entry.Name or "")
    local itemType = tostring(entry.type or entry.Type or "")

    if path:find("RelicFragment_", 1, true) then return true end
    if path:lower():find("/items/gems/", 1, true) then return true end
    if name:lower():find("relic fragment", 1, true) then return true end
    if itemType:lower():find("relic fragment", 1, true) then return true end

    return false
end

local function GetEntryPath(entry)
    if type(entry) == "string" then
        return entry
    end

    return entry.path or entry.Path
end

local function GetDropQuantity(command)
    return ClampNumber(GetCommandValue(command, "dropQuantity", "DropQuantity"), 1, 1, 999)
end

local function GetStackSize(command)
    return ClampNumber(GetCommandValue(command, "stackSize", "StackSize"), 1, 1, 999)
end

local function GetItemLevel(command, entry)
    local commandLevel = tonumber(GetCommandValue(command, "itemLevel", "ItemLevel")) or 0

    if commandLevel > 0 then
        return commandLevel
    end

    if IsRelicFragment(entry) then
        return 31
    end

    return 0
end

local function SpawnSingle(command)
    local path = GetCommandValue(command, "path", "Path")
    local name = GetCommandValue(command, "name", "Name")

    local item = nil

    if not path and name then
        item = FindItemByName(name)
        if item then
            path = item.path
        end
    end

    if not item and path then
        item = {
            path = path,
            name = name or path,
            type = ""
        }
    end

    local ok, result = Spawner.SpawnPath(
        path,
        GetDropQuantity(command),
        GetStackSize(command),
        GetItemLevel(command, item))

    if ok then
        status.lastSpawned = result
        status.processedCount = status.processedCount + 1
        SetStatus("Spawned item: " .. tostring(name or result), nil)
    else
        SetStatus("Spawn failed", result)
    end
end

local function FinishSafeQueue(message, errorMessage)
    isBusy = false
    safeQueue = {}
    safeIndex = 1
    cancelRequested = false

    SetStatus(message, errorMessage)
end

local function ProcessNextSafeItem()
    if cancelRequested then
        FinishSafeQueue(
            "Group spawn cancelled at " .. tostring(status.processedCount) .. " / " .. tostring(status.totalCount),
            "Cancelled by user")
        return
    end

    if safeIndex > #safeQueue then
        FinishSafeQueue(
            "Safe group spawn finished: " .. tostring(status.processedCount) .. " / " .. tostring(status.totalCount),
            nil)
        return
    end

    local entry = safeQueue[safeIndex]
    local path = GetEntryPath(entry)

    local ok, result = Spawner.SpawnPath(
        path,
        1,
        safeStackSize or 1,
        IsRelicFragment(entry) and 31 or 0)

    if ok then
        status.lastSpawned = result
        status.processedCount = status.processedCount + 1
        status.lastMessage = "Safe spawning " .. safeLabel .. ": " .. tostring(status.processedCount) .. " / " .. tostring(status.totalCount)
        status.error = nil
    else
        status.lastMessage = "Safe spawn failed at " .. tostring(status.processedCount + 1) .. " / " .. tostring(status.totalCount)
        status.error = tostring(result)
        print("[Remnant2Unlocker] Safe spawn failed: " .. tostring(result))
    end

    SaveStatus()

    safeIndex = safeIndex + 1

    ExecuteWithDelay(safeDelayMs, function()
        ProcessNextSafeItem()
    end)
end

local function StartSafeSpawn(paths, label, command)
    if not paths or #paths == 0 then
        SetStatus("No items found for safe group spawn", "No paths")
        isBusy = false
        SaveStatus()
        return
    end

    safeQueue = paths
    safeIndex = 1
    safeLabel = label or "Group"
    cancelRequested = false

    safeDelayMs = ClampNumber(GetCommandValue(command, "delayMs", "DelayMs"), 500, 100, 10000)
    safeStackSize = ClampNumber(GetCommandValue(command, "stackSize", "StackSize"), 1, 1, 999)

    status.processedCount = 0
    status.totalCount = #safeQueue
    status.lastMessage = "Starting safe group spawn: " .. safeLabel .. " 0 / " .. tostring(status.totalCount)
    status.error = nil

    SaveStatus()

    print("[Remnant2Unlocker] Starting safe group spawn: " .. safeLabel)
    print("[Remnant2Unlocker] Total items: " .. tostring(status.totalCount))
    print("[Remnant2Unlocker] Delay per item: " .. tostring(safeDelayMs) .. "ms")

    ExecuteWithDelay(safeDelayMs, function()
        ProcessNextSafeItem()
    end)
end

local function UnlockTypesSafe(command)
    local types = GetCommandValue(command, "types", "Types")
    local entries = GetItemsForTypes(types)

    local label = "selected group"

    if types and #types == 1 then
        label = tostring(types[1])
    end

    StartSafeSpawn(entries, label, command)
end

local function SpawnManySafe(command)
    local paths = GetCommandValue(command, "paths", "Paths")
    StartSafeSpawn(paths, "custom group", command)
end

local function ProcessCommand(command)
    if not command then return end

    local commandId = tonumber(GetCommandValue(command, "id", "Id")) or 0
    local action = GetCommandValue(command, "action", "Action")

    if commandId <= lastProcessedId then
        return
    end

    lastProcessedId = commandId

    if action == "idle" then
        return
    end

    status.lastCommandId = commandId
    status.lastAction = tostring(action or "unknown")

    print("[Remnant2Unlocker] Processing command " .. tostring(commandId) .. ": " .. tostring(action))

    if action == "cancel" then
        cancelRequested = true
        SetStatus("Cancel requested", nil)
        return
    end

    if isBusy then
        SetStatus("Command ignored because bridge is busy", "Bridge busy")
        return
    end

    isBusy = true
    SaveStatus()

    if action == "spawn" then
        status.processedCount = 0
        status.totalCount = 1
        SpawnSingle(command)
        isBusy = false
        SaveStatus()

    elseif action == "spawn_many" then
        SpawnManySafe(command)

    elseif action == "spawn_many_safe" then
        SpawnManySafe(command)

    elseif action == "unlock_types" then
        UnlockTypesSafe(command)

    elseif action == "unlock_types_safe" then
        UnlockTypesSafe(command)

    elseif action == "reload_items" then
        LoadItems()
        isBusy = false
        SaveStatus()
    elseif action == "console_command" then
        local consoleCommand = GetCommandValue(command, "command", "Command")
        ExecuteConsoleCommand(consoleCommand)
        SetStatus("Console command sent: " .. tostring(consoleCommand), nil)
        isBusy = false
        SaveStatus()

    else
        SetStatus("Unknown command: " .. tostring(action), "Unknown action")
        isBusy = false
        SaveStatus()
    end
end

function Queue.Tick()
    local content = ReadAllText(QUEUE_PATH)

    if not content then
        SetStatus("command_queue.json not found", "Missing command_queue.json")
        return
    end

    local command = DecodeJson(content)

    if not command then
        SetStatus("command_queue.json could not be parsed", "Invalid command_queue.json")
        return
    end

    ProcessCommand(command)
end

local function ScheduleTick()
    ExecuteWithDelay(200, function()
        Queue.Tick()
        ScheduleTick()
    end)
end

function Queue.Start()
    LoadItems()
    BaselineQueueId()
    SaveStatus()
    ScheduleTick()
end

return Queue