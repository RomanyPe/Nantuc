// benchmarks.js

function benchCall() {
  function sum(a, b) { return a + b; }
  let r0 = 0, r1 = 3_000_000;
  while (r1 !== 0) {
    r0 = sum(r0, r1);
    r1 -= 1;
  }
  return r0;
}

function benchMath() {
  let r0 = 0, r1 = 3_000_000;
  while (r1 !== 0) {
    r0 += r1;
    r1 -= 1;
  }
  return r0;
}

function benchAluTput1() {
  let r0 = 0, r7 = 1, r6 = 800_000;
  while (r6 !== 0) {
    r0 += r7; r0 += r7; r0 += r7; r0 += r7;
    r6 -= 1;
  }
  return r0;
}

function benchAluTput4() {
  let r0 = 0, r1 = 0, r2 = 0, r3 = 0;
  let r7 = 1, r6 = 800_000;
  while (r6 !== 0) {
    r0 += r7; r1 += r7; r2 += r7; r3 += r7;
    r6 -= 1;
  }
  return r0 + r1 + r2 + r3;
}

function benchAluTputMixed() {
  let r0 = 0, r1 = 0, r2 = 0, r3 = 0;
  let r7 = 1, r6 = 800_000;
  while (r6 !== 0) {
    r0 += r7;
    r1 -= r7;
    r2 &= r7;
    r3 |= r7;
    r6 -= 1;
  }
  return r0 + r1 + r2 + r3;
}

function benchMemory() {
  const arr = new Int32Array(25_000);
  let total = 0;
  for (let outer = 0; outer < 20; outer++) {
    for (let i = 0; i < 25_000; i++) arr[i] = i;
    let sum = 0;
    for (let i = 0; i < 25_000; i++) sum += arr[i];
    total += sum;
  }
  return total;
}

function benchBitOps() {
  let r0 = 1, r1 = 1, r2 = 0, r3 = 0, r4 = 0, r6 = 3_000_000;
  while (r6 !== 0) {
    r0 >>= r1;
    r2 &= r0;
    r3 |= r0;
    r4 ^= r0;
    r6 -= 1;
  }
  return r0 + r2 + r3 + r4;
}

function benchMul() {
  let r0 = 1, r1 = 3, r6 = 3_000_000;
  while (r6 !== 0) {
    r0 *= r1;
    r6 -= 1;
  }
  return r0;
}

function benchBranch() {
  let r5 = 500_000, r6 = 0, r2 = 0, r3 = 0;
  while (r5 !== 0) {
    r6 += 1;
    if ((r6 & 1) !== 0) {
      r2 += 1;
    } else {
      r3 += 1;
    }
    r5 -= 1;
  }
  return r2 + r3;
}

function benchDivMod() {
  let r0 = 1_000_000, r1 = 7, r2 = 0, r6 = 500_000;
  while (r6 !== 0) {
    r2 += Math.trunc(r0 / r1);
    r2 += r0 % r1;
    r0 += 1;
    r6 -= 1;
  }
  return r2;
}

function benchRecursion() {
  function down(n) {
    if (n <= 0) return 0;
    return down(n - 1);
  }
  let total = 0;
  for (let i = 0; i < 500; i++) total += down(500);
  return total;
}

function benchPointer() {
  // В JS указателей нет; используем Int32Array и индексы.
  const arr = new Int32Array(1000);
  for (let i = 0; i < 1000; i++) arr[i] = i;
  let s = 0;
  for (let rep = 0; rep < 2000; rep++) {
    for (let i = 0; i < 1000; i++) s += arr[i];
  }
  return s;
}

function benchStruct() {
  // Эмуляция структуры: массив объектов {x, y}.
  const pts = new Array(1000).fill(null).map(() => ({ x: 0, y: 0 }));
  let total = 0;
  for (let rep = 0; rep < 200; rep++) {
    for (let i = 0; i < 1000; i++) {
      pts[i].x = i;
      pts[i].y = i + 1;
    }
    let s = 0;
    for (let i = 0; i < 1000; i++) s += pts[i].x + pts[i].y;
    total += s;
  }
  return total;
}

function benchSort() {
  const arr = new Int32Array(200);
  let total = 0;
  for (let rep = 50; rep > 0; rep--) {
    for (let i = 0; i < 200; i++) arr[i] = (i * 37 + rep * 13) % 1000;
    for (let pass = 0; pass < 199; pass++) {
      for (let j = 0; j < 199 - pass; j++) {
        if (arr[j] > arr[j + 1]) {
          const tmp = arr[j];
          arr[j] = arr[j + 1];
          arr[j + 1] = tmp;
        }
      }
    }
    total += arr[0] + arr[199];
  }
  return total;
}

const BENCHES = [
  ["Call", benchCall], ["Math", benchMath],
  ["AluTput1", benchAluTput1], ["AluTput4", benchAluTput4],
  ["AluTputMixed", benchAluTputMixed], ["Memory", benchMemory],
  ["BitOps", benchBitOps], ["Mul", benchMul],
  ["Branch", benchBranch], ["DivMod", benchDivMod],
  ["Recursion", benchRecursion], ["Pointer", benchPointer],
  ["Struct", benchStruct], ["Sort", benchSort],
];

for (const [name, fn] of BENCHES) {
  const t0 = process.hrtime.bigint();
  const res = fn();
  const dt = Number(process.hrtime.bigint() - t0) / 1e9;
  console.error(`${name.padEnd(12)}  ${dt.toFixed(4)} s  (result=${res})`);
}