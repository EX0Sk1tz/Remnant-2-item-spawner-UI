local json = {}

local function escape_str(s)
    s = s:gsub("\\", "\\\\")
    s = s:gsub('"', '\\"')
    s = s:gsub("\n", "\\n")
    s = s:gsub("\r", "\\r")
    s = s:gsub("\t", "\\t")
    return '"' .. s .. '"'
end

function json.encode(value)
    local t = type(value)

    if t == "nil" then
        return "null"
    elseif t == "boolean" then
        return value and "true" or "false"
    elseif t == "number" then
        return tostring(value)
    elseif t == "string" then
        return escape_str(value)
    elseif t == "table" then
        local isArray = true
        local max = 0

        for k, _ in pairs(value) do
            if type(k) ~= "number" then
                isArray = false
                break
            end
            if k > max then max = k end
        end

        local parts = {}

        if isArray then
            for i = 1, max do
                table.insert(parts, json.encode(value[i]))
            end
            return "[" .. table.concat(parts, ",") .. "]"
        else
            for k, v in pairs(value) do
                table.insert(parts, escape_str(tostring(k)) .. ":" .. json.encode(v))
            end
            return "{" .. table.concat(parts, ",") .. "}"
        end
    end

    return "null"
end

function json.decode(str)
    local pos = 1

    local function skip()
        while true do
            local c = str:sub(pos, pos)
            if c == " " or c == "\n" or c == "\r" or c == "\t" then
                pos = pos + 1
            else
                break
            end
        end
    end

    local function parse_string()
        pos = pos + 1
        local result = ""

        while pos <= #str do
            local c = str:sub(pos, pos)

            if c == '"' then
                pos = pos + 1
                return result
            elseif c == "\\" then
                local n = str:sub(pos + 1, pos + 1)
                if n == "n" then result = result .. "\n"
                elseif n == "r" then result = result .. "\r"
                elseif n == "t" then result = result .. "\t"
                else result = result .. n end
                pos = pos + 2
            else
                result = result .. c
                pos = pos + 1
            end
        end

        return result
    end

    local function parse_number()
        local start = pos

        while str:sub(pos, pos):match("[%d%+%-%.eE]") do
            pos = pos + 1
        end

        return tonumber(str:sub(start, pos - 1))
    end

    local parse_value

    local function parse_array()
        pos = pos + 1
        local arr = {}
        skip()

        if str:sub(pos, pos) == "]" then
            pos = pos + 1
            return arr
        end

        while true do
            table.insert(arr, parse_value())
            skip()

            local c = str:sub(pos, pos)
            if c == "]" then
                pos = pos + 1
                break
            end

            pos = pos + 1
        end

        return arr
    end

    local function parse_object()
        pos = pos + 1
        local obj = {}
        skip()

        if str:sub(pos, pos) == "}" then
            pos = pos + 1
            return obj
        end

        while true do
            skip()
            local key = parse_string()
            skip()
            pos = pos + 1
            obj[key] = parse_value()
            skip()

            local c = str:sub(pos, pos)
            if c == "}" then
                pos = pos + 1
                break
            end

            pos = pos + 1
        end

        return obj
    end

    function parse_value()
        skip()
        local c = str:sub(pos, pos)

        if c == '"' then return parse_string()
        elseif c == "{" then return parse_object()
        elseif c == "[" then return parse_array()
        elseif c == "t" then pos = pos + 4 return true
        elseif c == "f" then pos = pos + 5 return false
        elseif c == "n" then pos = pos + 4 return nil
        else return parse_number() end
    end

    return parse_value()
end

return json