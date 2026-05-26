OngekiFumenEditor当前使用Costura.Fody实现单文件程序。即dotnet publish后，除了项目程序自己exe以及同名dll，没有其他多余的依赖dll文件存在。考虑使用MSBuild的PublishSingleFile替代Costura.Fody

目标
1. 使用Msbuild的PublishSingleFile替代Costura.Fody相关功能
2. dotnet publish .\OngekiFumenEditor\OngekiFumenEditor.csproj --no-restore -c RELEASE -o F:\ogkrEditorBuild\ --no-restore --disable-build-servers --force
   和
   dotnet publish .\OngekiFumenEditor.CommandLine\OngekiFumenEditor.CommandLine.csproj -c Release -o F:\ogkrEditorBuild\ --no-restore --disable-build-servers --force
3. CommandLine项目会调用OngekiFumenEditor的功能，注意一下
4. shell执行命令 OngekiFumenEditor.CommandLine.exe convert --inputFile "F:\\OngekiFumenEditor\\OngekiFumenEditor.Benchmark\\Data\\FumenSamples\\20993_04.ogkr" --outputFile "F:\\ogkrEditorBuild\\test\\20993_04.nyageki" 检查20993_04.nyageki是否正确生成

---

## 实施记录

### 1. 移除 Costura.Fody

- `OngekiFumenEditor/OngekiFumenEditor.csproj` 删除 `Costura.Fody` PackageReference、`DisableFody` 条件、`CopySoundTouchDll` 目标以及把 `costura-win-x64/*.dll` 当作 `EmbeddedResource` 的写法；改为以 `ContentWithTargetPath` 投到 `runtimes\win-x64\native\`，由 .NET runtime 在 single-file bundle 内自带原生库。
- 删除 `OngekiFumenEditor/FodyWeavers.xml` 与 `FodyWeavers.xsd`。
- `OngekiFumenEditor/App.xaml.cs` 移除原本用于解决 Costura satellite assembly 名字大小写问题的 `OnAssemblyResolve / TryResolveAsSatelliteAssembly` 逻辑。
- `OngekiFumenEditor.Benchmark/Infrastructure/BenchmarkRuntime.cs` 移除 `Costura.AssemblyLoader.Attach` 调用。
- `OngekiFumenEditor.CommandLine/Program.cs` 移除 Costura.Attach 反射调用。

### 2. PublishSingleFile 配置

`OngekiFumenEditor/OngekiFumenEditor.csproj` 与 `OngekiFumenEditor.CommandLine/OngekiFumenEditor.CommandLine.csproj` 都加入仅在 publish 时启用的 PropertyGroup（条件 `'$(_IsPublishing)' == 'true' or '$(PublishProtocol)' != ''`）：

```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
<SelfContained>false</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>false</EnableCompressionInSingleFile>
```

另外加 `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`（plural），确保 restore 阶段就解析到 win-x64 runtime asset，避免 `NETSDK1047`。`EnableCompressionInSingleFile` 必须为 false：framework-dependent single-file 不支持 bundle 压缩（NETSDK1176）。

为什么需要 `IncludeAllContentForSelfExtract=true`：Gemini 的 `AppBootstrapper.Configure` 通过 `Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", RecurseSubdirectories=true)` 来发现 MEF module 程序集，不主动走 `PublishSingleFileBypassAssemblies`。开启后所有 managed 程序集会被运行时解压到临时目录，且 `AppContext.BaseDirectory` 指向该临时目录，Gemini 才能扫描到它们。

### 3. 主程序集保留 + Resources 外置

PublishSingleFile 默认会把主程序集本身也打入 exe bundle，无法用 `ExcludeFromSingleFile` 排除。CommandLine 通过 `Assembly.Load(new AssemblyName("OngekiFumenEditor"))` 反射加载主程序集（详见 §4），因此主项目 publish 后必须保留独立的 `OngekiFumenEditor.dll`：

```xml
<Target Name="CopyMainAssemblyAfterPublish" AfterTargets="Publish">
    <Copy SourceFiles="$(IntermediateOutputPath)$(AssemblyName).dll"
          DestinationFolder="$(PublishDir)"
          SkipUnchangedFiles="true" />
</Target>
```

