local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local HelixDefaults = {
    MaxCharges = nil,
    ImpactDamage = nil,
    SideWinderDamage = nil,
    SideWinderCount = nil,
    NumChargesConsumedOnUse = nil,
}

local HelixMultipliers = {
    MaxCharges = 10,
    ImpactDamage = 10,
    SideWinderDamage = 10,
    SideWinderCount = 10,
    NumChargesConsumedOnUse = 0,
}

local function SetHelixDefaults(mod)
    HelixDefaults.MaxCharges = mod.MaxCharges
    HelixDefaults.ImpactDamage = mod.ImpactDamage
    HelixDefaults.SideWinderDamage = mod.SideWinderDamage
    HelixDefaults.SideWinderCount = mod.SideWinderCount
    HelixDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModHelix()
    local ok, err = pcall(function()
        local helix = FindAllOf("Mod_Helix_C")

        if not helix then
            print("[Remnant2Unlocker] Helix not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("Helix", HelixMultipliers)

        for _, mod in pairs(helix) do
            if not HelixDefaults.MaxCharges then
                SetHelixDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = HelixDefaults.NumChargesConsumedOnUse * HelixMultipliers.NumChargesConsumedOnUse
            mod.MaxCharges = HelixDefaults.MaxCharges * multipliers.MaxCharges
            mod.ImpactDamage = HelixDefaults.ImpactDamage * multipliers.ImpactDamage
            mod.SideWinderDamage = HelixDefaults.SideWinderDamage * multipliers.SideWinderDamage
            mod.SideWinderCount = HelixDefaults.SideWinderCount * multipliers.SideWinderCount

            print("[Remnant2Unlocker] Helix boosted: MaxCharges=" .. tostring(mod.MaxCharges)
                .. " ImpactDamage=" .. tostring(mod.ImpactDamage)
                .. " SideWinderDamage=" .. tostring(mod.SideWinderDamage)
                .. " SideWinderCount=" .. tostring(mod.SideWinderCount))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModHelix failed: " .. tostring(err))
    end
end

return {
    ModHelix = ModHelix,
}
