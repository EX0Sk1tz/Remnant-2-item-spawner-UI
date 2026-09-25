local UEHelpers = require("UEHelpers")
local ItemToSpawn=nil
local Quantity=1
local Level=0
NotifyOnNewObject("/Script/GunfireRuntime.ItemInstanceData", function(item)
    if not ItemToSpawn then return end
    item.Quantity=Quantity
    item.Level=Level
end)
-- EXAMPLE: `summon Material_Scraps_C 10 10`
-- summons 10 instances of 10 scrap
local function SpawnItem(Parameters)
    if #Parameters < 1 then return false end
    LoadAsset(Parameters[1])
    
    if #Parameters < 2 then return false end
    ItemToSpawn=Parameters[1]

    local KSL = UEHelpers.GetKismetSystemLibrary(true)
    local Player = UEHelpers.GetPlayerController()
    local Context = UEHelpers.GetWorldContextObject()

    if #Parameters > 2 then Quantity=Parameters[3] end
    if #Parameters > 3 then Level=Parameters[4] end
    
    for i=1,Parameters[2],1
    do
       KSL:ExecuteConsoleCommand(Context,string.format("summon %s",Parameters[1]),Player)
    end
    ItemToSpawn=nil
    Quantity=1
    Level=0
    return true
end
RegisterConsoleCommandHandler("summon", function(FullCommand, Parameters)
    return SpawnItem(Parameters)
end)
RegisterConsoleCommandHandler("Summon", function(FullCommand, Parameters)
    return SpawnItem(Parameters)
end)