#!/usr/bin/env python3
"""Fail a .NET WebAssembly publish whose runtime JS and native module disagree.

Why this exists
---------------
`dotnet publish -p:WasmEnableThreads=<bool>` selects a *different runtime pack*:

    Microsoft.NETCore.App.Runtime.Mono.browser-wasm              (single-threaded)
    Microsoft.NETCore.App.Runtime.Mono.multithread.browser-wasm  (multithreaded)

`dotnet.runtime.js` is delivered verbatim from the resolved pack, so the two
flavours ship different JS.  The multithread one resolves extra native entry
points that only the multithread native module exports:

    mono_wasm_invoke_jsexport_async_post
    mono_wasm_invoke_jsexport_sync
    mono_wasm_invoke_jsexport_sync_send

A bundle that mixes the multithread `dotnet.runtime.js` with a single-threaded
`dotnet.native.wasm` dies during the Emscripten preRun hook with:

    cwrap mono_wasm_invoke_jsexport_async_post not found or not a function

That mix is what happens when two publishes with different `WasmEnableThreads`
values share one obj/bin tree: the flag is not part of the intermediate path, so
the second publish inherits the first publish's staged runtime JS.  This script
turns the silent mismatch into a build failure.

Signals used
------------
* `dotnet.native.<hash>.js` embeds a literal build flag:
      runAOTCompilation: true, wasmEnableThreads: false, ...
* `dotnet.runtime.<hash>.js` is minified, so its own `wasmEnableThreads` value is
  a variable reference and cannot be read statically.  Its thread flavour is
  detected from the thread-only entry points it resolves.
* `dotnet.native.<hash>.wasm` exposes its thread flavour through the export
  section (multithread exports the thread-only entry points).

Verdict: all available signals must agree, and must match --expect-threads.

Usage
-----
    assert-wasm-runtime-consistency.py <wwwroot/_framework dir> [--expect-threads true|false]
    assert-wasm-runtime-consistency.py --runtime R.js --wasm N.wasm [--native-js N.js]

Exit code 0 = consistent, 1 = inconsistent.
"""

import argparse
import glob
import os
import re
import sys

THREAD_ONLY = (
    "mono_wasm_invoke_jsexport_async_post",
    "mono_wasm_invoke_jsexport_sync",
    "mono_wasm_invoke_jsexport_sync_send",
)

SINGLE = "single-thread"
MULTI = "multithread"


def read_u32(data, offset):
    result = 0
    shift = 0
    while True:
        byte = data[offset]
        offset += 1
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result, offset
        shift += 7


def wasm_export_names(path):
    """Names listed in the wasm export section."""
    with open(path, "rb") as handle:
        data = handle.read()
    if data[:4] != b"\x00asm":
        raise SystemExit(f"ERROR: {path} is not a wasm module")
    offset = 8  # magic + version
    while offset < len(data):
        section_id = data[offset]
        offset += 1
        size, offset = read_u32(data, offset)
        end = offset + size
        if section_id == 7:  # export section
            count, offset = read_u32(data, offset)
            names = set()
            for _ in range(count):
                name_len, offset = read_u32(data, offset)
                names.add(data[offset:offset + name_len].decode("utf-8", "replace"))
                offset += name_len
                offset += 1  # kind
                _, offset = read_u32(data, offset)  # index
            return names
        offset = end
    raise SystemExit(f"ERROR: no export section found in {path}")


