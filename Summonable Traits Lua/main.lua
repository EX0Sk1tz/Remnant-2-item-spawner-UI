local UEHelpers = require("UEHelpers")

local function Console(input)
    local KSL = UEHelpers.GetKismetSystemLibrary(true)
     KSL:ExecuteConsoleCommand(UEHelpers.GetWorldContextObject(),input,UEHelpers.GetPlayerController())
end

local function SplitCamel(s)
    return (s:gsub("%u", " %1"):gsub("^.", string.upper)):match'^%s*(.*)'
end

function UEHelpers.GetKismetTextLibrary(ForceInvalidateCache)
    local DefaultObject

    if not ForceInvalidateCache then
        DefaultObject = ModRef:GetSharedVariable("UEHelpers_KismetTextLibrary")
        if DefaultObject and DefaultObject:IsValid() then return DefaultObject end
    end

    DefaultObject = StaticFindObject("/Script/Engine.Default__KismetTextLibrary")
    ModRef:SetSharedVariable("UEHelpers_KismetTextLibrary", DefaultObject)
    if not DefaultObject:IsValid() then error(string.format("%s not found", "/Script/Engine.Default__KismetTextLibrary")) end

    return DefaultObject
end

local function ToSoftClass(input)
    local KSL = UEHelpers.GetKismetSystemLibrary(true)

    local obj =StaticFindObject(tostring(input)):GetCDO()
    if not (obj and obj:IsValid()) then print("NOT Valid Object") return end
    print(string.format("Valid Object: %s",obj:GetFullName()))
    local class = obj:GetClass()
    if not (class and class:IsValid()) then print("NOT Valid Class") return end
    print(string.format("Valid Class: %s",class:GetFullName()))
    return KSL:Conv_ClassToSoftClassReference(class)
end

RegisterConsoleCommandHandler("SummonTrait", function(FullCommand, Parameters)
    if #Parameters < 1 then print("") return false end
    local KTL = UEHelpers.GetKismetTextLibrary(true)

    local _,_, name= string.find(Parameters[1],"Trait_(%a*)_C")
    if not name then print("Not a valid Trait") return false end
    local KSL = UEHelpers.GetKismetSystemLibrary(true)
    local Base = "/Game/World_Base/Items/Traits/Material_AwardTrait_Base.Material_AwardTrait_Base_C"
    LoadAsset(Parameters[1])
    LoadAsset(Base)

    local BP = ToSoftClass(tostring(Parameters[1]))
    local Spawn = StaticFindObject("/Game/World_Base/Items/Traits/Material_AwardTrait_Base.Default__Material_AwardTrait_Base_C")

    if BP and KSL:IsValidSoftClassReference(BP) then
        Spawn:SetPropertyValue("Trait BP",BP)
        Spawn:SetPropertyValue("Label",KTL:Conv_StringToText(string.format("Award Trait: %s",SplitCamel(name))))
        Console("summon "..Base)
        print("Summoning Trait: "..name)
    else
        print("Invalid SoftClass Reference")
    end
    -- SummonTrait /Game/World_Base/Items/Traits/Archetype/AmmoReserves/Trait_AmmoReserves.Trait_AmmoReserves_C

    return false
end)


local PlayerInventory
-- On client restart ----------------------------------------------------------------------
RegisterHook("/Script/Engine.PlayerController:ClientRestart", function(PlayerSelf)
    local PlayerController = PlayerSelf:get()
	local PlayerExists = PlayerController:IsValid() and
						 PlayerController.Player:IsValid() and 
						 PlayerController.Character:IsValid() and
						 not PlayerController:HasAnyInternalFlags(EInternalObjectFlags.PendingKill)
    if not PlayerExists then
        return
    end
	
	-- Gather Character's ID then inventory ------------------------------------------------
	local CharacterFullName = PlayerController.Character:GetFullName()
    local CharacterStringTable = {}

    for i in CharacterFullName:gmatch("%S+") do
        table.insert(CharacterStringTable, i)
    end

    CharacterId = CharacterStringTable[#CharacterStringTable]
	
	PlayerInventory = StaticFindObject(string.format("%s.Inventory", CharacterId))
end)

RegisterConsoleCommandHandler("AddTrait", function(FullCommand, Parameters)
    if #Parameters < 1 then print("") return false end
    local KTL = UEHelpers.GetKismetTextLibrary(true)

    local _,_, name= string.find(Parameters[1],"Trait_(%a*)_C")
    if not name then print("Not a valid Trait") return false end
    local KSL = UEHelpers.GetKismetSystemLibrary(true)
    local Base = "/Game/World_Base/Items/Traits/Material_AwardTrait_Base.Material_AwardTrait_Base_C"
    LoadAsset(Parameters[1])
    LoadAsset(Base)

    local BP = ToSoftClass(tostring(Parameters[1]))
    local Spawn = StaticFindObject("/Game/World_Base/Items/Traits/Material_AwardTrait_Base.Default__Material_AwardTrait_Base_C")

    if BP and KSL:IsValidSoftClassReference(BP) then
        Spawn:SetPropertyValue("Trait BP",BP)
        Spawn:SetPropertyValue("Label",KTL:Conv_StringToText(string.format("Award Trait: %s",SplitCamel(name))))
        PlayerInventory:K2_AddItem(StaticFindObject(Base), 1, 0, "", false)
        --Console("summon "..Base)
        print("Summoning Trait: "..name)
    else
        print("Invalid SoftClass Reference")
    end
    -- AddTrait /Game/World_Base/Items/Traits/Archetype/AmmoReserves/Trait_AmmoReserves.Trait_AmmoReserves_C

    return false
end)