-- benchmarks.lua
local function bench_call()
    local function sum(a, b) return a + b end
    local r0, r1 = 0, 3000000
    while r1 ~= 0 do
        r0 = sum(r0, r1)
        r1 = r1 - 1
    end
    return r0
end

local function bench_math()
    local r0, r1 = 0, 3000000
    while r1 ~= 0 do
        r0 = r0 + r1
        r1 = r1 - 1
    end
    return r0
end

local function bench_alu_tput1()
    local r0, r7, r6 = 0, 1, 800000
    while r6 ~= 0 do
        r0 = r0 + r7; r0 = r0 + r7; r0 = r0 + r7; r0 = r0 + r7
        r6 = r6 - 1
    end
    return r0
end

local function bench_alu_tput4()
    local r0, r1, r2, r3 = 0, 0, 0, 0
    local r7, r6 = 1, 800000
    while r6 ~= 0 do
        r0 = r0 + r7; r1 = r1 + r7; r2 = r2 + r7; r3 = r3 + r7
        r6 = r6 - 1
    end
    return r0 + r1 + r2 + r3
end

local function bench_alu_tput_mixed()
    local r0, r1, r2, r3 = 0, 0, 0, 0
    local r7, r6 = 1, 800000
    while r6 ~= 0 do
        r0 = r0 + r7
        r1 = r1 - r7
        r2 = r2 & r7
        r3 = r3 | r7
        r6 = r6 - 1
    end
    return r0 + r1 + r2 + r3
end

local function bench_memory()
    local arr = {}
    local total = 0
    for _ = 1, 20 do
        for i = 0, 24999 do arr[i] = i end
        local s = 0
        for i = 0, 24999 do s = s + arr[i] end
        total = total + s
    end
    return total
end

local function bench_bit_ops()
    local r0, r1 = 1, 1
    local r2, r3, r4 = 0, 0, 0
    local r6 = 3000000
    while r6 ~= 0 do
        r0 = r0 >> r1
        r2 = r2 & r0
        r3 = r3 | r0
        r4 = r4 ~ r0
        r6 = r6 - 1
    end
    return r0 + r2 + r3 + r4
end

local function bench_mul()
    local r0, r1, r6 = 1, 3, 3000000
    while r6 ~= 0 do
        r0 = r0 * r1
        r6 = r6 - 1
    end
    return r0
end

local function bench_branch()
    local r5, r6, r2, r3 = 500000, 0, 0, 0
    while r5 ~= 0 do
        r6 = r6 + 1
        if (r6 & 1) ~= 0 then
            r2 = r2 + 1
        else
            r3 = r3 + 1
        end
        r5 = r5 - 1
    end
    return r2 + r3
end

local function bench_div_mod()
    local r0, r1, r2, r6 = 1000000, 7, 0, 500000
    while r6 ~= 0 do
        r2 = r2 + math.floor(r0 / r1)   -- целочисленное деление
        r2 = r2 + (r0 % r1)
        r0 = r0 + 1
        r6 = r6 - 1
    end
    return r2
end

local function bench_recursion()
    local function down(n)
        if n <= 0 then return 0 end
        return down(n - 1)
    end
    local total = 0
    for _ = 1, 500 do total = total + down(500) end
    return total
end

local function bench_pointer()
    -- В Lua нет указателей; используем таблицу с числовыми индексами.
    local arr = {}
    local n = 1000
    for i = 0, n - 1 do arr[i] = i end
    local s = 0
    for _ = 1, 2000 do
        for i = 0, n - 1 do s = s + arr[i] end
    end
    return s
end

local function bench_struct()
    local pts = {}
    local total = 0
    for _ = 1, 200 do
        for i = 0, 999 do pts[i] = {x = i, y = i + 1} end
        local s = 0
        for i = 0, 999 do s = s + pts[i].x + pts[i].y end
        total = total + s
    end
    return total
end

local function bench_sort()
    local arr = {}
    local total = 0
    for rep = 50, 1, -1 do
        for i = 0, 199 do arr[i] = (i * 37 + rep * 13) % 1000 end
        for pass = 0, 198 do
            for j = 0, 198 - pass do
                if arr[j] > arr[j + 1] then
                    arr[j], arr[j + 1] = arr[j + 1], arr[j]
                end
            end
        end
        total = total + arr[0] + arr[199]
    end
    return total
end

local benches = {
    {"Call", bench_call}, {"Math", bench_math},
    {"AluTput1", bench_alu_tput1}, {"AluTput4", bench_alu_tput4},
    {"AluTputMixed", bench_alu_tput_mixed}, {"Memory", bench_memory},
    {"BitOps", bench_bit_ops}, {"Mul", bench_mul},
    {"Branch", bench_branch}, {"DivMod", bench_div_mod},
    {"Recursion", bench_recursion}, {"Pointer", bench_pointer},
    {"Struct", bench_struct}, {"Sort", bench_sort},
}

for _, b in ipairs(benches) do
    local name, fn = b[1], b[2]
    local t0 = os.clock()
    local res = fn()
    local dt = os.clock() - t0
    io.stderr:write(string.format("%-12s  %.4f s  (result=%d)\n", name, dt, res))
end