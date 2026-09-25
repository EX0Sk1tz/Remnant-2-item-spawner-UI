local HotShotMod = require("WeaponMods.mod_hotshot")
local SandstormMod = require("WeaponMods.mod_sandstorm")
local ConcussiveShotMod = require("WeaponMods.mod_concussiveshot")
local HelixMod = require("WeaponMods.mod_helix")
local StatisBeamMod = require("WeaponMods.mod_statisbeam")
local VoltaicRondureMod = require("WeaponMods.mod_voltaic_rondure")
local ScrapshotMod = require("WeaponMods.mod_scrapshot")
local RottedArrowMod = require("WeaponMods.mod_rottedarrow")
local WeaponMods = require("WeaponMods.weapon_mods")

local WeaponModCommands = {}

local function RegisterBoostCommand(command, applyFn)
    RegisterConsoleCommandHandler(command, function(FullCommand, Parameters, Ar)
        local ok, err = pcall(applyFn)

        if not ok then
            print("[Remnant2Unlocker] " .. command .. " failed: " .. tostring(err))
        end

        return true
    end)
end

function WeaponModCommands.Start()
    RegisterBoostCommand("boost_hotshot", HotShotMod.ModHotShot)
    RegisterBoostCommand("boost_sandstorm", SandstormMod.ModSandstorm)
    RegisterBoostCommand("boost_concussiveshot", ConcussiveShotMod.ModConcussiveShot)
    RegisterBoostCommand("boost_helix", HelixMod.ModHelix)
    RegisterBoostCommand("boost_statisbeam", StatisBeamMod.ModStatisBeam)
    RegisterBoostCommand("boost_voltaicrondure", VoltaicRondureMod.ModVoltaicRondure)
    RegisterBoostCommand("boost_scrapshot", ScrapshotMod.ModScrapshot)
    RegisterBoostCommand("boost_rottedarrow", RottedArrowMod.ModRottedArrow)
    RegisterBoostCommand("boost_weapon_mods", WeaponMods.EnableAllWeaponMods)

    print("[Remnant2Unlocker] Weapon mod boost commands initialized")
end

return WeaponModCommands
