import {
    getOriginRootHandle,
    isAvailable as isOpfsAvailable,
    validateEntryName,
} from './opfs.js';

const EntryKind = Object.freeze({
    file: 1,
    folder: 2,
});

const StagingState = Object.freeze({
    none: 0,
    generatingDownload: 1,
    waitingAutomaticCleanup: 2,
});

const stagingDirectoryName = ".ongeki-opfs-downloads";
const writeBuffers = new Map();
const readHandles = new Map();
const outputHandles = new Map();
const stagingStates = new Map();
const retainedObjectUrls = new Set();
const textPreviewMimeType = "text/plain;charset=utf-8";
const previewMimeTypesByExtension = Object.freeze({
    ".avif": "image/avif",
    ".bmp": "image/bmp",
    ".gif": "image/gif",
    ".ico": "image/x-icon",
    ".jpeg": "image/jpeg",
    ".jpg": "image/jpeg",
    ".png": "image/png",
    ".webp": "image/webp",
    ".flac": "audio/flac",
    ".m4a": "audio/mp4",
    ".mp3": "audio/mpeg",
    ".oga": "audio/ogg",
    ".ogg": "audio/ogg",
    ".wav": "audio/wav",
    ".mp4": "video/mp4",
    ".ogv": "video/ogg",
    ".webm": "video/webm",
    ".pdf": "application/pdf",
    ".axaml": textPreviewMimeType,
    ".cfg": textPreviewMimeType,
    ".cjs": textPreviewMimeType,
    ".conf": textPreviewMimeType,
    ".cs": textPreviewMimeType,
    ".csproj": textPreviewMimeType,
    ".css": textPreviewMimeType,
    ".csv": textPreviewMimeType,
    ".htm": textPreviewMimeType,
    ".html": textPreviewMimeType,
    ".ini": textPreviewMimeType,
    ".js": textPreviewMimeType,
    ".json": textPreviewMimeType,
    ".jsonc": textPreviewMimeType,
    ".jsx": textPreviewMimeType,
    ".log": textPreviewMimeType,
    ".m4s": textPreviewMimeType,
    ".ma2": textPreviewMimeType,
    ".maidata": textPreviewMimeType,
    ".markdown": textPreviewMimeType,
    ".md": textPreviewMimeType,
    ".mjs": textPreviewMimeType,
    ".nyageki": textPreviewMimeType,
    ".ogkr": textPreviewMimeType,
    ".props": textPreviewMimeType,
    ".simai": textPreviewMimeType,
    ".sln": textPreviewMimeType,
    ".slnx": textPreviewMimeType,
    ".targets": textPreviewMimeType,
    ".toml": textPreviewMimeType,
    ".ts": textPreviewMimeType,
    ".tsv": textPreviewMimeType,
    ".tsx": textPreviewMimeType,
    ".txt": textPreviewMimeType,
    ".xaml": textPreviewMimeType,
    ".xhtml": textPreviewMimeType,
    ".xml": textPreviewMimeType,
    ".yaml": textPreviewMimeType,
    ".yml": textPreviewMimeType,
});
let nextReadHandle = 0;
let nextOutputHandle = 0;
let browserAvailable = false;

function isNamedDomException(error, name) {
    return typeof DOMException !== "undefined" &&
        error instanceof DOMException &&
        error.name === name;
}

function isNotFound(error) {
    return isNamedDomException(error, "NotFoundError");
}

function isTypeMismatch(error) {
    return isNamedDomException(error, "TypeMismatchError");
}

function normalizePath(relativePath) {
    if (typeof relativePath !== "string") {
        throw new TypeError("An OPFS relative path must be a string.");
    }

    if (relativePath.length === 0) {
        return "";
    }

    if (relativePath.startsWith("/") || relativePath.endsWith("/") || relativePath.includes("\\")) {
        throw new TypeError(`Unsafe OPFS relative path: ${relativePath}`);
    }

    const segments = relativePath.split("/");
    for (const segment of segments) {
        validateEntryName(segment);
    }
    return segments.join("/");
}

function joinPath(parentPath, entryName) {
    validateEntryName(entryName);
    return parentPath.length === 0 ? entryName : `${parentPath}/${entryName}`;
}

function getPathDepth(relativePath) {
    return relativePath.length === 0 ? 0 : relativePath.split("/").length;
}

function isDescendantPath(relativePath, parentPath) {
    return parentPath.length === 0
        ? relativePath.length > 0
        : relativePath.length > parentPath.length &&
          relativePath.startsWith(parentPath) &&
          relativePath[parentPath.length] === "/";
}

