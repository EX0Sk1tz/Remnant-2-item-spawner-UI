local json = require("json")

local CONFIG_PATH = require("paths").File("weapon_mod_boosts.json")

local WeaponModConfig = {}

local function ReadAllText(path)
    local file = io.open(path, "r")
    if not file then return nil end

    local content = file:read("*a")
    file:close()

    return content
end

function WeaponModConfig.ReadMultipliers(modKey, defaults)
    local content = ReadAllText(CONFIG_PATH)
    if not content then return defaults end

    local ok, config = pcall(function() return json.decode(content) end)
    if not ok or not config or not config[modKey] then return defaults end

    local merged = {}

    for field, defaultValue in pairs(defaults) do
        local savedValue = config[modKey][field]
        merged[field] = (savedValue ~= nil and tonumber(savedValue)) or defaultValue
    end

    return merged
end

return WeaponModConfig
