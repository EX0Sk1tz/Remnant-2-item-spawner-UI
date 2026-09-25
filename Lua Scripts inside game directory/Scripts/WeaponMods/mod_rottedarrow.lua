local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local RottedArrowDefaults = {
    NumChargesConsumedOnUse = nil,
    MaxCharges = nil,
    WeakSpotMod = nil,
    ImpactDamage = nil,
    BlastDamage = nil,
    DOTDamage = nil,
    CloudDuration = nil,
    BlastRadius = nil,
    CloudDamagePerSecond = nil,
}

local RottedArrowMultipliers = {
    NumChargesConsumedOnUse = 0,
    MaxCharges = 10,
    WeakSpotMod = 10,
    ImpactDamage = 10,
    BlastDamage = 0,
    DOTDamage = 10,
    CloudDuration = 10,
    BlastRadius = 10,
    CloudDamagePerSecond = 10,
}

local function SetRottedArrowDefaults(mod)
    RottedArrowDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
    RottedArrowDefaults.MaxCharges = mod.MaxCharges
    RottedArrowDefaults.WeakSpotMod = mod.WeakSpotMod
    RottedArrowDefaults.ImpactDamage = mod.ImpactDamage
    RottedArrowDefaults.BlastDamage = mod.BlastDamage
    RottedArrowDefaults.DOTDamage = mod.DOTDamage
    RottedArrowDefaults.CloudDuration = mod.CloudDuration
    RottedArrowDefaults.BlastRadius = mod.BlastRadius
    RottedArrowDefaults.CloudDamagePerSecond = mod.CloudDamagePerSecond
end

local function ModRottedArrow()
    local ok, err = pcall(function()
        local rottedArrow = FindAllOf("Mod_RottedArrow_C")

        if not rottedArrow then
            print("[Remnant2Unlocker] RottedArrow not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("RottedArrow", RottedArrowMultipliers)

        for _, mod in pairs(rottedArrow) do
            if not RottedArrowDefaults.NumChargesConsumedOnUse then
                SetRottedArrowDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = RottedArrowDefaults.NumChargesConsumedOnUse * RottedArrowMultipliers.NumChargesConsumedOnUse
            mod.MaxCharges = RottedArrowDefaults.MaxCharges * multipliers.MaxCharges
            mod.WeakSpotMod = RottedArrowDefaults.WeakSpotMod * multipliers.WeakSpotMod
            mod.ImpactDamage = RottedArrowDefaults.ImpactDamage * multipliers.ImpactDamage
            mod.BlastDamage = RottedArrowDefaults.BlastDamage * RottedArrowMultipliers.BlastDamage
            mod.DOTDamage = RottedArrowDefaults.DOTDamage * multipliers.DOTDamage
            mod.CloudDuration = RottedArrowDefaults.CloudDuration * multipliers.CloudDuration
            mod.BlastRadius = RottedArrowDefaults.BlastRadius * multipliers.BlastRadius
            mod.CloudDamagePerSecond = RottedArrowDefaults.CloudDamagePerSecond * multipliers.CloudDamagePerSecond

            print("[Remnant2Unlocker] RottedArrow boosted: MaxCharges=" .. tostring(mod.MaxCharges)
                .. " WeakSpotMod=" .. tostring(mod.WeakSpotMod)
                .. " ImpactDamage=" .. tostring(mod.ImpactDamage)
                .. " DOTDamage=" .. tostring(mod.DOTDamage)
                .. " CloudDuration=" .. tostring(mod.CloudDuration)
                .. " BlastRadius=" .. tostring(mod.BlastRadius)
                .. " CloudDamagePerSecond=" .. tostring(mod.CloudDamagePerSecond)
                .. " NumChargesConsumedOnUse=" .. tostring(mod.NumChargesConsumedOnUse))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModRottedArrow failed: " .. tostring(err))
    end
end

return {
    ModRottedArrow = ModRottedArrow,
}
