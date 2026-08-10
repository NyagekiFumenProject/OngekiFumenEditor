const EntryKind = Object.freeze({
    missing: 0,
    file: 1,
    folder: 2,
});

const writeBuffers = new Map();
const logWriteBuffers = new Map();
let originRoot = null;
let temporaryRoot = null;
let logRoot = null;
let available = false;
let logAvailable = false;

function isNotFound(error) {
    return error instanceof DOMException && error.name === "NotFoundError";
}

function isTypeMismatch(error) {
    return error instanceof DOMException && error.name === "TypeMismatchError";
}

function isUnavailableInitializationError(error) {
    return error instanceof DOMException && [
        "SecurityError",
        "NotAllowedError",
        "NotSupportedError",
        "InvalidStateError",
    ].includes(error.name);
}

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

function requireAvailable() {
    if (!available || originRoot === null || temporaryRoot === null) {
        throw new DOMException("Origin-private temporary storage is unavailable.", "NotSupportedError");
    }
}

function requireLogAvailable() {
    if (!logAvailable || logRoot === null) {
        throw new DOMException("Origin-private log storage is unavailable.", "NotSupportedError");
    }
}

function validateLogFileName(fileName) {
    if (typeof fileName !== "string" || fileName.length === 0 || fileName === "." || fileName === ".." ||
        fileName.includes("\\") || fileName.includes("/")) {
        throw new TypeError(`Unsafe log file name: ${fileName}`);
    }
}

function splitPath(relativePath) {
    if (typeof relativePath !== "string") {
        throw new TypeError("A temporary relative path must be a string.");
    }

    if (relativePath.length === 0) {
        return [];
    }

    const segments = relativePath.split("/");
    for (const segment of segments) {
        if (segment.length === 0 || segment === "." || segment === ".." ||
            segment.includes("\\") || segment.includes("/")) {
            throw new TypeError(`Unsafe temporary relative path: ${relativePath}`);
        }
    }

    return segments;
}

async function resolveParent(relativePath) {
    requireAvailable();
    const segments = splitPath(relativePath);
    if (segments.length === 0) {
        throw new TypeError("The provider root does not have a parent entry.");
    }

    let parent = temporaryRoot;
    for (let index = 0; index < segments.length - 1; index++) {
        try {
            parent = await parent.getDirectoryHandle(segments[index]);
        } catch (error) {
            if (isNotFound(error)) {
                return null;
            }
            throw error;
        }
    }

    return { parent, name: segments[segments.length - 1] };
}

async function requireParent(relativePath) {
    const resolved = await resolveParent(relativePath);
    if (resolved === null) {
        throw new DOMException(`The parent of '${relativePath}' does not exist.`, "NotFoundError");
    }
    return resolved;
}

async function requireFile(relativePath) {
    const resolved = await requireParent(relativePath);
    return resolved.parent.getFileHandle(resolved.name);
}

async function abortSilently(writable) {
    try {
        if (typeof writable.abort === "function") {
            await writable.abort();
        }
    } catch {
        // Preserve the original write or close error.
    }
}

export async function initialize() {
    available = false;
    originRoot = null;
    temporaryRoot = null;
    logRoot = null;
    logAvailable = false;

    try {
        if (typeof navigator?.storage?.getDirectory !== "function") {
            return false;
        }

        originRoot = await navigator.storage.getDirectory();
    } catch (error) {
        originRoot = null;
        if (!isUnavailableInitializationError(error)) {
            throw error;
        }
        console.warn("OPFS storage is unavailable.", error);
        return false;
    }

    try {
        temporaryRoot = await originRoot.getDirectoryHandle("temp", { create: true });
        available = true;
    } catch (error) {
        temporaryRoot = null;
        console.warn("OPFS temporary storage is unavailable; temporary writes will be discarded.", error);
    }

    try {
        logRoot = await originRoot.getDirectoryHandle("logs", { create: true });
        logAvailable = true;
    } catch (error) {
        logRoot = null;
        console.warn("OPFS log storage is unavailable; file logs are disabled.", error);
    }

    return available || logAvailable;
}

export function isAvailable() {
    return available;
}

export function isLogAvailable() {
    return logAvailable;
}

export async function getEntryKind(relativePath) {
    const resolved = await resolveParent(relativePath);
    if (resolved === null) {
        return EntryKind.missing;
    }

    try {
        await resolved.parent.getFileHandle(resolved.name);
        return EntryKind.file;
    } catch (error) {
        if (!isNotFound(error) && !isTypeMismatch(error)) {
            throw error;
        }
    }

    try {
        await resolved.parent.getDirectoryHandle(resolved.name);
        return EntryKind.folder;
    } catch (error) {
        if (isNotFound(error) || isTypeMismatch(error)) {
            return EntryKind.missing;
        }
        throw error;
    }
}

export async function createFile(relativePath) {
    const resolved = await requireParent(relativePath);
    await resolved.parent.getFileHandle(resolved.name, { create: true });
}

export async function tryCreateFile(relativePath) {
    if (await getEntryKind(relativePath) !== EntryKind.missing) {
        return false;
    }
    await createFile(relativePath);
    return true;
}

