local HotShotMod = require("WeaponMods.mod_hotshot")
local SandstormMod = require("WeaponMods.mod_sandstorm")
local ConcussiveShotMod = require("WeaponMods.mod_concussiveshot")
local HelixMod = require("WeaponMods.mod_helix")
local StatisBeamMod = require("WeaponMods.mod_statisbeam")
local VoltaicRondureMod = require("WeaponMods.mod_voltaic_rondure")
local ScrapshotMod = require("WeaponMods.mod_scrapshot")
local RottedArrowMod = require("WeaponMods.mod_rottedarrow")

local WeaponMods = {}

function WeaponMods.EnableAllWeaponMods()
    HotShotMod.ModHotShot()
    SandstormMod.ModSandstorm()
    ConcussiveShotMod.ModConcussiveShot()
    HelixMod.ModHelix()
    StatisBeamMod.ModStatisBeam()
    VoltaicRondureMod.ModVoltaicRondure()
    ScrapshotMod.ModScrapshot()
    RottedArrowMod.ModRottedArrow()
end

return WeaponMods
