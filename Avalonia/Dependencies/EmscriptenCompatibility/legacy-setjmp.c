/*
 * Copyright 2020 The Emscripten Authors. All rights reserved.
 * Emscripten is available under the MIT license and the University of
 * Illinois/NCSA Open Source License. Both licenses are included in the
 * Emscripten distribution.
 *
 * This file retains the Emscripten 3.1.56 saveSetjmp/testSetjmp ABI for
 * SkiaSharp WebAssembly assets that still import those helpers. Emscripten 6
 * renamed the native SjLj entry points, so the compatibility functions are
 * linked into the application alongside the current runtime.
 */
#include <stdint.h>
#include <stdlib.h>

typedef struct LegacySetjmpTableEntry {
    uintptr_t id;
    uint32_t label;
} LegacySetjmpTableEntry;

extern void setTempRet0(uint32_t value);

static uintptr_t setjmpId;

LegacySetjmpTableEntry* saveSetjmp(
    uintptr_t* env,
    uint32_t label,
    LegacySetjmpTableEntry* table,
    uint32_t size) {
    // Keep the table terminated by a zero-id entry, as required by the
    // Emscripten 3.1.56 JavaScript SjLj helper.
    const uintptr_t id = ++setjmpId;
    *env = id;

    for (;;) {
        for (uint32_t i = 0; i < size; ++i) {
            if (table[i].id == 0) {
                table[i].id = id;
                table[i].label = label;
                table[i + 1].id = 0;
                setTempRet0(size);
                return table;
            }
        }

        size *= 2;
        table = realloc(table, sizeof(LegacySetjmpTableEntry) * (size + 1));
    }
}

uint32_t testSetjmp(
    uintptr_t id,
    const LegacySetjmpTableEntry* table,
    uint32_t size) {
    for (uint32_t i = 0; i < size; ++i) {
        const uintptr_t currentId = table[i].id;
        if (currentId == 0) {
            break;
        }

        if (currentId == id) {
            return table[i].label;
        }
    }

    return 0;
}