Resources 子目录（音色 wav、纹理 png、模板 ogkr 等）已通过 `<None Update ... CopyToOutputDirectory="PreserveNewest">` 复制到输出目录。`IncludeAllContentForSelfExtract=true` 会把这些 content 也打入 bundle，运行时再解压——但用户期望它们保留在 exe 旁的 publish 目录，便于查看与替换。需要在 `_ComputeFilesToBundle` 之前给它们打上 `ExcludeFromSingleFile=true`：

```xml
<Target Name="ExcludeResourcesFromSingleFile" BeforeTargets="_ComputeFilesToBundle">
    <ItemGroup>
        <ResolvedFileToPublish Update="@(ResolvedFileToPublish)"
            Condition="$([System.String]::Copy('%(ResolvedFileToPublish.RelativePath)').Replace('\','/').StartsWith('Resources/'))">
            <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
        </ResolvedFileToPublish>
    </ItemGroup>
</Target>
```

> 必须用 `BeforeTargets="_ComputeFilesToBundle"`：`_ComputeFilesToBundle` 会把所有非 `ExcludeFromSingleFile=true` 的 `ResolvedFileToPublish` 移出该 list 进入 `_FilesToBundle`，因此 `AfterTargets="ComputeFilesToPublish"` 已经来不及。

### 4. CommandLine.exe 启动逻辑

`OngekiFumenEditor.CommandLine.csproj` 通过 `<ProjectReference Include="..\OngekiFumenEditor\OngekiFumenEditor.csproj">` 引用主项目，使得编辑器及其传递依赖一并被打入 CommandLine.exe 的 single-file bundle；这样 `Assembly.Load(new AssemblyName("OngekiFumenEditor"))` 走默认 ALC 就能解析主程序集及其所有依赖，避免 `Assembly.LoadFile` 引入独立 ALC 后依赖无法解析。

```csharp
// OngekiFumenEditor.CommandLine/Program.cs
var assembly = Assembly.Load(new AssemblyName("OngekiFumenEditor"));
var appType = assembly.GetType("OngekiFumenEditor.App");
dynamic app = Activator.CreateInstance(appType, args: [false]);
app.InitializeComponent();
return app.Run();
```

`app.Run()` 内部 WPF 会用 `Environment.GetCommandLineArgs()` 填充 `StartupEventArgs.Args`，因此 `AppBootstrapper.OnStartupForCMD` 仍能拿到 `convert --inputFile ... --outputFile ...` 参数。

### 5. Logs / Dumps / Resources 路径

`IncludeAllContentForSelfExtract=true` 让 `AppContext.BaseDirectory` 指向运行时解压的临时目录，但 Logs/Dumps 这类用户面向的相对路径应该写到 exe 真实所在目录。新增 `OngekiFumenEditor/Utils/AppExecutableLocator.cs`：

```csharp
public static string ExecutableDirectory { get; } = ResolveExecutableDirectory();

public static string ResolveRelativeToExecutable(string path)
{
    if (string.IsNullOrWhiteSpace(path)) return ExecutableDirectory;
    if (Path.IsPathRooted(path)) return path;
    return Path.GetFullPath(Path.Combine(ExecutableDirectory, path));
}

private static string ResolveExecutableDirectory()
{
    var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
    if (!string.IsNullOrEmpty(mainModule))
        return Path.GetDirectoryName(mainModule) ?? AppContext.BaseDirectory;
    return AppContext.BaseDirectory;
}
```

调用点更新：

- `Utils/Logs/DefaultImpls/FileLogOutput.cs` — `LogSetting.Default.LogFileDirPath` 经 `AppExecutableLocator.ResolveRelativeToExecutable` 解析。
- `Utils/DeadHandler/DumpFileHelper.cs` — `ProgramSetting.Default.DumpFileDirPath` 同上。
- `App.xaml.cs` 启动时 `Directory.SetCurrentDirectory(AppExecutableLocator.ExecutableDirectory)`。
- `Kernel/Mcp/SkillResources.cs` `SkillsRootDirectory` 也改用 `AppExecutableLocator` 解析。

### 6. 验证结果

按 plan 命令两步 publish 至 `F:\ogkrEditorBuild\` 后，目录内容：

```
OngekiFumenEditor.exe
OngekiFumenEditor.CommandLine.exe
Resources/                 # 音色 / 纹理 / 模板等外置资源
```

`OngekiFumenEditor.CommandLine.exe convert --inputFile ... --outputFile ...` 成功生成 `20993_04.nyageki`（156261 字节，header 正常），运行期 Logs 写入 `F:\ogkrEditorBuild\Logs\`。
