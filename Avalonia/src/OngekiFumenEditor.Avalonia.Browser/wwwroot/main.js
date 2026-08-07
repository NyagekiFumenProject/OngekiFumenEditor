import {initialize as initializeTemporaryFileSystem} from './temporaryFileSystem.js';

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
    phaseStart("temporary-file-system", "正在初始化临时文件系统...", 8);
    trackResourceStart("./temporaryFileSystem.js", "临时文件系统模块");
    const temporaryFileSystemAvailable = await initializeTemporaryFileSystem();
    trackResourceComplete("./temporaryFileSystem.js");
    phaseComplete(
        "temporary-file-system",
        temporaryFileSystemAvailable
            ? "临时文件系统已准备。"
            : "临时文件系统不可用，将使用不持久化的回退实现。",
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
