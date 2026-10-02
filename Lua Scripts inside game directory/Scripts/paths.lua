-- Resolves the Remnant2Unlocker mod folder once, so every module reads/writes its JSON files in
-- the right place regardless of UE4SS layout.
--
-- io.open resolves relative paths against the game process's current directory, not UE4SS's.
-- The tagged UE4SS releases keep UE4SS and Mods\ next to the exe, so "Mods/Remnant2Unlocker/" works
-- there. The newer experimental builds ("v3.0.1 Beta", also shipped in Allow Asset Mods) nest
-- everything under ue4ss\Mods\ - on Steam as well as Game Pass - so the same relative path points
-- at a folder that doesn't exist (confirmed from users' UE4SS.logs: "items.json not found" /
-- "command_queue.json not found" while the mod itself loaded fine).
--
-- A folder only counts if it holds this script: a leftover Mods\Remnant2Unlocker with just an
-- items.json in it (an older install, a manual workaround) is not where UE4SS loaded us from, and
-- picking it made the mod read a stale items.json and never see the app's commands (a user's
-- UE4SS.log: "Mod folder: Mods/Remnant2Unlocker/", then "command_queue.json not found").

local Paths = {}

local function FileExists(path)
    local file = io.open(path, "r")

    if file then
        file:close()
        return true
    end

    return false
end

local function IsModFolder(dir)
    return FileExists(dir .. "Scripts/paths.lua") and FileExists(dir .. "items.json")
end

local function FromScriptLocation()
    local ok, info = pcall(debug.getinfo, 1, "S")

    if not ok or not info or type(info.source) ~= "string" then
        return nil
    end

    -- source is "@<full path>\Remnant2Unlocker\scripts\paths.lua"; strip "scripts\paths.lua".
    local dir = info.source:gsub("^@", ""):match("^(.*[/\\])[Ss]cripts[/\\][^/\\]+$")

    if dir and FileExists(dir .. "items.json") then
        return dir
    end

    return nil
end

-- UE4SS running from a ue4ss\ subfolder (and not also next to the exe) loads mods from there,
-- so look there first.
local function Candidates()
    local flat = "Mods/Remnant2Unlocker/"
    local nested = "ue4ss/Mods/Remnant2Unlocker/"

    if FileExists("ue4ss/UE4SS.dll") and not FileExists("UE4SS.dll") then
        return { nested, flat, "../Mods/Remnant2Unlocker/" }
    end

    return { flat, nested, "../Mods/Remnant2Unlocker/" }
end

local function Resolve()
    local fromScript = FromScriptLocation()

    if fromScript then
        return fromScript
    end

    local candidates = Candidates()

    for _, candidate in ipairs(candidates) do
        if IsModFolder(candidate) then
            return candidate
        end
    end

    -- No folder with our scripts in it (unusual layout): settle for one that has items.json.
    for _, candidate in ipairs(candidates) do
        if FileExists(candidate .. "items.json") then
            return candidate
        end
    end

    -- Nothing found: keep the historical default so the "not found" messages stay the same.
    return "Mods/Remnant2Unlocker/"
end

Paths.ModDir = Resolve()

print("[Remnant2Unlocker] Mod folder: " .. Paths.ModDir)

function Paths.File(name)
    return Paths.ModDir .. name
end

return Paths
