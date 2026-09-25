local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local HotShotDefaults = {
    FireDuration = nil,
    FireBaseDamage = nil,
    ModDuration = nil,
}

local HotShotMultipliers = {
    FireDuration = 10,
    FireBaseDamage = 100,
    ModDuration = 10,
}

local function SetHotShotDefaults(mod)
    HotShotDefaults.FireDuration = mod.FireDuration
    HotShotDefaults.FireBaseDamage = mod.FireBaseDamage
    HotShotDefaults.ModDuration = mod.ModDuration
end

local function ModHotShot()
    local ok, err = pcall(function()
        local hotShot = FindAllOf("Mod_Hotshot_C")

        if not hotShot then
            print("[Remnant2Unlocker] HotShot not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("HotShot", HotShotMultipliers)

        for _, mod in pairs(hotShot) do
            if not HotShotDefaults.FireDuration then
                SetHotShotDefaults(mod)
            end

            mod.FireDuration = HotShotDefaults.FireDuration * multipliers.FireDuration
            mod.FireBaseDamage = HotShotDefaults.FireBaseDamage * multipliers.FireBaseDamage
            mod.ModDuration = HotShotDefaults.ModDuration * multipliers.ModDuration

            print("[Remnant2Unlocker] HotShot boosted: FireDuration=" .. tostring(mod.FireDuration)
                .. " FireBaseDamage=" .. tostring(mod.FireBaseDamage)
                .. " ModDuration=" .. tostring(mod.ModDuration))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModHotShot failed: " .. tostring(err))
    end
end

return {
    ModHotShot = ModHotShot,
}