def text(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        return handle.read()


def runtime_required_symbols(runtime_js_path):
    """mono_wasm_* names the runtime JS resolves from the native module."""
    return set(re.findall(r'"(mono_wasm_[A-Za-z0-9_]+)"', text(runtime_js_path)))


def native_bound_symbols(native_js_path):
    """mono_wasm_* names the native module's JS glue binds to wasm exports."""
    pattern = r"Module\[['\"]_?(mono_wasm_[A-Za-z0-9_]+)['\"]\]\s*=\s*wasmExports"
    return set(re.findall(pattern, text(native_js_path)))


def declared_native_flag(native_js_path):
    """Literal `wasmEnableThreads: true|false` from the native module's JS glue."""
    values = set(re.findall(r"wasmEnableThreads:\s*(true|false)", text(native_js_path)))
    if not values:
        return None, []
    return values.pop() == "true", sorted(values)


def flavour_of(symbols):
    return MULTI if symbols else SINGLE


# A multithread bundle legitimately contains BOTH the content-hashed
# dotnet.native.<hash>.js and the unhashed dotnet.native.js published for the
# emscripten pthread worker.  Prefer the hashed one when both are present.
PLAIN_NAMES = {"dotnet.native.js", "dotnet.native.wasm", "dotnet.runtime.js"}


def single_match(pattern, label):
    hits = sorted(glob.glob(pattern))
    if not hits:
        raise SystemExit(f"ERROR: no {label} matching {pattern}")
    if len(hits) > 1:
        preferred = [hit for hit in hits if os.path.basename(hit) not in PLAIN_NAMES]
        if len(preferred) == 1:
            return preferred[0]
        raise SystemExit(f"ERROR: multiple {label} match {pattern}: {hits}")
    return hits[0]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("framework", nargs="?", help="path to wwwroot/_framework")
    parser.add_argument("--runtime", help="explicit dotnet.runtime.<hash>.js")
    parser.add_argument("--wasm", help="explicit dotnet.native.<hash>.wasm")
    parser.add_argument("--native-js", help="explicit dotnet.native.<hash>.js")
    parser.add_argument("--expect-threads", choices=["true", "false"], help="expected WasmEnableThreads value")
    args = parser.parse_args()

    if args.framework:
        runtime_js = single_match(os.path.join(args.framework, "dotnet.runtime*.js"), "dotnet.runtime*.js")
        native_wasm = single_match(os.path.join(args.framework, "dotnet.native*.wasm"), "dotnet.native*.wasm")
        native_js_hits = sorted(glob.glob(os.path.join(args.framework, "dotnet.native*.js")))
        native_js = native_js_hits[0] if native_js_hits else None
    else:
        if not (args.runtime and args.wasm):
            parser.error("give either a _framework directory or --runtime plus --wasm")
        runtime_js, native_wasm, native_js = args.runtime, args.wasm, args.native_js

    required = runtime_required_symbols(runtime_js)
    js_thread_only = sorted(required & set(THREAD_ONLY))
    runtime_flavour = flavour_of(js_thread_only)

    exported = wasm_export_names(native_wasm)
    wasm_thread_only = sorted(exported & set(THREAD_ONLY))
    wasm_flavour = flavour_of(wasm_thread_only)

    print(f"runtime JS : {os.path.basename(runtime_js)}")
    print(f"  flavour  : {runtime_flavour}")
    print(f"  resolves : {js_thread_only or 'no thread-only entry points'}")
    print(f"native wasm: {os.path.basename(native_wasm)} ({len(exported)} exports)")
    print(f"  flavour  : {wasm_flavour}")
    print(f"  exports  : {wasm_thread_only or 'no thread-only entry points'}")

    declared = None
    if native_js:
        declared, raw_values = declared_native_flag(native_js)
        bound = native_bound_symbols(native_js)
        shown = {True: "true", False: "false"}.get(declared, f"unreadable {raw_values}")
        print(f"native JS  : {os.path.basename(native_js)}")
        print(f"  binds    : {len(bound)} mono_wasm_* symbols")
        print(f"  wasmEnableThreads: {shown}")
        if len(raw_values) > 1:
            print(f"  WARNING: conflicting literal values in native JS: {raw_values}")

    # Emscripten creates pthread workers with
    # new Worker(new URL("dotnet.native.js", import.meta.url)) -- the unhashed name.
    # Without it a host that falls back to the SPA document answers with index.html,
    # every worker dies before initialising, and dotnet.create() never resolves.
    framework_dir = args.framework or os.path.dirname(runtime_js)
    worker_script = os.path.join(framework_dir, "dotnet.native.js")
    has_worker_script = os.path.isfile(worker_script)
    print(f"pthread worker: {os.path.basename(worker_script)} "
          f"{'present' if has_worker_script else 'MISSING'}")

    missing = sorted(sym for sym in required if sym not in exported)
    print(f"\nruntime JS resolves {len(required)} mono_wasm_* entry points; "
          f"{len(missing)} not exported by the native module:")
    for sym in missing:
        print(f"  - {sym}")

    failures = []
    declared_flavour = None if declared is None else (MULTI if declared else SINGLE)

    if runtime_flavour != wasm_flavour:
        failures.append(
            f"thread flavour mismatch: dotnet.runtime.js is {runtime_flavour} but "
            f"dotnet.native.wasm is {wasm_flavour}"
        )
    if declared_flavour and declared_flavour != runtime_flavour:
        failures.append(
            f"dotnet.native.js declares wasmEnableThreads={str(declared).lower()} ({declared_flavour}) "
            f"but dotnet.runtime.js is {runtime_flavour}"
        )
    if runtime_flavour == MULTI and not has_worker_script:
        failures.append(
            "multithread bundle is missing dotnet.native.js: emscripten spawns its workers from "
            'new Worker(new URL("dotnet.native.js", import.meta.url)), and hosts that answer unknown '
            "paths with the SPA document return index.html, so every worker dies and "
            "dotnet.create() never resolves (blank page, no error)"
        )
    if args.expect_threads:
        expected = MULTI if args.expect_threads == "true" else SINGLE
        detected = {runtime_flavour, wasm_flavour} | ({declared_flavour} if declared_flavour else set())
        if detected != {expected}:
            failures.append(
                f"requested WasmEnableThreads={args.expect_threads} ({expected}) but bundle signals are "
                f"runtime JS={runtime_flavour}, native wasm={wasm_flavour}"
                + (f", native JS={declared_flavour}" if declared_flavour else "")
            )

    if failures:
        print("\nFAIL:")
        for reason in failures:
            print(f"  - {reason}")
        print(
            "\nFix: publish each WasmEnableThreads flavour from its own clean intermediate "
            "and output tree, and publish the unhashed dotnet.native.js beside the hashed "
            "one so the emscripten pthread worker URL resolves."
        )
        return 1

    print(f"\nOK: consistent {runtime_flavour} bundle.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
