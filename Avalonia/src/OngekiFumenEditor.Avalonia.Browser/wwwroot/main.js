import {initialize as initializeOpfs} from './opfs.js';
import {
    initialize as initializeTemporaryFileSystem,
    isAvailable as isTemporaryStorageAvailable,
} from './temporaryFileSystem.js';
import {
    initialize as initializeLogFileSystem,
    isAvailable as isLogStorageAvailable,
} from './logFileSystem.js';
import {initialize as initializeBrowserOpfs} from './opfsBrowser.js';

const startup = globalThis.__startupProgress;
let currentPhase = "启动入口";
let currentResource = "";

function phaseStart(id, message, progress) {
    currentPhase = message || id || currentPhase;
    startup?.phaseStart(id, message, progress);
}

function phaseComplete(id, message, progress) {
    currentPhase = message || id || currentPhase;
    startup?.phaseComplete(id, message, progress);
}

function trackResourceStart(url, label) {
    currentResource = url;
    startup?.resourceStart(url, label);
}

function trackResourceComplete(url, details) {
    startup?.resourceComplete(url, details);
}

async function loadDotnetModule() {
    const candidates = [
        './_framework/dotnet.js',
        './dotnet.js',
    ];
    let lastError = null;

    for (const candidate of candidates) {
        trackResourceStart(candidate, ".NET runtime 入口");
        try {
            const module = await import(candidate);
            trackResourceComplete(candidate);
            return module;
        } catch (error) {
            lastError = error;
            // One of these paths is intentionally absent in some publish layouts.
            startup?.resourceWarning(candidate, error, ".NET runtime 入口");
        }
    }

    const error = new Error("无法加载 .NET runtime 入口文件（已尝试 _framework/dotnet.js 和 dotnet.js）。");
    error.cause = lastError;
    throw error;
}

async function startBrowserApplication() {
    phaseStart("temporary-file-system", "正在初始化 OPFS 临时与日志存储...", 8);
    trackResourceStart("./opfs.js", "OPFS 基础存储模块");
    await initializeOpfs();
    await Promise.all([
        initializeTemporaryFileSystem(),
        initializeLogFileSystem(),
        initializeBrowserOpfs(),
    ]);
    const temporaryStorageAvailable = isTemporaryStorageAvailable();
    const logStorageAvailable = isLogStorageAvailable();
    trackResourceComplete("./opfs.js");
    const storageStatus = temporaryStorageAvailable && logStorageAvailable
        ? "OPFS 临时与日志存储已准备。"
        : temporaryStorageAvailable
            ? "OPFS 临时存储已准备，文件日志不可用。"
            : logStorageAvailable
                ? "OPFS 文件日志已准备，临时文件将使用非持久化回退。"
                : "OPFS 不可用：临时文件将使用非持久化回退，文件日志已禁用。";
    phaseComplete(
        "temporary-file-system",
        storageStatus,
        18);

    phaseStart("dotnet-entry", "正在加载 .NET runtime 入口...", 22);
    const dotnetModule = await loadDotnetModule();
    const {dotnet} = dotnetModule || {};
    if (!dotnet) {
        throw new Error(".NET runtime 入口没有导出 dotnet builder。");
    }
    phaseComplete("dotnet-entry", ".NET runtime 入口已加载。", 32);

    const isBrowser = typeof window !== "undefined";
    if (!isBrowser) {
        throw new Error("当前运行环境不是浏览器。");
    }

    let dotnetBuilder = dotnet
        //.withDiagnosticTracing(true)
        .withApplicationArgumentsFromQuery();

    if (typeof dotnetBuilder.withResourceLoader === "function" && startup?.bootResourceLoader) {
        dotnetBuilder = dotnetBuilder.withResourceLoader(startup.bootResourceLoader);
    }

    if (dotnetBuilder.withMainAssembly) {
        dotnetBuilder = dotnetBuilder.withMainAssembly("OngekiFumenEditor.Avalonia.Browser");
        console.log("append withMainAssembly() into builder");
    }

    currentResource = "";
    phaseStart("dotnet-runtime", "正在下载并创建 .NET runtime...", 38);
    const dotnetRuntime = await dotnetBuilder.create();
    phaseComplete("dotnet-runtime", ".NET runtime 已创建，正在启动 Avalonia...", 84);

    const config = dotnetRuntime.getConfig();
    if (!config?.mainAssemblyName) {
        throw new Error(".NET runtime 配置中缺少主程序集名称。");
    }

    phaseStart("avalonia-main", `正在启动主程序集 ${config.mainAssemblyName}...`, 90);
    currentResource = "";
    const runMainTask = dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    startup?.ready("Avalonia 应用已启动。");
    await runMainTask;
}

try {
    await startBrowserApplication();
} catch (error) {
    startup?.fail(error, currentPhase, currentResource);
    console.error("Browser startup failed", error);
    throw error;
}
