using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils;

/// <summary>
///     Desktop 侧的本地路径文档打开分发。谱面编辑器的普通项目打开走其 Provider 自身的
///     文件选择流程，不在此处按路径构造上下文；FastOpen(.ogkr/.nyageki) 的打开由
///     Desktop FastOpen 服务单独提供。
/// </summary>
public static class DesktopDocumentOpenService
{
    public static async Task<bool> TryOpenAsync(string filePath)
    {
        var provider = PickEditorProvider(filePath);
        if (provider is not null)
        {
            if (provider is IFumenVisualEditorProvider)
                return false;

            Log.LogInfo($"Open document by provider {provider.GetType().Name}: {filePath}");
            var document = provider.Create();
            var shouldShow = await provider.TryOpen(document);

            if (shouldShow)
            {
                await IoC.Get<IShell>().OpenDocumentAsync(document);
                return true;
            }
        }

        return false;
    }

    private static IEditorProvider PickEditorProvider(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IoC.GetAll<IEditorProvider>().FirstOrDefault(x => x.FileTypes.Any(t =>
            (t.Patterns ?? []).Any(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))));
    }
}
