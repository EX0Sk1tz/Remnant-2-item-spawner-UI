local UEHelpers = require("UEHelpers")

local CombatActions = {}

-- Ported from CheatMod (Scripts/player_utils.lua). Uses this mod's own GetPlayerPawn/IsValidObject
-- helpers (already proven in cheats.lua/fov.lua/movement_speed.lua/destroy.lua) instead of
-- CheatMod's UEHelpers.GetPlayer(), which isn't used anywhere else in this mod.

local function IsValidObject(obj)
    if not obj then return false end

    local ok, valid = pcall(function()
        if obj.IsValid then
            return obj:IsValid()
        end

        return true
    end)

    return ok and valid == true
end

local function GetPlayerPawn()
    local ok, pawn = pcall(function()
        local pc = UEHelpers.GetPlayerController()

        if pc and pc.Pawn then
            return pc.Pawn
        end

        return FindFirstOf("RemnantPlayerCharacter")
    end)

    if ok and IsValidObject(pawn) then
        return pawn
    end

    return nil
end

function CombatActions.ReplenishCooldownsAndModPower()
    ExecuteInGameThread(function()
        local player = GetPlayerPawn()

        if not player then
            print("[Remnant2Unlocker] ReplenishCooldownsAndModPower: player pawn not found")
            return
        end

        local ok, err = pcall(function()
            player:ReplenishResources(
                false, -- Health
                false, -- Ammo
                false, -- DragonHearts
                true,  -- Cooldowns
                true,  -- ModPower
                false  -- IsBossRush
            )
        end)

        if ok then
            print("[Remnant2Unlocker] Cooldowns and Mod Power replenished")
        else
            print("[Remnant2Unlocker] ReplenishCooldownsAndModPower failed: " .. tostring(err))
        end
    end)
end

-- Boosts the RateScale of all currently loaded, non-locomotion player AnimSequences. This is a
-- one-shot pass over whatever is loaded right now -- newly streamed-in animations (e.g. after a
-- level transition) won't be affected until this is re-run, same caveat CheatMod documents.
function CombatActions.FastPlayerActions()
    ExecuteInGameThread(function()
        local ok, err = pcall(function()
            local animSequences = FindAllOf("AnimSequence")

            if not animSequences then
                print("[Remnant2Unlocker] FastPlayerActions: no AnimSequence objects found")
                return
            end

            local boosted = 0

            for _, anim in pairs(animSequences) do
                -- Each object is handled in its own pcall, and Default__ class objects (CDOs)
                -- are skipped -- writing a property to one is usually harmless, but destroy.lua
                -- documented a real engine crash from touching a CDO the same way FindAllOf
                -- returns it here, and this loop crashed the whole game once during testing.
                -- Better safe: never touch a CDO, and never let one bad object stop the rest of
                -- the scan.
                pcall(function()
                    local name = anim:GetFullName():lower()

                    if name:find("default__", 1, true) then
                        return
                    end

                    if name:find("player")
                        and not name:find("walk")
                        and not name:find("jog")
                        and not name:find("aim")
                        and not name:find("sprint")
                        and not name:find("run")
                        and not name:find("crouch")
                        and not name:find("evaderoll") then
                        anim.RateScale = 4.0
                        boosted = boosted + 1
                    end

                    if name:find("evaderoll") then
                        -- 1.7 felt better than 2.0 in testing (CheatMod's own tuning note)
                        anim.RateScale = 1.7
                        boosted = boosted + 1
                    end
                end)
            end

            print("[Remnant2Unlocker] Fast Player Actions applied to " .. tostring(boosted) .. " animation(s)")
        end)

        if not ok then
            print("[Remnant2Unlocker] FastPlayerActions failed: " .. tostring(err))
        end
    end)
end

return CombatActions
