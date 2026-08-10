import {
    getOrCreateRootDirectory,
    isAvailable as isOpfsAvailable,
} from './opfs.js';

const writeBuffers = new Map();
let temporaryFileSystem = null;

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

function requireTemporaryFileSystem() {
    if (temporaryFileSystem === null) {
        throw new DOMException("Origin-private temporary storage is unavailable.", "NotSupportedError");
    }
    return temporaryFileSystem;
}

function requireWriteBuffer(handle) {
    const bytes = writeBuffers.get(handle);
    if (bytes === undefined) {
        throw new Error(`Unknown temporary write buffer ${handle}.`);
    }
    return bytes;
}

export async function initialize() {
    temporaryFileSystem = null;
    if (!isOpfsAvailable()) {
        return false;
    }

    try {
        temporaryFileSystem = await getOrCreateRootDirectory("temp");
        return true;
    } catch (error) {
        temporaryFileSystem = null;
        console.warn("OPFS temporary storage is unavailable; temporary writes will be discarded.", error);
        return false;
    }
}

export function isAvailable() {
    return temporaryFileSystem !== null;
}

export async function getEntryKind(relativePath) {
    return requireTemporaryFileSystem().getEntryKind(relativePath);
}

export async function createFile(relativePath) {
    await requireTemporaryFileSystem().createFile(relativePath);
}

export async function tryCreateFile(relativePath) {
    return requireTemporaryFileSystem().tryCreateFile(relativePath);
}

export async function createFolder(relativePath) {
    await requireTemporaryFileSystem().createFolder(relativePath);
}

export async function tryCreateFolder(relativePath) {
    return requireTemporaryFileSystem().tryCreateFolder(relativePath);
}

export async function getFileLength(relativePath) {
    return requireTemporaryFileSystem().getFileLength(relativePath);
}

export async function readFile(relativePath) {
    return { data: await requireTemporaryFileSystem().readFile(relativePath) };
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
    await requireTemporaryFileSystem().writeFile(relativePath, bytes);
}

export async function appendFile(relativePath, handle) {
    const bytes = requireWriteBuffer(handle);
    await requireTemporaryFileSystem().appendFile(relativePath, bytes);
}

export async function deleteFile(relativePath) {
    await requireTemporaryFileSystem().deleteFile(relativePath);
}

export async function deleteFolder(relativePath) {
    await requireTemporaryFileSystem().deleteFolder(relativePath);
}

export async function clear() {
    await requireTemporaryFileSystem().clear();
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
