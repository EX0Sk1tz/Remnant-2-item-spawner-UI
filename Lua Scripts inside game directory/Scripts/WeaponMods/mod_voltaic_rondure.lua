local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local VoltaicRondureDefaults = {
    PulseDelay = nil,
    OrbDamage = nil,
    ProjectileLifetime = nil,
    EffectRadius = nil,
    ShockDamage = nil,
    ShockDuration = nil,
    NumChargesConsumedOnUse = nil,
}

local VoltaicRondureMultipliers = {
    PulseDelay = 0.01,
    OrbDamage = 10,
    ProjectileLifetime = 1,
    EffectRadius = 3,
    ShockDamage = 10,
    ShockDuration = 2,
    NumChargesConsumedOnUse = 0,
}

local function SetVoltaicRondureDefaults(mod)
    VoltaicRondureDefaults.PulseDelay = mod.PulseDelay
    VoltaicRondureDefaults.OrbDamage = mod.OrbDamage
    VoltaicRondureDefaults.ProjectileLifetime = mod.ProjectileLifetime
    VoltaicRondureDefaults.EffectRadius = mod.EffectRadius
    VoltaicRondureDefaults.ShockDamage = mod.ShockDamage
    VoltaicRondureDefaults.ShockDuration = mod.ShockDuration
    VoltaicRondureDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModVoltaicRondure()
    local ok, err = pcall(function()
        local voltaicRondure = FindAllOf("Mod_VoltaicRondure_C")

        if not voltaicRondure then
            print("[Remnant2Unlocker] VoltaicRondure not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("VoltaicRondure", VoltaicRondureMultipliers)

        for _, mod in pairs(voltaicRondure) do
            if not VoltaicRondureDefaults.PulseDelay then
                SetVoltaicRondureDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = VoltaicRondureDefaults.NumChargesConsumedOnUse * VoltaicRondureMultipliers.NumChargesConsumedOnUse
            mod.PulseDelay = multipliers.PulseDelay
            mod.OrbDamage = VoltaicRondureDefaults.OrbDamage * multipliers.OrbDamage
            mod.ProjectileLifetime = VoltaicRondureDefaults.ProjectileLifetime * multipliers.ProjectileLifetime
            mod.EffectRadius = VoltaicRondureDefaults.EffectRadius * multipliers.EffectRadius
            mod.ShockDamage = VoltaicRondureDefaults.ShockDamage * multipliers.ShockDamage
            mod.ShockDuration = VoltaicRondureDefaults.ShockDuration * multipliers.ShockDuration

            print("[Remnant2Unlocker] VoltaicRondure boosted: PulseDelay=" .. tostring(mod.PulseDelay)
                .. " OrbDamage=" .. tostring(mod.OrbDamage)
                .. " ProjectileLifetime=" .. tostring(mod.ProjectileLifetime)
                .. " EffectRadius=" .. tostring(mod.EffectRadius)
                .. " ShockDamage=" .. tostring(mod.ShockDamage)
                .. " ShockDuration=" .. tostring(mod.ShockDuration))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModVoltaicRondure failed: " .. tostring(err))
    end
end

return {
    ModVoltaicRondure = ModVoltaicRondure,
}
