// benchmarks.rs
// rustc -O benchmarks.rs -o benchmarks
// (или cargo build --release)
use std::time::Instant;

#[inline(never)]
fn sum_fn(a: i32, b: i32) -> i32 { a.wrapping_add(b) }

fn bench_call() -> i64 {
    let (mut r0, mut r1) = (0i32, 3_000_000i32);
    while r1 != 0 { r0 = sum_fn(r0, r1); r1 -= 1; }
    r0 as i64
}
fn bench_math() -> i64 {
    let (mut r0, mut r1) = (0i32, 3_000_000i32);
    while r1 != 0 { r0 = r0.wrapping_add(r1); r1 -= 1; }
    r0 as i64
}
fn bench_alu_tput1() -> i64 {
    let (mut r0, r7, mut r6) = (0i32, 1i32, 800_000i32);
    while r6 != 0 {
        r0 = r0.wrapping_add(r7); r0 = r0.wrapping_add(r7);
        r0 = r0.wrapping_add(r7); r0 = r0.wrapping_add(r7);
        r6 -= 1;
    }
    r0 as i64
}
fn bench_alu_tput4() -> i64 {
    let (mut r0, mut r1, mut r2, mut r3) = (0i32, 0i32, 0i32, 0i32);
    let (r7, mut r6) = (1i32, 800_000i32);
    while r6 != 0 {
        r0 = r0.wrapping_add(r7); r1 = r1.wrapping_add(r7);
        r2 = r2.wrapping_add(r7); r3 = r3.wrapping_add(r7);
        r6 -= 1;
    }
    r0 as i64 + r1 as i64 + r2 as i64 + r3 as i64
}
fn bench_alu_tput_mixed() -> i64 {
    let (mut r0, mut r1, mut r2, mut r3) = (0i32, 0i32, 0i32, 0i32);
    let (r7, mut r6) = (1i32, 800_000i32);
    while r6 != 0 {
        r0 = r0.wrapping_add(r7);
        r1 = r1.wrapping_sub(r7);
        r2 &= r7;
        r3 |= r7;
        r6 -= 1;
    }
    r0 as i64 + r1 as i64 + r2 as i64 + r3 as i64
}
fn bench_memory() -> i64 {
    let mut arr = vec![0i32; 25_000];
    let mut total: i64 = 0;
    for _ in 0..20 {
        for i in 0..25_000 { arr[i] = i as i32; }
        let mut s: i64 = 0;
        for i in 0..25_000 { s += arr[i] as i64; }
        total += s;
    }
    total
}
fn bench_bit_ops() -> i64 {
    let (mut r0, r1, mut r2, mut r3, mut r4, mut r6) = (1i32, 1i32, 0i32, 0i32, 0i32, 3_000_000i32);
    while r6 != 0 {
        r0 >>= r1; r2 &= r0; r3 |= r0; r4 ^= r0;
        r6 -= 1;
    }
    r0 as i64 + r2 as i64 + r3 as i64 + r4 as i64
}
fn bench_mul() -> i64 {
    let (mut r0, r1, mut r6) = (1i32, 3i32, 3_000_000i32);
    while r6 != 0 { r0 = r0.wrapping_mul(r1); r6 -= 1; }
    r0 as i64
}
fn bench_branch() -> i64 {
    let (mut r5, mut r6) = (500_000i32, 0i32);
    let (mut r2, mut r3): (i64, i64) = (0, 0);
    while r5 != 0 {
        r6 += 1;
        if r6 & 1 != 0 { r2 += 1; } else { r3 += 1; }
        r5 -= 1;
    }
    r2 + r3
}
fn bench_div_mod() -> i64 {
    let (mut r0, r1, mut r6) = (1_000_000i32, 7i32, 500_000i32);
    let mut r2: i64 = 0;
    while r6 != 0 {
        r2 += (r0 / r1) as i64;
        r2 += (r0 % r1) as i64;
        r0 += 1;
        r6 -= 1;
    }
    r2
}
fn down(n: i32) -> i32 { if n <= 0 { 0 } else { down(n - 1) } }
fn bench_recursion() -> i64 {
    let mut total: i64 = 0;
    for _ in 0..500 { total += down(500) as i64; }
    total
}
fn bench_pointer() -> i64 {
    let mut arr = vec![0i32; 1000];
    let n = 1000usize;
    let p = arr.as_mut_ptr();
    unsafe {
        for i in 0..n { *p.add(i) = i as i32; }
        let mut s: i64 = 0;
        for _ in 0..2000 {
            for i in 0..n { s += *p.add(i) as i64; }
        }
        s
    }
}
#[derive(Clone, Copy)]
struct Point { x: i32, y: i32 }
fn bench_struct() -> i64 {
    let mut pts = vec![Point { x: 0, y: 0 }; 1000];
    let mut total: i64 = 0;
    for _ in 0..200 {
        for i in 0..1000 { pts[i].x = i as i32; pts[i].y = i as i32 + 1; }
        let mut s: i64 = 0;
        for i in 0..1000 { s += pts[i].x as i64 + pts[i].y as i64; }
        total += s;
    }
    total
}
fn bench_sort() -> i64 {
    let mut arr = [0i32; 200];
    let mut total: i64 = 0;
    for rep in (1..=50).rev() {
        for i in 0..200 { arr[i] = ((i as i32) * 37 + rep * 13) % 1000; }
        for pass in 0..199 {
            for j in 0..(199 - pass) {
                if arr[j] > arr[j + 1] { arr.swap(j, j + 1); }
            }
        }
        total += arr[0] as i64 + arr[199] as i64;
    }
    total
}

fn main() {
    let benches: [(&str, fn() -> i64); 14] = [
        ("Call", bench_call), ("Math", bench_math),
        ("AluTput1", bench_alu_tput1), ("AluTput4", bench_alu_tput4),
        ("AluTputMixed", bench_alu_tput_mixed), ("Memory", bench_memory),
        ("BitOps", bench_bit_ops), ("Mul", bench_mul),
        ("Branch", bench_branch), ("DivMod", bench_div_mod),
        ("Recursion", bench_recursion), ("Pointer", bench_pointer),
        ("Struct", bench_struct), ("Sort", bench_sort),
    ];
    for (name, f) in benches {
        f();                                              // warmup
        let t0 = Instant::now();
        let res = f();
        let dt = t0.elapsed().as_secs_f64();
        eprintln!("{:<12}  {:8.4} s  (result={})", name, dt, res);
    }
}