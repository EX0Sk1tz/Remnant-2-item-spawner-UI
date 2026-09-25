local UEHelpers = require("UEHelpers")
local json = require("json")

local Spawner = {}

-- Actor(s) created by the most recent SpawnPath call, tracked so DestroyLastSpawned (see
-- Scripts/destroy.lua) can target them by object reference instead of a camera-aim trace, which
-- is what makes the built-in "DestroyTarget" console command hit the floor instead of a small
-- item pickup. Only the latest spawn is kept -- actor references don't survive a game restart,
-- so DestroyNearbySpawned can't use them for older items; see knownSpawnedClasses below instead.
local lastSpawnedActors = {}

-- Every Blueprint class name ever spawned through this mod, persisted to disk so
-- DestroyNearbySpawned can scan for old items after a restart without having to FindAllOf() the
-- entire ~800-class item catalog every time (that took ~48s and crashed on a class default
-- object in testing -- see Scripts/destroy.lua). Lazily loaded once, then kept in memory as a
-- set and only re-written to disk when a genuinely new class is spawned.
local SPAWNED_CLASSES_PATH = require("paths").File("spawned_classes.json")
local knownSpawnedClasses = nil

-- Some Cheat Mod builds (seen on Game Pass) only accept "summon <path>" and silently spawn
-- nothing when drop/stack/level arguments follow it. We can't tell up front which build is
-- installed, so the first argument-carrying summon that produces no new actor is retried as a
-- plain summon, and the result decides the mode for the rest of the session:
--   "unknown" -> nothing proven yet, fallback allowed
--   "args"    -> an argument summon produced an actor; never fall back
--   "plain"   -> only plain summons work; send those directly from now on
--   "off"     -> neither form was detected (actor tracking unreliable here); stop retrying so
--                we don't double-spawn on every call
local summonMode = "unknown"

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

    -- Some items.json paths carry their own summon arguments (Prism Fragments are stored as
    -- "<path>_C 1 1 31"). BuildSummonCommand appends drop/stack/level itself, so keep only the
    -- asset path -- otherwise the arguments end up doubled and GetClassNameFromPath can't match.
    value = value:match("^(%S+)") or value

    return value
end

local function ClampNumber(value, fallback, minValue, maxValue)
    local n = tonumber(value) or fallback

    if n < minValue then n = minValue end
    if n > maxValue then n = maxValue end

    return n
end

local function BuildSummonCommand(normalizedPath, drop, stack, level)
    if level > 0 then
        return string.format("summon %s %d %d %d", normalizedPath, drop, stack, level)
    end

    return string.format("summon %s %d %d", normalizedPath, drop, stack)
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

-- The spawn path looks like "/Game/.../Weapon_CrescentMoon.Weapon_CrescentMoon_C" -- the
-- segment after the last "." is the actual Blueprint class name, which is what FindAllOf
-- expects.
local function GetClassNameFromPath(path)
    if not path then return nil end

    return tostring(path):match("%.([%w_]+)$")
end

-- GetFullName() returns a name that is unique per UObject instance within the running game
-- (engine-enforced), so it's a safe identity key across two separate FindAllOf snapshots even
-- though each call returns fresh Lua wrapper tables for the same underlying objects.
local function GetActorKey(actor)
    local ok, name = pcall(function() return actor:GetFullName() end)

    if ok and name then
        return name
    end

    return tostring(actor)
end

local function SnapshotActorsByClass(className)
    local snapshot = {}

    if not className then
        return snapshot
    end

    local ok, list = pcall(function() return FindAllOf(className) end)

    if not ok or not list then
        return snapshot
    end

    for _, actor in ipairs(list) do
        if IsValidObject(actor) then
            snapshot[GetActorKey(actor)] = actor
        end
    end

    return snapshot
end

local function LoadKnownSpawnedClasses()
    if knownSpawnedClasses then
        return knownSpawnedClasses
    end

    local set = {}

    pcall(function()
        local file = io.open(SPAWNED_CLASSES_PATH, "r")
        if not file then return end

        local content = file:read("*a")
        file:close()

        local list = json.decode(content)
        if not list then return end

        for _, className in ipairs(list) do
            set[className] = true
        end
    end)

    knownSpawnedClasses = set

    return set
end

local function RememberSpawnedClass(className)
    if not className then return end

    local set = LoadKnownSpawnedClasses()

    if set[className] then
        return
    end

    set[className] = true

    local list = {}

    for name in pairs(set) do
        table.insert(list, name)
    end

    pcall(function()
        local file = io.open(SPAWNED_CLASSES_PATH, "w")
        if not file then return end

        file:write(json.encode(list))
        file:close()
    end)
end

