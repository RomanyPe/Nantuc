# benchmarks.rb

def bench_call
  sum = ->(a, b) { a + b }
  r0, r1 = 0, 3_000_000
  while r1 != 0
    r0 = sum.call(r0, r1)
    r1 -= 1
  end
  r0
end

def bench_math
  r0, r1 = 0, 3_000_000
  while r1 != 0
    r0 += r1
    r1 -= 1
  end
  r0
end

def bench_alu_tput1
  r0, r7, r6 = 0, 1, 800_000
  while r6 != 0
    r0 += r7; r0 += r7; r0 += r7; r0 += r7
    r6 -= 1
  end
  r0
end

def bench_alu_tput4
  r0 = r1 = r2 = r3 = 0
  r7, r6 = 1, 800_000
  while r6 != 0
    r0 += r7; r1 += r7; r2 += r7; r3 += r7
    r6 -= 1
  end
  r0 + r1 + r2 + r3
end

def bench_alu_tput_mixed
  r0 = r1 = r2 = r3 = 0
  r7, r6 = 1, 800_000
  while r6 != 0
    r0 += r7
    r1 -= r7
    r2 &= r7
    r3 |= r7
    r6 -= 1
  end
  r0 + r1 + r2 + r3
end

def bench_memory
  arr = Array.new(25_000, 0)
  total = 0
  20.times do
    25_000.times { |i| arr[i] = i }
    total += arr.sum
  end
  total
end

def bench_bit_ops
  r0 = r1 = 1
  r2 = r3 = r4 = 0
  r6 = 3_000_000
  while r6 != 0
    r0 >>= r1
    r2 &= r0
    r3 |= r0
    r4 ^= r0
    r6 -= 1
  end
  r0 + r2 + r3 + r4
end

def bench_mul
  r0, r1, r6 = 1, 3, 3_000_000
  while r6 != 0
    r0 *= r1
    r6 -= 1
  end
  r0
end

def bench_branch
  r5, r6, r2, r3 = 500_000, 0, 0, 0
  while r5 != 0
    r6 += 1
    if (r6 & 1) != 0
      r2 += 1
    else
      r3 += 1
    end
    r5 -= 1
  end
  r2 + r3
end

def bench_div_mod
  r0, r1, r2, r6 = 1_000_000, 7, 0, 500_000
  while r6 != 0
    r2 += r0 / r1
    r2 += r0 % r1
    r0 += 1
    r6 -= 1
  end
  r2
end

def bench_recursion
  down = ->(n) { n <= 0 ? 0 : down.call(n - 1) }
  total = 0
  500.times { total += down.call(500) }
  total
end

def bench_pointer
  # В Ruby указателей нет; эмулируем через массив.
  arr = Array.new(1000) { |i| i }
  s = 0
  2000.times do
    1000.times { |i| s += arr[i] }
  end
  s
end

def bench_struct
  # Эмуляция структуры: массив пар [x, y].
  pts = Array.new(1000) { [0, 0] }
  total = 0
  200.times do
    1000.times { |i| pts[i] = [i, i + 1] }
    s = 0
    pts.each { |x, y| s += x + y }
    total += s
  end
  total
end

def bench_sort
  arr = Array.new(200, 0)
  total = 0
  50.downto(1) do |rep|
    200.times { |i| arr[i] = (i * 37 + rep * 13) % 1000 }
    (0...199).each do |pass|
      (0...(199 - pass)).each do |j|
        if arr[j] > arr[j + 1]
          arr[j], arr[j + 1] = arr[j + 1], arr[j]
        end
      end
    end
    total += arr[0] + arr[199]
  end
  total
end

BENCHES = [
  ["Call", method(:bench_call)],
  ["Math", method(:bench_math)],
  ["AluTput1", method(:bench_alu_tput1)],
  ["AluTput4", method(:bench_alu_tput4)],
  ["AluTputMixed", method(:bench_alu_tput_mixed)],
  ["Memory", method(:bench_memory)],
  ["BitOps", method(:bench_bit_ops)],
  ["Mul", method(:bench_mul)],
  ["Branch", method(:bench_branch)],
  ["DivMod", method(:bench_div_mod)],
  ["Recursion", method(:bench_recursion)],
  ["Pointer", method(:bench_pointer)],
  ["Struct", method(:bench_struct)],
  ["Sort", method(:bench_sort)],
]

BENCHES.each do |name, m|
  t0 = Process.clock_gettime(Process::CLOCK_MONOTONIC)
  res = m.call
  dt = Process.clock_gettime(Process::CLOCK_MONOTONIC) - t0
  $stderr.puts format("%-12s  %8.4f s  (result=%d)", name, dt, res)
end