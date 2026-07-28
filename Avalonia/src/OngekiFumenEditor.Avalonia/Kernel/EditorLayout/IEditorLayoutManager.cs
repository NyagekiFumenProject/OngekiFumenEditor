namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout;

public interface IEditorLayoutManager
{
    Task<bool> SaveLayout(Stream outputLayoutDataStream);
    Task<bool> LoadLayout(Stream intputLayoutDataStream);

    Task<bool> ApplyDefaultSuggestEditorLayout();
}
