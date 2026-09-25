-- Resolves the Remnant2Unlocker mod folder once, so every module reads/writes its JSON files in
-- the right place regardless of UE4SS layout.
--
-- io.open resolves relative paths against the game process's current directory, not UE4SS's.
-- Steam/Epic installs keep UE4SS and Mods\ next to the exe, so "Mods/Remnant2Unlocker/" works
-- there. The Game Pass UE4SS build nests everything under ue4ss\Mods\, so the same relative path
-- points at a folder that doesn't exist (confirmed from a user's UE4SS.log: "items.json not
-- found" / "command_queue.json not found" while the mod itself loaded fine).

local Paths = {}

local function FileExists(path)
    local file = io.open(path, "r")

    if file then
        file:close()
        return true
    end

    return false
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

local function Resolve()
    local fromScript = FromScriptLocation()

    if fromScript then
        return fromScript
    end

    for _, candidate in ipairs({
        "Mods/Remnant2Unlocker/",
        "ue4ss/Mods/Remnant2Unlocker/",
        "../Mods/Remnant2Unlocker/"
    }) do
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
