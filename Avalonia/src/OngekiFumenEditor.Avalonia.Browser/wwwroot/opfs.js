export const EntryKind = Object.freeze({
    missing: 0,
    file: 1,
    folder: 2,
});

let originRoot = null;

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

function isUnavailableInitializationError(error) {
    return typeof DOMException !== "undefined" &&
        error instanceof DOMException && [
            "SecurityError",
            "NotAllowedError",
            "NotSupportedError",
            "InvalidStateError",
        ].includes(error.name);
}

function requireOriginRoot() {
    if (originRoot === null) {
        throw new DOMException("Origin-private file system is unavailable.", "NotSupportedError");
    }
    return originRoot;
}

export function validateEntryName(entryName) {
    if (typeof entryName !== "string" || entryName.length === 0 ||
        entryName === "." || entryName === ".." ||
        entryName.includes("\\") || entryName.includes("/")) {
        throw new TypeError(`Unsafe OPFS entry name: ${entryName}`);
    }
}

function splitPath(relativePath) {
    if (typeof relativePath !== "string") {
        throw new TypeError("An OPFS relative path must be a string.");
    }

    if (relativePath.length === 0) {
        return [];
    }

    const segments = relativePath.split("/");
    for (const segment of segments) {
        validateEntryName(segment);
    }
    return segments;
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
    originRoot = null;

    try {
        const storage = globalThis.navigator?.storage;
        if (typeof storage?.getDirectory !== "function") {
            return false;
        }

        originRoot = await storage.getDirectory();
        return true;
    } catch (error) {
        originRoot = null;
        if (!isUnavailableInitializationError(error)) {
            throw error;
        }
        console.warn("OPFS storage is unavailable.", error);
        return false;
    }
}

export function isAvailable() {
    return originRoot !== null;
}

export async function getOrCreateRootDirectory(entryName) {
    validateEntryName(entryName);
    const handle = await requireOriginRoot().getDirectoryHandle(entryName, { create: true });
    return new OpfsDirectory(handle);
}

export class OpfsDirectory {
    #root;

    constructor(root) {
        if (root === null || root === undefined) {
            throw new TypeError("An OPFS directory handle is required.");
        }
        this.#root = root;
    }

    async #resolveParent(relativePath) {
        const segments = splitPath(relativePath);
        if (segments.length === 0) {
            throw new TypeError("The OPFS directory root does not have a parent entry.");
        }

        let parent = this.#root;
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

    async #requireParent(relativePath) {
        const resolved = await this.#resolveParent(relativePath);
        if (resolved === null) {
            throw new DOMException(`The parent of '${relativePath}' does not exist.`, "NotFoundError");
        }
        return resolved;
    }

    async #requireFile(relativePath) {
        const resolved = await this.#requireParent(relativePath);
        return resolved.parent.getFileHandle(resolved.name);
    }

    async getEntryKind(relativePath) {
        const resolved = await this.#resolveParent(relativePath);
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

    async createFile(relativePath) {
        const resolved = await this.#requireParent(relativePath);
        await resolved.parent.getFileHandle(resolved.name, { create: true });
    }

    async tryCreateFile(relativePath) {
        if (await this.getEntryKind(relativePath) !== EntryKind.missing) {
            return false;
        }
        await this.createFile(relativePath);
        return true;
    }

    async createFolder(relativePath) {
        const resolved = await this.#requireParent(relativePath);
        await resolved.parent.getDirectoryHandle(resolved.name, { create: true });
    }

    async tryCreateFolder(relativePath) {
        if (await this.getEntryKind(relativePath) !== EntryKind.missing) {
            return false;
        }
        await this.createFolder(relativePath);
        return true;
    }

    async getFileLength(relativePath) {
        const handle = await this.#requireFile(relativePath);
        return (await handle.getFile()).size;
    }

    async readFile(relativePath) {
        const handle = await this.#requireFile(relativePath);
        return new Uint8Array(await (await handle.getFile()).arrayBuffer());
    }

    async writeFile(relativePath, bytes) {
        const resolved = await this.#requireParent(relativePath);
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

    async appendFile(relativePath, bytes) {
        const fileHandle = await this.#requireFile(relativePath);
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

    async deleteFile(relativePath) {
        const resolved = await this.#resolveParent(relativePath);
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

    async deleteFolder(relativePath) {
        if (relativePath === "") {
            await this.clear();
            return;
        }

        const resolved = await this.#resolveParent(relativePath);
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

    async clear() {
        for await (const [name] of this.#root.entries()) {
            await this.#root.removeEntry(name, { recursive: true });
        }
    }
}
