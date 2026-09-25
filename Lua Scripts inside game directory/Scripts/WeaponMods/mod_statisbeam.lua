local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local StatisBeamDefaults = {
    Damage = nil,
    Duration = nil,
    RequiredHitDuration = nil,
    NumChargesConsumedOnUse = nil,
}

local StatisBeamMultipliers = {
    Damage = 10,
    Duration = 10,
    RequiredHitDuration = 10,
    NumChargesConsumedOnUse = 0,
}

local function SetStatisBeamDefaults(mod)
    StatisBeamDefaults.Damage = mod.Damage
    StatisBeamDefaults.Duration = mod.Duration
    StatisBeamDefaults.RequiredHitDuration = mod.RequiredHitDuration
    StatisBeamDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModStatisBeam()
    local ok, err = pcall(function()
        local statisBeam = FindAllOf("Mod_StasisBeam_C")

        if not statisBeam then
            print("[Remnant2Unlocker] StatisBeam not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("StatisBeam", StatisBeamMultipliers)

        for _, mod in pairs(statisBeam) do
            if not StatisBeamDefaults.Damage then
                SetStatisBeamDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = StatisBeamDefaults.NumChargesConsumedOnUse * StatisBeamMultipliers.NumChargesConsumedOnUse
            mod.Damage = StatisBeamDefaults.Damage * multipliers.Damage
            mod.Duration = StatisBeamDefaults.Duration * multipliers.Duration
            mod.RequiredHitDuration = StatisBeamDefaults.RequiredHitDuration * multipliers.RequiredHitDuration

            print("[Remnant2Unlocker] StatisBeam boosted: Damage=" .. tostring(mod.Damage)
                .. " Duration=" .. tostring(mod.Duration)
                .. " RequiredHitDuration=" .. tostring(mod.RequiredHitDuration))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModStatisBeam failed: " .. tostring(err))
    end
end

return {
    ModStatisBeam = ModStatisBeam,
}
