import {
    getOrCreateRootDirectory,
    isAvailable as isOpfsAvailable,
    validateEntryName,
} from './opfs.js';

const writeBuffers = new Map();
let logFileSystem = null;

function copyFromMemoryView(source, destination) {
    if (typeof source.copyTo === "function") {
        source.copyTo(destination);
    } else if (typeof destination.set === "function") {
        destination.set(source);
    } else {
        for (let index = 0; index < destination.length; index++) {
            destination[index] = source[index];
        }
    }
}

function requireLogFileSystem() {
    if (logFileSystem === null) {
        throw new DOMException("Origin-private log storage is unavailable.", "NotSupportedError");
    }
    return logFileSystem;
}

function requireWriteBuffer(handle) {
    const bytes = writeBuffers.get(handle);
    if (bytes === undefined) {
        throw new Error(`Unknown log write buffer ${handle}.`);
    }
    return bytes;
}

export async function initialize() {
    logFileSystem = null;
    if (!isOpfsAvailable()) {
        return false;
    }

    try {
        logFileSystem = await getOrCreateRootDirectory("logs");
        return true;
    } catch (error) {
        logFileSystem = null;
        console.warn("OPFS log storage is unavailable; file logs are disabled.", error);
        return false;
    }
}

export function isAvailable() {
    return logFileSystem !== null;
}

export async function tryCreateFile(fileName) {
    validateEntryName(fileName);
    return requireLogFileSystem().tryCreateFile(fileName);
}

export function setWriteBuffer(handle, data, byteLength) {
    const bytes = new Uint8Array(byteLength);
    copyFromMemoryView(data, bytes);
    writeBuffers.set(handle, bytes);
}

export function releaseWriteBuffer(handle) {
    writeBuffers.delete(handle);
}

export async function appendFile(fileName, handle) {
    validateEntryName(fileName);
    const bytes = requireWriteBuffer(handle);
    await requireLogFileSystem().appendFile(fileName, bytes);
}

globalThis.LogFileSystemInterop = Object.freeze({
    isAvailable,
    tryCreateFile,
    setWriteBuffer,
    releaseWriteBuffer,
    appendFile,
});
