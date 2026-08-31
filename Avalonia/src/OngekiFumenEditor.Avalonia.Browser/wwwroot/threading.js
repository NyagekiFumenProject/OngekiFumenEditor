"use strict";

const headerState = {
    coop: null,
    coep: null,
};

const coopEnabledValues = new Set(["same-origin"]);
const coepEnabledValues = new Set(["require-corp", "credentialless"]);

function parseHeaderState(value, enabledValues) {
    if (value === null || value === undefined) {
        return false;
    }

    const values = String(value)
        .toLowerCase()
        .split(",")
        .map(item => item.split(";", 1)[0].trim());
    return values.some(item => enabledValues.has(item));
}

function applyCrossOriginIsolationFallback() {
    if (typeof globalThis.crossOriginIsolated !== "boolean") {
        return;
    }

    // crossOriginIsolated is the browser's combined result for COOP and COEP.
    // Use it only when the response headers themselves could not be inspected.
    if (headerState.coop === null) {
        headerState.coop = globalThis.crossOriginIsolated;
    }
    if (headerState.coep === null) {
        headerState.coep = globalThis.crossOriginIsolated;
    }
}

async function probeResponseHeaders() {
    if (typeof globalThis.fetch !== "function" || !globalThis.location?.href) {
        applyCrossOriginIsolationFallback();
        return;
    }

    let controller = null;
    let timeoutHandle = null;
    try {
        controller = typeof AbortController === "function" ? new AbortController() : null;
        if (controller) {
            timeoutHandle = setTimeout(() => controller.abort(), 1000);
        }

        let response = await fetch(globalThis.location.href, {
            method: "HEAD",
            cache: "no-store",
            credentials: "same-origin",
            signal: controller?.signal,
        });

        // A few static hosts reject HEAD while serving GET normally.
        if (!response.ok && (response.status === 405 || response.status === 501)) {
            response = await fetch(globalThis.location.href, {
                method: "GET",
                cache: "no-store",
                credentials: "same-origin",
                signal: controller?.signal,
            });
        }

        if (!response.ok) {
            throw new Error(`Unable to inspect the document response (${response.status}).`);
        }

        headerState.coop = parseHeaderState(
            response.headers.get("Cross-Origin-Opener-Policy"),
            coopEnabledValues);
        headerState.coep = parseHeaderState(
            response.headers.get("Cross-Origin-Embedder-Policy"),
            coepEnabledValues);
    } catch {
        // Diagnostics must never prevent the application from starting.
    } finally {
        if (timeoutHandle !== null) {
            clearTimeout(timeoutHandle);
        }
    }

    applyCrossOriginIsolationFallback();
}

let initialization = probeResponseHeaders();

export function initialize() {
    return initialization;
}

function toState(value) {
    return value === null ? -1 : value ? 1 : 0;
}

function getWasmEnableThreadsState() {
    const value = globalThis.__ongekiWasmEnableThreads;
    return typeof value === "boolean" ? (value ? 1 : 0) : -1;
}

globalThis.BrowserThreadingInterop = Object.freeze({
    getCoopHeaderState: () => toState(headerState.coop),
    getCoepHeaderState: () => toState(headerState.coep),
    getSharedArrayBufferState: () => {
        if (typeof globalThis.SharedArrayBuffer !== "function") {
            return 0;
        }

        try {
            // Constructing one verifies that the current document may actually use it.
            new globalThis.SharedArrayBuffer(1);
            return 1;
        } catch {
            return 0;
        }
    },
    getWasmEnableThreadsState,
});
