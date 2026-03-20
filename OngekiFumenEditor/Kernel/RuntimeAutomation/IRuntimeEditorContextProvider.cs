using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IRuntimeEditorContextProvider
    {
        EditorContextInfo GetCurrentEditor();

        EditorContextInfo GetEditor(string editorId);

        IReadOnlyList<EditorContextInfo> GetOpenedEditors();
    }
}
