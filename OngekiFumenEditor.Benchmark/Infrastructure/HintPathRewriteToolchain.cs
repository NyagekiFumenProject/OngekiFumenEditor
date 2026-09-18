using System.IO;
using System.Xml.Linq;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Toolchains.Results;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

/// <summary>
/// 自定义 BDN Toolchain: 复用默认 CsProjCoreToolchain 的 Builder/Executor,
/// 只把 wrapper csproj 里的 ProjectReference 链替换为 Reference HintPath,
/// 让 wrapper 不再重建 Caliburn.Micro.Core / OngekiFumenEditor 等 dll —
/// 从源头避开 WPF + Fody Costura 多层 ProjectReference 引起的 CSC CS2012 文件锁。
/// </summary>
internal static class HintPathRewriteToolchain
{
    public static IToolchain Create(string targetFrameworkMoniker, string hostBinDirectory)
    {
        var settings = new NetCoreAppSettings(
            targetFrameworkMoniker: targetFrameworkMoniker,
            runtimeFrameworkVersion: null,
            name: targetFrameworkMoniker);

        var baseToolchain = CsProjCoreToolchain.From(settings);
        return new HintPathToolchainWrapper(
            "HintPathRewrite",
            new HintPathRewriteGenerator(baseToolchain.Generator, hostBinDirectory),
            baseToolchain.Builder,
            baseToolchain.Executor);
    }
}

internal sealed class HintPathToolchainWrapper : Toolchain
{
    public HintPathToolchainWrapper(string name, IGenerator generator, IBuilder builder, IExecutor executor)
        : base(name, generator, builder, executor)
    {
    }
}

internal sealed class HintPathRewriteGenerator : IGenerator
{
    private readonly IGenerator inner;
    private readonly string hostBinDirectory;

    public HintPathRewriteGenerator(IGenerator inner, string hostBinDirectory)
    {
        this.inner = inner;
        this.hostBinDirectory = hostBinDirectory;
    }

    public GenerateResult GenerateProject(BuildPartition buildPartition, ILogger logger, string rootArtifactsFolderPath)
    {
        var result = inner.GenerateProject(buildPartition, logger, rootArtifactsFolderPath);
        if (!result.IsGenerateSuccess)
            return result;

        try
        {
            RewriteCsproj(result.ArtifactsPaths.ProjectFilePath, logger);
        }
        catch (Exception ex)
        {
            logger.WriteLineError($"[HintPathRewrite] Failed to rewrite wrapper csproj: {ex}");
            return GenerateResult.Failure(result.ArtifactsPaths, new[] { ex.ToString() });
        }

        return result;
    }

    private void RewriteCsproj(string csprojPath, ILogger logger)
    {
        if (!File.Exists(csprojPath))
        {
            logger.WriteLineError($"[HintPathRewrite] Wrapper csproj not found: {csprojPath}");
            return;
        }

        var doc = XDocument.Load(csprojPath);
        var root = doc.Root ?? throw new InvalidOperationException("Wrapper csproj has no root element.");
        var ns = root.Name.Namespace;

        // 1. 删除所有 <ProjectReference> 节点(默认 Generator 写入的"重建主项目"指令)
        root.Descendants(ns + "ProjectReference").Remove();

        // 2. 给 wrapper 启用 WPF + 附加 AspNetCore framework reference。
        //    - UseWPF: 主 Benchmark 依赖 OngekiFumenEditor.App (Application 子类),
        //      子进程 JIT 加载 App 时需要 PresentationFramework.dll。
        //    - FrameworkReference Microsoft.AspNetCore.App: 主项目 runtimeconfig 含此 framework
        //      (transitively 经某些 NuGet 引入,如 Microsoft.CodeAnalysis.Workspaces.MSBuild),
        //      MEF AssemblyCatalog.GetTypes() 扫描时若缺这个 framework 会抛
        //      ReflectionTypeLoadException 链(Microsoft.Extensions.Options / AspNetCore.Authentication 等)。
        var firstPropertyGroup = root.Elements(ns + "PropertyGroup").FirstOrDefault();
        if (firstPropertyGroup is not null)
            firstPropertyGroup.Add(new XElement(ns + "UseWPF", "true"));

        root.Add(new XElement(ns + "ItemGroup",
            new XElement(ns + "FrameworkReference", new XAttribute("Include", "Microsoft.AspNetCore.App"))));

        // 3. 注入 <Reference Include="<name>"><HintPath>...</HintPath><Private>true</Private></Reference>
        //    枚举主 Benchmark bin 目录下的全部 *.dll(主项目 build 时已经把所有 transitive 依赖
        //    平铺到这里),让 wrapper 直接 link 已有 dll 而非重新编译。
        if (!Directory.Exists(hostBinDirectory))
        {
            logger.WriteLineError($"[HintPathRewrite] Host bin directory not found: {hostBinDirectory}. "
                + "请先 `dotnet build` 主 Benchmark 项目。");
            return;
        }

        var refGroup = new XElement(ns + "ItemGroup");
        var added = 0;
        foreach (var dll in Directory.GetFiles(hostBinDirectory, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            refGroup.Add(new XElement(ns + "Reference",
                new XAttribute("Include", name),
                new XElement(ns + "HintPath", dll),
                new XElement(ns + "Private", "true")));   // 让 wrapper bin 复制 dll 供子进程加载
            added++;
        }

        root.Add(refGroup);
        doc.Save(csprojPath);

        logger.WriteLine($"[HintPathRewrite] Rewrote {csprojPath}: removed ProjectReferences, added {added} HintPath Reference(s).");
    }
}
