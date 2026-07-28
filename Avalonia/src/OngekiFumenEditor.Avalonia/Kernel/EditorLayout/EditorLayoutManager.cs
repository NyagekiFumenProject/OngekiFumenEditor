using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout;

[RegisterSingleton<IEditorLayoutManager>]
public class EditorLayoutManager : IEditorLayoutManager
{
    public async Task<bool> LoadLayout(Stream intputLayoutDataStream)
    {
        if (intputLayoutDataStream is not null)
            Log.LogWarning("LoadLayout(Stream) currently delegates to shell built-in layout loader and ignores stream payload.");

        var shell = IoC.Get<IShell>();
        return await shell.LoadLayout();
    }

    public async Task<bool> SaveLayout(Stream outputLayoutDataStream)
    {
        var shell = IoC.Get<IShell>();
        var result = await shell.SaveLayout();
        if (result && outputLayoutDataStream is not null)
        {
            // Keep API compatibility with WPF signature.
            await outputLayoutDataStream.FlushAsync();
        }

        return result;
    }

    public Task<bool> ApplyDefaultSuggestEditorLayout()
    {
        return IoC.Get<IShell>().LoadLayout();
    }
}
