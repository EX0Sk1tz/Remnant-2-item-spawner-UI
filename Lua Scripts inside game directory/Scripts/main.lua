print("[Remnant2Unlocker] main.lua loaded")

local Queue = require("queue")
local Hotkeys = require("hotkeys")
local MovementSpeed = require("movement_speed")

Queue.Start()
Hotkeys.Start()
MovementSpeed.Start()

RegisterKeyBind(Key.F8, function()
    print("[Remnant2Unlocker] Bridge is running")
end)