function compareEntryNames(left, right) {
    return left.localeCompare(right, undefined, {
        numeric: true,
        sensitivity: "base",
    });
}

function requireBrowserAvailable() {
    if (!browserAvailable || !isOpfsAvailable()) {
        throw new DOMException("Origin-private file system browsing is unavailable.", "NotSupportedError");
    }
}

async function resolveDirectory(relativePath) {
    requireBrowserAvailable();
    const normalizedPath = normalizePath(relativePath);
    let directory = getOriginRootHandle();
    if (normalizedPath.length === 0) {
        return directory;
    }

    for (const segment of normalizedPath.split("/")) {
        directory = await directory.getDirectoryHandle(segment);
    }
    return directory;
}

async function resolveParent(relativePath) {
    const normalizedPath = normalizePath(relativePath);
    const separatorIndex = normalizedPath.lastIndexOf("/");
    const parentPath = separatorIndex < 0 ? "" : normalizedPath.slice(0, separatorIndex);
    const name = separatorIndex < 0 ? normalizedPath : normalizedPath.slice(separatorIndex + 1);
    if (name.length === 0) {
        throw new TypeError("The OPFS root does not have a parent entry.");
    }
    return {
        parent: await resolveDirectory(parentPath),
        name,
    };
}

async function resolveFile(relativePath) {
    const resolved = await resolveParent(relativePath);
    return resolved.parent.getFileHandle(resolved.name);
}

function getPreviewMimeType(relativePath, fileMimeType) {
    const fileName = relativePath.slice(relativePath.lastIndexOf("/") + 1);
    const extensionIndex = fileName.lastIndexOf(".");
    if (extensionIndex >= 0) {
        const extension = fileName.slice(extensionIndex).toLowerCase();
        const mappedMimeType = previewMimeTypesByExtension[extension];
        if (mappedMimeType !== undefined) {
            return mappedMimeType;
        }
    }

    const normalizedMimeType = fileMimeType.split(";", 1)[0].trim().toLowerCase();
    if (normalizedMimeType.startsWith("text/") ||
        normalizedMimeType === "application/json" ||
        normalizedMimeType.endsWith("+json") ||
        normalizedMimeType === "application/javascript" ||
        normalizedMimeType.endsWith("+xml") ||
        normalizedMimeType === "application/xml" ||
        normalizedMimeType === "image/svg+xml") {
        return textPreviewMimeType;
    }
    if (normalizedMimeType.startsWith("image/") ||
        normalizedMimeType.startsWith("audio/") ||
        normalizedMimeType.startsWith("video/") ||
        normalizedMimeType === "application/pdf") {
        return normalizedMimeType;
    }
    return textPreviewMimeType;
}

function showFilePreviewMessage(previewWindow, relativePath, message, isError = false) {
    if (previewWindow.closed) {
        return;
    }

    const fileName = relativePath.slice(relativePath.lastIndexOf("/") + 1);
    const previewDocument = previewWindow.document;
    previewDocument.title = fileName;
    previewDocument.documentElement.style.colorScheme = "light dark";
    previewDocument.body.replaceChildren();
    Object.assign(previewDocument.body.style, {
        alignItems: "center",
        background: "Canvas",
        color: isError ? "#c42b1c" : "CanvasText",
        display: "flex",
        fontFamily: "system-ui, sans-serif",
        justifyContent: "center",
        margin: "0",
        minHeight: "100vh",
        padding: "24px",
        textAlign: "center",
    });

    const status = previewDocument.createElement("div");
    status.style.whiteSpace = "pre-wrap";
    status.textContent = message;
    previewDocument.body.append(status);
}

async function loadFilePreview(previewWindow, relativePath) {
    let objectUrl;
    try {
        const fileHandle = await resolveFile(relativePath);
        const file = await fileHandle.getFile();
        if (previewWindow.closed) {
            return;
        }

        const previewMimeType = getPreviewMimeType(relativePath, file.type);
        const previewContent = file.type === previewMimeType
            ? file
            : new Blob([file], { type: previewMimeType });
        objectUrl = URL.createObjectURL(previewContent);
        retainedObjectUrls.add(objectUrl);
        previewWindow.location.replace(objectUrl);
    } catch (error) {
        if (objectUrl !== undefined) {
            retainedObjectUrls.delete(objectUrl);
            URL.revokeObjectURL(objectUrl);
        }
        const detail = error instanceof Error ? error.message : String(error);
        try {
            showFilePreviewMessage(
                previewWindow,
                relativePath,
                `Unable to open this OPFS file.\n${detail}`,
                true);
        } catch {
            // The preview page may have been closed while the OPFS file was being read.
        }
    }
}

