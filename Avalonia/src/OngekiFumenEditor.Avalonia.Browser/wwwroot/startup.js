"use strict";

(() => {
    const root = globalThis;
    const overlay = document.getElementById("startup-overlay");
    const statusElement = document.getElementById("startup-status");
    const resourceElement = document.getElementById("startup-resource");
    const progressElement = overlay?.querySelector(".startup-progress");
    const progressBar = document.getElementById("startup-progress-bar");
    const progressPercent = document.getElementById("startup-progress-percent");
    const progressCount = document.getElementById("startup-progress-count");
    const resourceList = document.getElementById("startup-resources");
    const errorElement = document.getElementById("startup-error");
    const errorMessageElement = document.getElementById("startup-error-message");
    const errorResourceElement = document.getElementById("startup-error-resource");
    const errorStackElement = document.getElementById("startup-error-stack");
    const reloadButton = document.getElementById("startup-reload");

    if (!overlay || !statusElement || !resourceElement || !progressBar || !resourceList ||
        !errorElement || !errorMessageElement || !errorResourceElement || !errorStackElement) {
        return;
    }

    const state = {
        progress: 0,
        currentResource: "",
        currentPhase: "",
        failed: false,
        readyRequested: false,
        tracking: true,
        activeFetches: 0,
        lastFailedResource: null,
        resources: new Map(),
        resourceOrder: [],
        errors: [],
        hideTimer: null,
        fetchRestore: null,
        performanceObserver: null,
    };

    const originalFetch = typeof root.fetch === "function" ? root.fetch : null;
    const resourceLimit = 80;

    function now() {
        return typeof performance === "object" && typeof performance.now === "function"
            ? performance.now()
            : Date.now();
    }

    function clampProgress(value) {
        const numericValue = Number(value);
        if (!Number.isFinite(numericValue)) {
            return state.progress;
        }
        return Math.max(0, Math.min(100, numericValue));
    }

    function resolveUrl(value) {
        let rawValue = value;
        if (value && typeof value === "object" && typeof value.url === "string") {
            rawValue = value.url;
        }
        if (typeof rawValue !== "string" || rawValue.length === 0) {
            return "";
        }
        try {
            return new URL(rawValue, document.baseURI).href;
        } catch {
            return rawValue;
        }
    }

    function displayResourceName(value) {
        const url = resolveUrl(value);
        if (!url) {
            return "未知资源";
        }
        try {
            const parsedUrl = new URL(url, document.baseURI);
            if (parsedUrl.origin === globalThis.location.origin) {
                return `${parsedUrl.pathname}${parsedUrl.search}`;
            }
            return parsedUrl.href;
        } catch {
            return url;
        }
    }

    function isTrackableResource(value) {
        const url = resolveUrl(value);
        if (!url) {
            return false;
        }
        return !/^(data|blob|javascript|about):/i.test(url);
    }

    function formatError(error) {
        if (error instanceof Error) {
            return error.message || error.name || "未知错误";
        }
        if (error && typeof error.message === "string") {
            return error.message;
        }
        if (typeof error === "string") {
            return error;
        }
        try {
            const serialized = JSON.stringify(error);
            return serialized && serialized !== "{}" ? serialized : String(error);
        } catch {
            return String(error);
        }
    }

    function formatStatus(status) {
        switch (status) {
            case "loading":
                return "加载中";
            case "loaded":
                return "已完成";
            case "warning":
                return "可选失败";
            case "error":
                return "失败";
            default:
                return "等待";
        }
    }

    function updateResourceCount() {
        if (!progressCount) {
            return;
        }
        const records = [...state.resources.values()];
        const completed = records.filter(record =>
            record.status === "loaded" || record.status === "warning" || record.status === "error").length;
        progressCount.textContent = records.length === 0
            ? "等待资源..."
            : `${completed}/${records.length} 个资源`;
    }

    function updateProgress(value) {
        const nextProgress = clampProgress(value);
        state.progress = Math.max(state.progress, nextProgress);
        const roundedProgress = Math.round(state.progress);
        progressBar.style.width = `${roundedProgress}%`;
        if (progressElement) {
            progressElement.setAttribute("aria-valuenow", String(roundedProgress));
        }
        if (progressPercent) {
            progressPercent.textContent = `${roundedProgress}%`;
        }
    }

    function setStatus(message, progress, resource) {
        if (typeof message === "string" && message.length > 0) {
            statusElement.textContent = message;
        }
        if (resource !== undefined) {
            state.currentResource = resolveUrl(resource);
            resourceElement.textContent = state.currentResource
                ? `资源：${displayResourceName(state.currentResource)}`
                : "";
            resourceElement.title = state.currentResource || "";
        }
        if (progress !== undefined) {
            updateProgress(progress);
        }
        updateResourceCount();
    }

    function trimResourceList() {
        while (state.resourceOrder.length > resourceLimit) {
            const index = state.resourceOrder.findIndex(key => {
                const record = state.resources.get(key);
                return record && record.status !== "loading";
            });
            if (index < 0) {
                return;
            }
            const [key] = state.resourceOrder.splice(index, 1);
            const record = state.resources.get(key);
            record?.element.remove();
            state.resources.delete(key);
        }
    }

    function ensureResource(value, label) {
        const url = resolveUrl(value);
        const key = url || `resource:${label || "unknown"}`;
        let record = state.resources.get(key);
        if (record) {
            return record;
        }

        const element = document.createElement("li");
        element.className = "startup-resource-row";
        element.dataset.status = "pending";

        const stateNode = document.createElement("span");
        stateNode.className = "startup-resource-state";
        stateNode.textContent = formatStatus("pending");

        const nameNode = document.createElement("span");
        nameNode.className = "startup-resource-name";
        nameNode.textContent = label ? `${label} (${displayResourceName(url)})` : displayResourceName(url);
        nameNode.title = url || label || "未知资源";

        element.append(stateNode, nameNode);
        resourceList.append(element);
        record = {
            key,
            url,
            label: label || "网络资源",
            element,
            stateNode,
            nameNode,
            status: "pending",
            startedAt: now(),
            runtime: state.currentPhase === "dotnet-runtime",
            attempts: 0,
        };
        state.resources.set(key, record);
        state.resourceOrder.push(key);
        trimResourceList();
        return record;
    }

    function markResource(value, status, label, details) {
        const record = ensureResource(value, label);
        record.status = status;
        record.stateNode.textContent = formatStatus(status);
        record.element.dataset.status = status;
        if (label && record.label !== label) {
            record.label = label;
            record.nameNode.textContent = `${label} (${displayResourceName(record.url)})`;
        }
        if (details) {
            record.nameNode.title = `${record.url || record.label}\n${details}`;
        }
        if (status === "loading") {
            record.startedAt = now();
            record.attempts += 1;
            state.currentResource = record.url;
            setStatus(`正在加载：${record.label}`, undefined, record.url);
            resourceList.append(record.element);
        } else if (state.currentResource === record.url) {
            resourceElement.textContent = record.url
                ? `资源：${displayResourceName(record.url)}`
                : "";
        }
        updateResourceCount();
        updateRuntimeProgress();
        return record;
    }

    function resourceStart(value, label) {
        if (!state.tracking) {
            return null;
        }
        return markResource(value, "loading", label);
    }

    function resourceComplete(value, details) {
        const record = markResource(value, "loaded", undefined, details);
        if (state.lastFailedResource?.key === record.key) {
            state.lastFailedResource = null;
        }
        return record;
    }

    function resourceWarning(value, error, label) {
        const record = markResource(value, "warning", label, formatError(error));
        if (record) {
            record.stateNode.textContent = "可选失败";
        }
        return record;
    }

    function resourceError(value, error, label, fatal) {
        const record = markResource(value, fatal ? "error" : "warning", label, formatError(error));
        state.lastFailedResource = record;
        if (fatal) {
            fail(error, "资源加载失败", record?.url);
        }
        return record;
    }

    function updateRuntimeProgress() {
        if (state.currentPhase !== "dotnet-runtime" || state.failed) {
            return;
        }
        const runtimeRecords = [...state.resources.values()].filter(record => record.runtime);
        const finished = runtimeRecords.filter(record =>
            record.status === "loaded" || record.status === "warning" || record.status === "error").length;
        const pending = runtimeRecords.filter(record => record.status === "loading").length;
        const denominator = finished + pending + state.activeFetches;
        if (denominator > 0) {
            updateProgress(42 + Math.min(38, (finished / denominator) * 38));
        }
    }

    function phaseStart(id, message, progress) {
        if (state.failed) {
            return;
        }
        state.currentPhase = id || "";
        setStatus(message, progress);
    }

    function phaseComplete(id, message, progress) {
        if (id) {
            state.currentPhase = id;
        }
        setStatus(message, progress);
    }

    function addError(error, context, resource) {
        const message = formatError(error);
        const normalizedResource = resolveUrl(resource);
        const duplicate = state.errors.some(item =>
            item.message === message && item.context === context && item.resource === normalizedResource);
        if (duplicate) {
            return;
        }
        state.errors.push({ message, context, resource: normalizedResource });
    }

    function fail(error, context, resource) {
        const failedResource = state.lastFailedResource?.url || resolveUrl(resource) || "";
        addError(error, context, failedResource);
        if (state.failed) {
            return;
        }
        state.failed = true;
        if (state.hideTimer !== null) {
            clearTimeout(state.hideTimer);
            state.hideTimer = null;
        }
        overlay.classList.add("startup-failed");
        overlay.classList.remove("splash-close");
        errorElement.hidden = false;
        errorMessageElement.textContent = `${context ? `${context}：` : ""}${formatError(error)}`;
        errorResourceElement.textContent = failedResource
            ? `相关资源：${displayResourceName(failedResource)}`
            : "";
        errorStackElement.textContent = error?.stack || formatError(error);
        setStatus("启动失败，请查看错误信息。", undefined, failedResource);
        updateProgress(state.progress);
        console.error(context || "Browser startup failed", error);
    }

    function ready(message) {
        if (state.failed || state.readyRequested) {
            return;
        }
        state.readyRequested = true;
        setStatus(message || "应用已启动", 100);
        const startedAt = now();
        const reveal = () => {
            if (state.failed) {
                return;
            }
            const host = document.getElementById("out");
            const hasApplicationContent = !!host && host.childNodes.length > 0;
            if (hasApplicationContent || now() - startedAt >= 2500) {
                overlay.classList.add("splash-close");
                state.hideTimer = setTimeout(() => {
                    state.tracking = false;
                    if (state.fetchRestore) {
                        state.fetchRestore();
                        state.fetchRestore = null;
                    }
                }, 320);
                return;
            }
            setTimeout(reveal, 50);
        };
        setTimeout(reveal, 120);
    }

    function handleResourceElement(element) {
        if (!(element instanceof HTMLScriptElement) && !(element instanceof HTMLLinkElement)) {
            return;
        }
        const rawUrl = element instanceof HTMLScriptElement ? element.src : element.href;
        if (!rawUrl || element.dataset.startupObserved === "true") {
            return;
        }
        element.dataset.startupObserved = "true";
        const label = element.dataset.startupLabel ||
            (element instanceof HTMLLinkElement ? "样式表" : "脚本");
        resourceStart(rawUrl, label);
        element.addEventListener("load", () => resourceComplete(rawUrl), { once: true });
        element.addEventListener("error", () =>
            resourceError(rawUrl, new Error(`无法加载 ${displayResourceName(rawUrl)}`), label, true), { once: true });
        if (element instanceof HTMLLinkElement && element.sheet) {
            resourceComplete(rawUrl);
        }
    }

    function installElementTracking() {
        document.querySelectorAll("script[src], link[href]").forEach(handleResourceElement);
        if (typeof MutationObserver === "function") {
            const observer = new MutationObserver(records => {
                for (const mutation of records) {
                    for (const node of mutation.addedNodes) {
                        if (node instanceof Element) {
                            handleResourceElement(node);
                            node.querySelectorAll?.("script[src], link[href]").forEach(handleResourceElement);
                        }
                    }
                }
            });
            observer.observe(document.documentElement, { childList: true, subtree: true });
        }
    }

    function installPerformanceTracking() {
        const processEntries = entries => {
            if (!state.tracking) {
                return;
            }
            for (const entry of entries) {
                if (!isTrackableResource(entry.name)) {
                    continue;
                }
                const record = ensureResource(entry.name, "网络资源");
                if (record.status === "pending" || record.status === "loading") {
                    resourceComplete(entry.name, `${Math.round(entry.duration)} ms`);
                }
            }
        };
        if (typeof performance === "object" && typeof performance.getEntriesByType === "function") {
            processEntries(performance.getEntriesByType("resource"));
        }
        if (typeof PerformanceObserver !== "function") {
            return;
        }
        try {
            state.performanceObserver = new PerformanceObserver(list => processEntries(list.getEntries()));
            state.performanceObserver.observe({ type: "resource", buffered: true });
        } catch {
            state.performanceObserver = null;
        }
    }

    function installFetchTracking() {
        if (!originalFetch) {
            return;
        }
        const trackedFetch = function (...args) {
            if (!state.tracking) {
                return Reflect.apply(originalFetch, this, args);
            }
            const url = resolveUrl(args[0]);
            if (!isTrackableResource(url)) {
                return Reflect.apply(originalFetch, this, args);
            }
            const record = resourceStart(url, "网络资源");
            state.activeFetches += 1;
            let request;
            try {
                request = Reflect.apply(originalFetch, this, args);
            } catch (error) {
                state.activeFetches = Math.max(0, state.activeFetches - 1);
                resourceError(url, error, record?.label, false);
                throw error;
            }
            return Promise.resolve(request).then(response => {
                state.activeFetches = Math.max(0, state.activeFetches - 1);
                if (response && response.ok === false) {
                    resourceError(url, new Error(`HTTP ${response.status} ${response.statusText || "请求失败"}`), record?.label, false);
                } else if (!state.performanceObserver) {
                    resourceComplete(url, response ? `${response.status} ${response.statusText || ""}`.trim() : "已响应");
                } else if (record) {
                    record.nameNode.title = `${record.url || record.label}\n${response.status} ${response.statusText || ""}`.trim();
                }
                return response;
            }, error => {
                state.activeFetches = Math.max(0, state.activeFetches - 1);
                resourceError(url, error, record?.label, false);
                throw error;
            });
        };
        root.fetch = trackedFetch;
        state.fetchRestore = () => {
            if (root.fetch === trackedFetch) {
                root.fetch = originalFetch;
            }
        };
    }

    function installErrorTracking() {
        root.addEventListener("error", event => {
            const target = event.target;
            const resource = target && (target.src || target.href);
            if (resource) {
                resourceError(resource, event.error || new Error(`无法加载 ${displayResourceName(resource)}`), undefined, true);
                return;
            }
            if (event.error || event.message) {
                fail(event.error || new Error(event.message), "运行时错误", event.filename);
            }
        }, true);
        root.addEventListener("unhandledrejection", event => {
            fail(event.reason || new Error("未处理的 Promise 异常"), "未处理的启动异常");
        });
    }

    function bootResourceLoader(type, name, defaultUri) {
        const resource = defaultUri || name;
        if (state.tracking && resource) {
            resourceStart(resource, `${type || "runtime"}: ${name || displayResourceName(resource)}`);
        }
        return undefined;
    }

    reloadButton?.addEventListener("click", () => globalThis.location.reload());
    installElementTracking();
    installPerformanceTracking();
    installFetchTracking();
    installErrorTracking();
    phaseStart("bootstrap", "正在加载启动监控...", 5);

    root.__startupProgress = Object.freeze({
        phaseStart,
        phaseComplete,
        resourceStart,
        resourceComplete,
        resourceWarning,
        resourceError,
        bootResourceLoader,
        setStatus,
        updateProgress,
        ready,
        fail,
    });
})();
