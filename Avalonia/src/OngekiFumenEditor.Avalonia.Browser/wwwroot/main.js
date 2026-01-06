let dotnetModule;

try {
    dotnetModule = await import('./_framework/dotnet.js');
} catch (e) {
    try {
        dotnetModule = await import('./dotnet.js');
    } catch (e2) {
        console.error("无法找到 dotnet.js 入口文件", e2);
    }
}
const {dotnet} = dotnetModule;

console.log(`dotnet = ${dotnet}`);

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let dotnetBuilder = dotnet
    //.withDiagnosticTracing(true)
    .withApplicationArgumentsFromQuery();

if (dotnetBuilder.withMainAssembly) {
    dotnetBuilder = dotnetBuilder.withMainAssembly("OngekiFumenEditor.Avalonia.Browser");
    console.log("append withMainAssembly() into builder");
}

const dotnetRuntime = await dotnetBuilder.create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