export async function createFolder(relativePath) {
    const resolved = await requireParent(relativePath);
    await resolved.parent.getDirectoryHandle(resolved.name, { create: true });
}

export async function tryCreateFolder(relativePath) {
    if (await getEntryKind(relativePath) !== EntryKind.missing) {
        return false;
    }
    await createFolder(relativePath);
    return true;
}

export async function getFileLength(relativePath) {
    const handle = await requireFile(relativePath);
    return (await handle.getFile()).size;
}

export async function readFile(relativePath) {
    const handle = await requireFile(relativePath);
    const data = new Uint8Array(await (await handle.getFile()).arrayBuffer());
    return { data };
}

export function setWriteBuffer(handle, data, byteLength) {
    const bytes = new Uint8Array(byteLength);
    copyFromMemoryView(data, bytes);
    writeBuffers.set(handle, bytes);
}

export function releaseWriteBuffer(handle) {
    writeBuffers.delete(handle);
}

function requireWriteBuffer(handle) {
    const bytes = writeBuffers.get(handle);
    if (bytes === undefined) {
        throw new Error(`Unknown temporary write buffer ${handle}.`);
    }
    return bytes;
}

function setLogWriteBuffer(handle, data, byteLength) {
    const bytes = new Uint8Array(byteLength);
    copyFromMemoryView(data, bytes);
    logWriteBuffers.set(handle, bytes);
}

function releaseLogWriteBuffer(handle) {
    logWriteBuffers.delete(handle);
}

function requireLogWriteBuffer(handle) {
    const bytes = logWriteBuffers.get(handle);
    if (bytes === undefined) {
        throw new Error(`Unknown log write buffer ${handle}.`);
    }
    return bytes;
}

export async function writeFile(relativePath, handle) {
    const bytes = requireWriteBuffer(handle);
    const resolved = await requireParent(relativePath);
    const fileHandle = await resolved.parent.getFileHandle(resolved.name, { create: true });
    const writable = await fileHandle.createWritable();
    let committed = false;
    try {
        await writable.write(bytes);
        await writable.close();
        committed = true;
    } finally {
        if (!committed) {
            await abortSilently(writable);
        }
    }
}

export async function appendFile(relativePath, handle) {
    const bytes = requireWriteBuffer(handle);
    const fileHandle = await requireFile(relativePath);
    const original = await fileHandle.getFile();
    const writable = await fileHandle.createWritable({ keepExistingData: true });
    let committed = false;
    try {
        await writable.seek(original.size);
        await writable.write(bytes);
        await writable.close();
        committed = true;
    } finally {
        if (!committed) {
            await abortSilently(writable);
        }
    }
}

export async function tryCreateLogFile(fileName) {
    requireLogAvailable();
    validateLogFileName(fileName);
    try {
        await logRoot.getFileHandle(fileName);
        return false;
    } catch (error) {
        if (!isNotFound(error)) {
            throw error;
        }
    }

    await logRoot.getFileHandle(fileName, { create: true });
    return true;
}

export async function appendLogFile(fileName, handle) {
    requireLogAvailable();
    validateLogFileName(fileName);
    const bytes = requireLogWriteBuffer(handle);
    const fileHandle = await logRoot.getFileHandle(fileName);
    const original = await fileHandle.getFile();
    const writable = await fileHandle.createWritable({ keepExistingData: true });
    let committed = false;
    try {
        await writable.seek(original.size);
        await writable.write(bytes);
        await writable.close();
        committed = true;
    } finally {
        if (!committed) {
            await abortSilently(writable);
        }
    }
}

export async function deleteFile(relativePath) {
    const resolved = await resolveParent(relativePath);
    if (resolved === null) {
        return;
    }

    try {
        await resolved.parent.getFileHandle(resolved.name);
    } catch (error) {
        if (isNotFound(error)) {
            return;
        }
        throw error;
    }

    await resolved.parent.removeEntry(resolved.name);
}

export async function deleteFolder(relativePath) {
    requireAvailable();
    if (relativePath.length === 0) {
        await clear();
        return;
    }

    const resolved = await resolveParent(relativePath);
    if (resolved === null) {
        return;
    }

    try {
        await resolved.parent.getDirectoryHandle(resolved.name);
    } catch (error) {
        if (isNotFound(error)) {
            return;
        }
        throw error;
    }

    await resolved.parent.removeEntry(resolved.name, { recursive: true });
}

export async function clear() {
    requireAvailable();
    for await (const [name] of temporaryRoot.entries()) {
        await temporaryRoot.removeEntry(name, { recursive: true });
    }
}

globalThis.TemporaryFileSystemInterop = Object.freeze({
    initialize,
    isAvailable,
    getEntryKind,
    createFile,
    tryCreateFile,
    createFolder,
    tryCreateFolder,
    getFileLength,
    readFile,
    setWriteBuffer,
    releaseWriteBuffer,
    writeFile,
    appendFile,
    deleteFile,
    deleteFolder,
    clear,
});

globalThis.LogFileSystemInterop = Object.freeze({
    isAvailable: isLogAvailable,
    tryCreateFile: tryCreateLogFile,
    setWriteBuffer: setLogWriteBuffer,
    releaseWriteBuffer: releaseLogWriteBuffer,
    appendFile: appendLogFile,
});
