local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local SandstormDefaults = {
    CycloneDuration = nil,
    CycloneBaseRadius = nil,
    CycloneHomingRadius = nil,
    CycloneDPS = nil,
    CycloneDamageFrequency = nil,
    NumChargesConsumedOnUse = nil,
}

local SandstormMultipliers = {
    CycloneDuration = 10,
    CycloneBaseRadius = 10,
    CycloneHomingRadius = 10,
    CycloneDPS = 10,
    CycloneDamageFrequency = 10,
    NumChargesConsumedOnUse = 0,
}

local function SetSandstormDefaults(mod)
    SandstormDefaults.CycloneDuration = mod.CycloneDuration
    SandstormDefaults.CycloneBaseRadius = mod.CycloneBaseRadius
    SandstormDefaults.CycloneHomingRadius = mod.CycloneHomingRadius
    SandstormDefaults.CycloneDPS = mod.CycloneDPS
    SandstormDefaults.CycloneDamageFrequency = mod.CycloneDamageFrequency
    SandstormDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModSandstorm()
    local ok, err = pcall(function()
        local sandstorm = FindAllOf("Mod_Sandstorm_C")

        if not sandstorm then
            print("[Remnant2Unlocker] Sandstorm not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("Sandstorm", SandstormMultipliers)

        for _, mod in pairs(sandstorm) do
            if not SandstormDefaults.CycloneDuration then
                SetSandstormDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = SandstormDefaults.NumChargesConsumedOnUse * SandstormMultipliers.NumChargesConsumedOnUse
            mod.CycloneDuration = SandstormDefaults.CycloneDuration * multipliers.CycloneDuration
            mod.CycloneBaseRadius = SandstormDefaults.CycloneBaseRadius * multipliers.CycloneBaseRadius
            mod.CycloneHomingRadius = SandstormDefaults.CycloneHomingRadius * multipliers.CycloneHomingRadius
            mod.CycloneDPS = SandstormDefaults.CycloneDPS * multipliers.CycloneDPS
            mod.CycloneDamageFrequency = SandstormDefaults.CycloneDamageFrequency * multipliers.CycloneDamageFrequency

            print("[Remnant2Unlocker] Sandstorm boosted: CycloneDuration=" .. tostring(mod.CycloneDuration)
                .. " CycloneBaseRadius=" .. tostring(mod.CycloneBaseRadius)
                .. " CycloneHomingRadius=" .. tostring(mod.CycloneHomingRadius)
                .. " CycloneDPS=" .. tostring(mod.CycloneDPS)
                .. " CycloneDamageFrequency=" .. tostring(mod.CycloneDamageFrequency))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModSandstorm failed: " .. tostring(err))
    end
end

return {
    ModSandstorm = ModSandstorm,
}