async function getSortedEntries(directory) {
    const entries = [];
    for await (const entry of directory.entries()) {
        entries.push(entry);
    }
    entries.sort((left, right) => compareEntryNames(left[0], right[0]));
    return entries;
}

function getStagingState(relativePath) {
    const directState = stagingStates.get(relativePath);
    if (directState !== undefined) {
        return directState;
    }

    if (relativePath !== stagingDirectoryName) {
        return StagingState.none;
    }

    let hasWaitingEntry = false;
    for (const state of stagingStates.values()) {
        if (state === StagingState.generatingDownload) {
            return StagingState.generatingDownload;
        }
        if (state === StagingState.waitingAutomaticCleanup) {
            hasWaitingEntry = true;
        }
    }
    return hasWaitingEntry ? StagingState.waitingAutomaticCleanup : StagingState.none;
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

function requireWriteBuffer(handle) {
    const bytes = writeBuffers.get(handle);
    if (bytes === undefined) {
        throw new Error(`Unknown OPFS browser write buffer ${handle}.`);
    }
    return bytes;
}

async function abortWritableSilently(writable) {
    try {
        if (typeof writable.abort === "function") {
            await writable.abort();
        }
    } catch {
        // Preserve the original OPFS write error.
    }
}

function requireReadHandle(handle) {
    const state = readHandles.get(handle);
    if (state === undefined) {
        throw new Error(`Unknown OPFS browser read handle ${handle}.`);
    }
    return state;
}

function requireOutputHandle(handle) {
    const state = outputHandles.get(handle);
    if (state === undefined) {
        throw new Error(`Unknown OPFS browser output handle ${handle}.`);
    }
    return state;
}

function sanitizeSuggestedName(fileName) {
    if (typeof fileName !== "string" || fileName.trim().length === 0) {
        return "download";
    }
    const sanitized = fileName
        .replace(/[<>:"/\\|?*\u0000-\u001f]/g, "_")
        .replace(/[ .]+$/g, "");
    return sanitized.length === 0 ? "download" : sanitized;
}

function appendNumericSuffix(fileName, suffix) {
    const dotIndex = fileName.lastIndexOf(".");
    return dotIndex <= 0
        ? `${fileName} (${suffix})`
        : `${fileName.slice(0, dotIndex)} (${suffix})${fileName.slice(dotIndex)}`;
}

async function entryExists(directory, entryName) {
    try {
        await directory.getFileHandle(entryName);
        return true;
    } catch (error) {
        if (!isNotFound(error) && !isTypeMismatch(error)) {
            throw error;
        }
    }

    try {
        await directory.getDirectoryHandle(entryName);
        return true;
    } catch (error) {
        if (isNotFound(error) || isTypeMismatch(error)) {
            return false;
        }
        throw error;
    }
}

async function createUniqueStagingFile(suggestedName) {
    const root = getOriginRootHandle();
    const directory = await root.getDirectoryHandle(stagingDirectoryName, { create: true });
    const baseName = sanitizeSuggestedName(suggestedName);
    let candidate = baseName;
    let suffix = 2;
    while (await entryExists(directory, candidate)) {
        candidate = appendNumericSuffix(baseName, suffix++);
    }

    const fileHandle = await directory.getFileHandle(candidate, { create: true });
    return {
        fileHandle,
        relativePath: joinPath(stagingDirectoryName, candidate),
    };
}

async function removeStagingFile(relativePath) {
    stagingStates.delete(relativePath);
    try {
        const resolved = await resolveParent(relativePath);
        await resolved.parent.removeEntry(resolved.name);
    } catch (error) {
        if (!isNotFound(error)) {
            console.warn(`Failed to remove OPFS staging file '${relativePath}'.`, error);
        }
    }

    try {
        const root = getOriginRootHandle();
        const directory = await root.getDirectoryHandle(stagingDirectoryName);
        for await (const _ of directory.entries()) {
            return;
        }
        await root.removeEntry(stagingDirectoryName);
    } catch (error) {
        if (!isNotFound(error)) {
            console.warn("Failed to remove the empty OPFS staging directory.", error);
        }
    }
}

function enqueueOutputWrite(output, bytes) {
    output.pending = output.pending.then(() => output.writable.write(bytes));
    return output.pending;
}

async function triggerOrdinaryDownload(output) {
    const file = await output.fileHandle.getFile();
    const objectUrl = URL.createObjectURL(file);
    retainedObjectUrls.add(objectUrl);

    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = output.suggestedName;
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
}

async function beginStagingDownload(suggestedName, mimeType) {
    const staging = await createUniqueStagingFile(suggestedName);
    let writable;
    try {
        writable = await staging.fileHandle.createWritable();
    } catch (error) {
        await removeStagingFile(staging.relativePath);
        throw error;
    }
    const handle = ++nextOutputHandle;
    stagingStates.set(staging.relativePath, StagingState.generatingDownload);
    outputHandles.set(handle, {
        mode: "staging",
        writable,
        pending: Promise.resolve(),
        suggestedName,
        mimeType,
        fileHandle: staging.fileHandle,
        stagingPath: staging.relativePath,
    });
    return JSON.stringify({
        handle,
        canceled: false,
        mode: "staging",
        stagingPath: staging.relativePath,
    });
}

async function addManifestDirectory(relativePath, manifest, visitedPaths) {
    const normalizedPath = normalizePath(relativePath);
    if (visitedPaths.has(normalizedPath)) {
        return;
    }
    visitedPaths.add(normalizedPath);

    const directory = await resolveDirectory(normalizedPath);
    manifest.entries.push({
        path: normalizedPath,
        kind: EntryKind.folder,
        size: null,
        lastModified: null,
    });

    for (const [name, handle] of await getSortedEntries(directory)) {
        const childPath = joinPath(normalizedPath, name);
        if (handle.kind === "directory") {
            await addManifestDirectory(childPath, manifest, visitedPaths);
            continue;
        }

        const file = await handle.getFile();
        manifest.entries.push({
            path: childPath,
            kind: EntryKind.file,
            size: file.size,
            lastModified: file.lastModified,
        });
        manifest.totalBytes += file.size;
        manifest.totalFiles++;
        visitedPaths.add(childPath);
    }
}

async function addManifestFile(relativePath, manifest, visitedPaths) {
    const normalizedPath = normalizePath(relativePath);
    if (visitedPaths.has(normalizedPath)) {
        return;
    }

    const fileHandle = await resolveFile(normalizedPath);
    const file = await fileHandle.getFile();
    manifest.entries.push({
        path: normalizedPath,
        kind: EntryKind.file,
        size: file.size,
        lastModified: file.lastModified,
    });
    manifest.totalBytes += file.size;
    manifest.totalFiles++;
    visitedPaths.add(normalizedPath);
}

export async function initialize() {
    browserAvailable = false;
    stagingStates.clear();

    if (!isOpfsAvailable()) {
        return false;
    }

    try {
        const root = getOriginRootHandle();
        try {
            await root.removeEntry(stagingDirectoryName, { recursive: true });
        } catch (error) {
            if (!isNotFound(error)) {
                console.warn("Failed to clean stale OPFS download staging entries.", error);
            }
        }
        browserAvailable = true;
        return true;
    } catch (error) {
        console.warn("OPFS browser initialization failed.", error);
        return false;
    }
}

export function isAvailable() {
    return browserAvailable && isOpfsAvailable();
}

export async function listDirectory(relativePath) {
    const normalizedPath = normalizePath(relativePath);
    const directory = await resolveDirectory(normalizedPath);
    const result = [];

    for (const [name, handle] of await getSortedEntries(directory)) {
        const childPath = joinPath(normalizedPath, name);
        if (handle.kind === "directory") {
            result.push({
                name,
                relativePath: childPath,
                kind: EntryKind.folder,
                size: null,
                lastModified: null,
                stagingState: getStagingState(childPath),
            });
            continue;
        }

        const file = await handle.getFile();
        result.push({
            name,
            relativePath: childPath,
            kind: EntryKind.file,
            size: file.size,
            lastModified: file.lastModified,
            stagingState: getStagingState(childPath),
        });
    }

    return JSON.stringify(result);
}

export async function directoryExists(relativePath) {
    try {
        await resolveDirectory(relativePath);
        return true;
    } catch (error) {
        if (isNotFound(error) || isTypeMismatch(error)) {
            return false;
        }
        throw error;
    }
}

export function openFilePreview(relativePath) {
    requireBrowserAvailable();
    const normalizedPath = normalizePath(relativePath);
    if (normalizedPath.length === 0 || typeof globalThis.open !== "function") {
        return false;
    }

    let previewWindow;
    try {
        // Open synchronously while the double-tap still counts as a browser user gesture.
        previewWindow = globalThis.open("", "_blank");
    } catch {
        return false;
    }
    if (previewWindow === null) {
        return false;
    }

    try {
        previewWindow.opener = null;
        const fileName = normalizedPath.slice(normalizedPath.lastIndexOf("/") + 1);
        showFilePreviewMessage(previewWindow, normalizedPath, `Loading ${fileName} from OPFS...`);
    } catch {
        previewWindow.close();
        return false;
    }

    void loadFilePreview(previewWindow, normalizedPath);
    return true;
}

export async function beginDownload(suggestedFileName, useZip) {
    requireBrowserAvailable();
    const suggestedName = sanitizeSuggestedName(suggestedFileName);
    const mimeType = useZip ? "application/zip" : "application/octet-stream";

    if (typeof globalThis.showSaveFilePicker === "function") {
        let fileHandle;
        try {
            const options = { suggestedName };
            if (useZip) {
                options.types = [{
                    description: "ZIP archive",
                    accept: { "application/zip": [".zip"] },
                }];
            }
            fileHandle = await globalThis.showSaveFilePicker(options);
        } catch (error) {
            if (isNamedDomException(error, "AbortError")) {
                return JSON.stringify({ handle: 0, canceled: true, mode: "picker", stagingPath: null });
            }
            if (!isNamedDomException(error, "NotAllowedError") &&
                !isNamedDomException(error, "SecurityError") &&
                !isNamedDomException(error, "NotSupportedError")) {
                throw error;
            }
            console.warn("Streaming file picker is unavailable; using OPFS staging download.", error);
            return beginStagingDownload(suggestedName, mimeType);
        }

        const writable = await fileHandle.createWritable();
        const handle = ++nextOutputHandle;
        outputHandles.set(handle, {
            mode: "picker",
            writable,
            pending: Promise.resolve(),
            suggestedName,
            mimeType,
            fileHandle,
            stagingPath: null,
        });
        return JSON.stringify({ handle, canceled: false, mode: "picker", stagingPath: null });
    }

    return beginStagingDownload(suggestedName, mimeType);
}

export async function buildManifest(requestJson) {
    requireBrowserAvailable();
    const request = JSON.parse(requestJson);
    if (!Array.isArray(request.selectedEntries) || request.selectedEntries.length === 0) {
        throw new TypeError("At least one OPFS entry is required to build a download manifest.");
    }

    const normalizedSelections = request.selectedEntries
        .map(entry => ({
            relativePath: normalizePath(entry.relativePath),
            kind: entry.kind,
        }))
        .sort((left, right) =>
            getPathDepth(left.relativePath) - getPathDepth(right.relativePath) ||
            compareEntryNames(left.relativePath, right.relativePath));
    const selections = [];
    for (const entry of normalizedSelections) {
        if (selections.some(parent =>
            parent.kind === EntryKind.folder && isDescendantPath(entry.relativePath, parent.relativePath))) {
            continue;
        }
        if (!selections.some(existing => existing.relativePath === entry.relativePath)) {
            selections.push(entry);
        }
    }

    const manifest = {
        entries: [],
        totalBytes: 0,
        totalFiles: 0,
    };
    const visitedPaths = new Set();
    for (const entry of selections) {
        if (entry.kind === EntryKind.folder) {
            await addManifestDirectory(entry.relativePath, manifest, visitedPaths);
        } else if (entry.kind === EntryKind.file) {
            await addManifestFile(entry.relativePath, manifest, visitedPaths);
        } else {
            throw new TypeError(`Unknown OPFS entry kind ${entry.kind}.`);
        }
    }

    return JSON.stringify(manifest);
}

export async function validateManifest(manifestJson) {
    requireBrowserAvailable();
    const manifest = JSON.parse(manifestJson);
    for (const entry of manifest.entries) {
        if (entry.kind === EntryKind.folder) {
            try {
                await resolveDirectory(entry.path);
            } catch (error) {
                throw new DOMException(`OPFS folder changed during download: ${entry.path}`, "InvalidStateError");
            }
            continue;
        }

        let file;
        try {
            file = await (await resolveFile(entry.path)).getFile();
        } catch (error) {
            throw new DOMException(`OPFS file became unreadable during download: ${entry.path}`, "InvalidStateError");
        }
        if (file.size !== entry.size || file.lastModified !== entry.lastModified) {
            throw new DOMException(`OPFS file changed during download: ${entry.path}`, "InvalidStateError");
        }
    }
    return true;
}

export async function openRead(relativePath, expectedSize, expectedLastModified) {
    const normalizedPath = normalizePath(relativePath);
    const file = await (await resolveFile(normalizedPath)).getFile();
    if (file.size !== expectedSize || file.lastModified !== expectedLastModified) {
        throw new DOMException(`OPFS file changed before it could be read: ${normalizedPath}`, "InvalidStateError");
    }

    const handle = ++nextReadHandle;
    readHandles.set(handle, {
        file,
        offset: 0,
    });
    return handle;
}

export async function readFile(relativePath) {
    const normalizedPath = normalizePath(relativePath);
    try {
        const file = await (await resolveFile(normalizedPath)).getFile();
        return { data: new Uint8Array(await file.arrayBuffer()) };
    } catch (error) {
        if (isNotFound(error)) {
            return { data: null };
        }
        throw error;
    }
}

export async function readChunk(handle, maximumByteLength) {
    const state = requireReadHandle(handle);
    if (!Number.isSafeInteger(maximumByteLength) || maximumByteLength <= 0) {
        throw new RangeError("The OPFS read chunk length must be a positive safe integer.");
    }

    if (state.offset >= state.file.size) {
        return { data: new Uint8Array(0) };
    }

    const end = Math.min(state.offset + maximumByteLength, state.file.size);
    const data = new Uint8Array(await state.file.slice(state.offset, end).arrayBuffer());
    state.offset = end;
    return { data };
}

export function closeRead(handle) {
    readHandles.delete(handle);
}

export function setWriteBuffer(handle, data, byteLength) {
    const bytes = new Uint8Array(byteLength);
    copyFromMemoryView(data, bytes);
    writeBuffers.set(handle, bytes);
}

export function releaseWriteBuffer(handle) {
    writeBuffers.delete(handle);
}

export async function writeFile(relativePath, handle) {
    const bytes = requireWriteBuffer(handle);
    const normalizedPath = normalizePath(relativePath);
    const resolved = await resolveParent(normalizedPath);
    const fileHandle = await resolved.parent.getFileHandle(resolved.name, { create: true });
    const writable = await fileHandle.createWritable();
    let committed = false;
    try {
        await writable.write(bytes);
        await writable.close();
        committed = true;
    } finally {
        if (!committed) {
            await abortWritableSilently(writable);
        }
    }
}

export function queueDownloadBuffer(outputHandle, bufferHandle) {
    const output = requireOutputHandle(outputHandle);
    enqueueOutputWrite(output, requireWriteBuffer(bufferHandle));
}

export async function writeDownloadBuffer(outputHandle, bufferHandle) {
    const output = requireOutputHandle(outputHandle);
    await enqueueOutputWrite(output, requireWriteBuffer(bufferHandle));
}

export async function flushDownload(outputHandle) {
    await requireOutputHandle(outputHandle).pending;
}

export async function closeDownload(outputHandle) {
    const output = requireOutputHandle(outputHandle);
    await output.pending;
    await output.writable.close();

    if (output.mode === "staging") {
        await triggerOrdinaryDownload(output);
        stagingStates.set(output.stagingPath, StagingState.waitingAutomaticCleanup);
    }
    outputHandles.delete(outputHandle);
}

export async function abortDownload(outputHandle) {
    const output = outputHandles.get(outputHandle);
    if (output === undefined) {
        return;
    }

    try {
        await output.pending;
    } catch {
        // The pending write error is superseded by the original .NET operation error.
    }

    try {
        if (typeof output.writable.abort === "function") {
            await output.writable.abort();
        }
    } catch (error) {
        console.warn("Failed to abort a browser download stream.", error);
    } finally {
        outputHandles.delete(outputHandle);
    }

    if (output.mode === "staging") {
        await removeStagingFile(output.stagingPath);
    }
}

globalThis.addEventListener?.("beforeunload", () => {
    for (const objectUrl of retainedObjectUrls) {
        URL.revokeObjectURL(objectUrl);
    }
    retainedObjectUrls.clear();
});

globalThis.BrowserOpfsInterop = Object.freeze({
    initialize,
    isAvailable,
    listDirectory,
    directoryExists,
    openFilePreview,
    beginDownload,
    buildManifest,
    validateManifest,
    openRead,
    readFile,
    readChunk,
    closeRead,
    setWriteBuffer,
    releaseWriteBuffer,
    writeFile,
    queueDownloadBuffer,
    writeDownloadBuffer,
    flushDownload,
    closeDownload,
    abortDownload,
});
