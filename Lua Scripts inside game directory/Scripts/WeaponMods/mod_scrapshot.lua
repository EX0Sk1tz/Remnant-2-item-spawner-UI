local WeaponModConfig = require("WeaponMods.weapon_mod_config")

local ScrapshotDefaults = {
    MaxCharges = nil,
    BlastRadius = nil,
    DOTDamage = nil,
    CaltropDuration = nil,
    BleedDamage = nil,
    BleedDuration = nil,
    NumChargesConsumedOnUse = nil,
}

local ScrapshotMultipliers = {
    MaxCharges = 10,
    BlastRadius = 10,
    DOTDamage = 10,
    CaltropDuration = 10,
    BleedDamage = 10,
    BleedDuration = 10,
    NumChargesConsumedOnUse = 0,
}

local function SetScrapshotDefaults(mod)
    ScrapshotDefaults.MaxCharges = mod.MaxCharges
    ScrapshotDefaults.BlastRadius = mod.BlastRadius
    ScrapshotDefaults.DOTDamage = mod.DOTDamage
    ScrapshotDefaults.CaltropDuration = mod.CaltropDuration
    ScrapshotDefaults.BleedDamage = mod.BleedDamage
    ScrapshotDefaults.BleedDuration = mod.BleedDuration
    ScrapshotDefaults.NumChargesConsumedOnUse = mod.NumChargesConsumedOnUse
end

local function ModScrapshot()
    local ok, err = pcall(function()
        local scrapshot = FindAllOf("Mod_Scrapshot_C")

        if not scrapshot then
            print("[Remnant2Unlocker] Scrapshot not found")
            return
        end

        local multipliers = WeaponModConfig.ReadMultipliers("Scrapshot", ScrapshotMultipliers)

        for _, mod in pairs(scrapshot) do
            if not ScrapshotDefaults.MaxCharges then
                SetScrapshotDefaults(mod)
            end

            mod.NumChargesConsumedOnUse = ScrapshotDefaults.NumChargesConsumedOnUse * ScrapshotMultipliers.NumChargesConsumedOnUse
            mod.MaxCharges = ScrapshotDefaults.MaxCharges * multipliers.MaxCharges
            mod.BlastRadius = ScrapshotDefaults.BlastRadius * multipliers.BlastRadius
            mod.DOTDamage = ScrapshotDefaults.DOTDamage * multipliers.DOTDamage
            mod.CaltropDuration = ScrapshotDefaults.CaltropDuration * multipliers.CaltropDuration
            mod.BleedDamage = ScrapshotDefaults.BleedDamage * multipliers.BleedDamage
            mod.BleedDuration = ScrapshotDefaults.BleedDuration * multipliers.BleedDuration

            print("[Remnant2Unlocker] Scrapshot boosted: MaxCharges=" .. tostring(mod.MaxCharges)
                .. " BlastRadius=" .. tostring(mod.BlastRadius)
                .. " DOTDamage=" .. tostring(mod.DOTDamage)
                .. " CaltropDuration=" .. tostring(mod.CaltropDuration)
                .. " BleedDamage=" .. tostring(mod.BleedDamage)
                .. " BleedDuration=" .. tostring(mod.BleedDuration)
                .. " NumChargesConsumedOnUse=" .. tostring(mod.NumChargesConsumedOnUse))
        end
    end)

    if not ok then
        print("[Remnant2Unlocker] ModScrapshot failed: " .. tostring(err))
    end
end

return {
    ModScrapshot = ModScrapshot,
}
