using System.IO;

namespace OngekiFumenEditor.FumenCheckerTests;

/// <summary>按 AppContext.BaseDirectory 逐级向上找到仓库里的样例谱面目录（找不到就返回空）。</summary>
internal static class SampleCorpus
{
    public static string[] FindCharts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "OngekiFumenEditor.Benchmark", "Data", "FumenSamples");
            if (Directory.Exists(candidate))
                return Directory.GetFiles(candidate, "*.ogkr");
        }

        return Array.Empty<string>();
    }
}