-- Runs the summon right here; only called from SpawnPath's game-thread callback. Returns true if
-- the console command could be sent.
--
-- track=false skips the FindAllOf snapshots around the summon (and with them DestroyLastSpawned
-- tracking and the plain-summon detection). Batch spawns use that: FindAllOf walks every loaded
-- object of the class and has crashed this mod before, and back-to-back spawns run it while the
-- previous items' assets are still streaming in -- two scans per item was the heaviest thing a
-- batch did on the game thread.
local function RunSummonNow(command, className, plainCommand, plainRepeat, track)
    local sent = false
    local untrackedClassName = nil

    if track == false then
        untrackedClassName = className
        className = nil
    end

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

        -- "summon" spawns its actor(s) synchronously inside this native call, so the
        -- before/after snapshot taken immediately around it reliably captures exactly the
        -- actor(s) this command just created.
        local function RunAndCollect(cmd, times)
            print("[Remnant2Unlocker] ExecuteConsoleCommand: " .. cmd .. (times > 1 and (" (x" .. times .. ")") or ""))

            local before = SnapshotActorsByClass(className)

            for _ = 1, times do
                ksl:ExecuteConsoleCommand(context, cmd, player)
            end

            local newActors = {}

            if className then
                for key, actor in pairs(SnapshotActorsByClass(className)) do
                    if not before[key] then
                        table.insert(newActors, actor)
                    end
                end
            end

            return newActors
        end

        local newActors

        if summonMode == "plain" and plainCommand then
            newActors = RunAndCollect(plainCommand, plainRepeat)
        else
            newActors = RunAndCollect(command, 1)

            if className and plainCommand and plainCommand ~= command then
                if #newActors > 0 then
                    summonMode = "args"
                elseif summonMode == "unknown" then
                    print("[Remnant2Unlocker] Summon with arguments spawned nothing, retrying without arguments")

                    newActors = RunAndCollect(plainCommand, plainRepeat)

                    if #newActors > 0 then
                        summonMode = "plain"
                        print("[Remnant2Unlocker] Plain summon worked: this Cheat Mod ignores summon arguments, so stack size and item level can't be applied")
                    else
                        summonMode = "off"
                        print("[Remnant2Unlocker] Plain summon also spawned nothing detectable, argument fallback disabled for this session")
                    end
                end
            end
        end

        if #newActors > 0 then
            lastSpawnedActors = newActors
        end

        if className then
            RememberSpawnedClass(className)
        elseif track == false then
            -- Still record the class for DestroyNearbySpawned, just without the scans.
            RememberSpawnedClass(untrackedClassName)
        end

        sent = true
    end)

    if ok and sent then
        print("[Remnant2Unlocker] ExecuteConsoleCommand called successfully")
    elseif not ok then
        print("[Remnant2Unlocker] ExecuteConsoleCommand failed: " .. tostring(err))
    end

    return ok and sent
end

local function BuildSpawn(path, dropQuantity, stackSize, itemLevel)
    local normalizedPath = NormalizePath(path)

    if normalizedPath == "" then
        return nil
    end

    local drop = ClampNumber(dropQuantity, 1, 1, 999)
    local stack = ClampNumber(stackSize, 1, 1, 999)
    local level = tonumber(itemLevel) or 0

    return {
        command = BuildSummonCommand(normalizedPath, drop, stack, level),
        className = GetClassNameFromPath(normalizedPath),
        -- The plain form can't carry a drop count, so it's repeated instead.
        plainCommand = "summon " .. normalizedPath,
        plainRepeat = drop,
    }
end

local function SentCommand(spawn)
    return summonMode == "plain" and spawn.plainCommand or spawn.command
end

-- Spawns handed to ExecuteInGameThread that haven't run yet. Batch spawns wait for IsIdle() so
-- they never queue a second summon behind one the game thread hasn't got to (e.g. during a
-- loading screen) -- summons queued up like that would then run back-to-back.
local pendingSpawns = 0

-- Queues the spawn for the game thread and returns immediately. Never call this from inside an
-- ExecuteInGameThread callback: queuing game-thread work from within one crashed this UE4SS build
-- (memcpy inside its ProcessEvent callback dispatch). Batch spawns pass track=false, see
-- RunSummonNow.
function Spawner.SpawnPath(path, dropQuantity, stackSize, itemLevel, track)
    local spawn = BuildSpawn(path, dropQuantity, stackSize, itemLevel)

    if not spawn then
        return false, "Empty spawn path"
    end

    pendingSpawns = pendingSpawns + 1

    ExecuteInGameThread(function()
        RunSummonNow(spawn.command, spawn.className, spawn.plainCommand, spawn.plainRepeat, track)
        pendingSpawns = math.max(0, pendingSpawns - 1)
    end)

    return true, SentCommand(spawn)
end

-- True once every queued spawn has run on the game thread.
function Spawner.IsIdle()
    return pendingSpawns == 0
end

-- Returns the actor(s) created by the most recent SpawnPath call (there can be more than one
-- if dropQuantity > 1), or an empty table if nothing is tracked yet.
function Spawner.GetLastSpawnedActors()
    return lastSpawnedActors
end

-- Every Blueprint class name ever spawned through this mod (this session or a previous one).
function Spawner.GetKnownSpawnedClassNames()
    local set = LoadKnownSpawnedClasses()
    local list = {}

    for className in pairs(set) do
        table.insert(list, className)
    end

    return list
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
