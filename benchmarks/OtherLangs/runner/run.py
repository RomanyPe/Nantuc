#!/usr/bin/env python3
"""
Runner for OtherLangs benchmarks (scripting languages only).

Reads ../langs.json, runs each available interpreter against its benchmark
script, parses output of the form

    <Name>  <seconds> s  (result=<N>)

and writes results.json + results.csv + prints a summary pivot table.

Usage:
    python run.py
    python run.py --langs python lua --cases Call Math --repeat 3 -v
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

RESULT_RE = re.compile(
    r'^(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s+'
    r'(?P<time>\d+(?:\.\d+)?)\s*s\s+.*result=(?P<result>-?\d+)'
)

VERSION_FLAGS = {
    "python":  ["--version"],
    "python3": ["--version"],
    "py":      ["--version"],
    "lua":     ["-v"],
    "luajit":  ["-v"],
    "ruby":    ["--version"],
    "node":    ["--version"],
}


def interpreter_version(exe: str) -> str | None:
    flags = VERSION_FLAGS.get(exe)
    if not flags:
        return None
    try:
        r = subprocess.run(
            [exe] + flags, capture_output=True, text=True, timeout=5
        )
        text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
        return text.splitlines()[0][:120] if text else None
    except Exception:
        return None


def run_once(cmd: list[str], cwd: Path, timeout: int):
    t0 = time.perf_counter()
    try:
        r = subprocess.run(
            cmd, cwd=cwd, capture_output=True, text=True,
            timeout=timeout, errors="replace",
        )
    except subprocess.TimeoutExpired:
        return None
    except FileNotFoundError:
        return None
    wall = time.perf_counter() - t0
    return r, wall


def parse(text: str) -> list[dict]:
    out = []
    for line in text.splitlines():
        m = RESULT_RE.search(line)
        if m:
            out.append({
                "case":    m.group("name"),
                "seconds": float(m.group("time")),
                "result":  int(m.group("result")),
            })
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description="Run OtherLangs benchmarks")
    ap.add_argument("--langs",  nargs="*", default=[], help="filter by language id")
    ap.add_argument("--cases",  nargs="*", default=[], help="filter by case name")
    ap.add_argument("-o", "--output", default="results.json")
    ap.add_argument("--timeout", type=int, default=300)
    ap.add_argument("--repeat",  type=int, default=1)
    ap.add_argument("-v", "--verbose", action="store_true")
    args = ap.parse_args()

    script_dir = Path(__file__).resolve().parent
    root = script_dir.parent
    langs_file = root / "langs.json"
    if not langs_file.exists():
        print(f"ERROR: {langs_file} not found", file=sys.stderr)
        return 1

    config = json.loads(langs_file.read_text(encoding="utf-8"))
    langs = config.get("languages", [])
    if args.langs:
        langs = [l for l in langs if l["id"] in args.langs]

    all_results: list[dict] = []

    for lang in langs:
        cmd = lang["run"]
        exe = cmd[0]
        cwd = root / lang["cwd"]

        print(f"\n=== {lang['name']} [{lang['id']}] ===")
        print(f"    cmd: {' '.join(cmd)}")
        print(f"    cwd: {cwd}")

        if not shutil.which(exe):
            print(f"    WARNING: '{exe}' not found in PATH — skipping",
                  file=sys.stderr)
            continue

        ver = interpreter_version(exe)
        if ver:
            print(f"    version: {ver}")

        best: dict[str, dict] = {}
        for i in range(args.repeat):
            if args.repeat > 1:
                print(f"    run {i + 1}/{args.repeat} ...")
            res = run_once(cmd, cwd, args.timeout)
            if res is None:
                print(f"    WARNING: timed out after {args.timeout}s or exe "
                      f"disappeared — skipping", file=sys.stderr)
                best = {}
                break
            r, wall = res
            if args.verbose:
                print(f"    exit={r.returncode}, wall={wall:.2f}s")
            parsed = parse((r.stderr or "") + "\n" + (r.stdout or ""))
            if not parsed:
                print(f"    WARNING: no results parsed (exit={r.returncode})",
                      file=sys.stderr)
                if args.verbose and r.stderr:
                    print("    --- raw stderr ---")
                    print(r.stderr)
                best = {}
                break
            for p in parsed:
                cur = best.get(p["case"])
                if cur is None or p["seconds"] < cur["seconds"]:
                    best[p["case"]] = p

        if not best:
            continue

        cases_in_order = sorted(best.values(), key=lambda x: -x["seconds"])
        for p in cases_in_order:
            if args.cases and p["case"] not in args.cases:
                continue
            all_results.append({
                "lang":     lang["id"],
                "language": lang["name"],
                "category": lang["category"],
                "case":     p["case"],
                "seconds":  round(p["seconds"], 6),
                "result":   p["result"],
            })

        print(f"    parsed {len(best)} cases")
        for p in cases_in_order:
            print(f"      {p['case']:<12}  {p['seconds']:10.4f} s  "
                  f"result={p['result']}")

    if not all_results:
        print("\nERROR: no results collected.", file=sys.stderr)
        return 2

    out = Path(args.output)
    out.write_text(json.dumps(all_results, indent=2, ensure_ascii=False),
                   encoding="utf-8")
    csv_path = out.with_suffix(".csv")
    with csv_path.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=[
            "lang", "language", "category", "case", "seconds", "result"
        ])
        w.writeheader()
        w.writerows(all_results)
    print(f"\nSaved {len(all_results)} results → {out}, {csv_path}")

    # ── pivot summary ─────────────────────────────────────────────────────
    print("\n=== Summary (seconds per case, min across runs) ===")
    cases = sorted({r["case"] for r in all_results})
    lang_ids = sorted({r["lang"] for r in all_results})
    header = f"{'Case':<14}" + "".join(f"{l:>15}" for l in lang_ids)
    print(header)
    print("-" * len(header))
    for c in cases:
        row = f"{c:<14}"
        for l in lang_ids:
            v = next(
                (r["seconds"] for r in all_results
                 if r["case"] == c and r["lang"] == l),
                None,
            )
            row += f"{v:>15.4f}" if v is not None else f"{'-':>15}"
        print(row)
    return 0


if __name__ == "__main__":
    sys.exit(main())