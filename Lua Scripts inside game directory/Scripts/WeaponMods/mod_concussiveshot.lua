local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local ConcussiveShotDefaults = {
    BlastDamage = nil,
    MaxRange = nil,
    BaseKnockbackDistance = nil,
    AOERadius = nil,
    MaxCharges = nil,
    NumChargesConsumedOnUse = nil,
}

local ConcussiveShotMultipliers = {
    BlastDamage = 10,
    MaxRange = 10,
    BaseKnockbackDistance = 10,
    AOERadius = 10,
    MaxCharges = 10,
    NumChargesConsumedOnUse = 0,
}

local function SetConcussiveShotDefaults(mod)
    ConcussiveShotDefaults.BlastDamage = mod.BlastDamage
    ConcussiveShotDefaults.MaxRange = mod.MaxRange
    ConcussiveShotDefaults.BaseKnockbackDistance = mod.BaseKnockbackDistance
    ConcussiveShotDefaults.AOERadius = mod.AOERadius
    ConcussiveShotDefaults.MaxCharges = mod.MaxCharges
    ConcussiveShotDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModConcussiveShot()
    local ok, err = pcall(function()
        local concussiveShot = FindAllOf("Mod_ConcussiveShot_C")

        if not concussiveShot then
            print("[Remnant2Unlocker] ConcussiveShot not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("ConcussiveShot", ConcussiveShotMultipliers)

        for _, mod in pairs(concussiveShot) do
            if not ConcussiveShotDefaults.BlastDamage then
                SetConcussiveShotDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = ConcussiveShotDefaults.NumChargesConsumedOnUse * ConcussiveShotMultipliers.NumChargesConsumedOnUse
            mod.BlastDamage = ConcussiveShotDefaults.BlastDamage * multipliers.BlastDamage
            mod.MaxRange = ConcussiveShotDefaults.MaxRange * multipliers.MaxRange
            mod.BaseKnockbackDistance = ConcussiveShotDefaults.BaseKnockbackDistance * multipliers.BaseKnockbackDistance
            mod.AOERadius = ConcussiveShotDefaults.AOERadius * multipliers.AOERadius
            mod.MaxCharges = ConcussiveShotDefaults.MaxCharges * multipliers.MaxCharges

            print("[Remnant2Unlocker] ConcussiveShot boosted: BlastDamage=" .. tostring(mod.BlastDamage)
                .. " MaxRange=" .. tostring(mod.MaxRange)
                .. " BaseKnockbackDistance=" .. tostring(mod.BaseKnockbackDistance)
                .. " AOERadius=" .. tostring(mod.AOERadius)
                .. " MaxCharges=" .. tostring(mod.MaxCharges))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModConcussiveShot failed: " .. tostring(err))
    end
end

return {
    ModConcussiveShot = ModConcussiveShot,
}
