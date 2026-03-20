using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(EditorTools))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorTools
    {
        private readonly IRuntimeEditorContextProvider editorContextProvider;

        [ImportingConstructor]
        public EditorTools(IRuntimeEditorContextProvider editorContextProvider)
        {
            this.editorContextProvider = editorContextProvider;
        }

        [McpServerTool(Name = "editor.get_current", Title = "Get Current Editor", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get the currently active editor in the running Ongeki Fumen Editor instance.")]
        public EditorContextInfo GetCurrent()
        {
            return editorContextProvider.GetCurrentEditor();
        }

        [McpServerTool(Name = "editor.list_opened", Title = "List Opened Editors", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("List all currently opened editors in the running Ongeki Fumen Editor instance.")]
        public IReadOnlyList<EditorContextInfo> ListOpened()
        {
            return editorContextProvider.GetOpenedEditors();
        }

        [McpServerTool(Name = "editor.get_current_summary", Title = "Get Current Editor Summary", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get a lightweight summary of the currently active editor, including file paths and object counts.")]
        public object GetCurrentSummary()
        {
            var current = editorContextProvider.GetCurrentEditor();
            if (current is null)
            {
                return new
                {
                    success = false,
                    errorCode = "NO_ACTIVE_EDITOR"
                };
            }

            return new
            {
                success = true,
                editorId = current.EditorId,
                displayName = current.DisplayName,
                projectPath = current.ProjectPath,
                fumenPath = current.FumenPath,
                isDirty = current.IsDirty,
                isActive = current.IsActive,
                counts = new
                {
                    lanes = current.LaneCount,
                    taps = current.TapCount,
                    holds = current.HoldCount,
                    bells = current.BellCount,
                    bullets = current.BulletCount,
                    bpmChanges = current.BpmChangeCount,
                    soflans = current.SoflanCount,
                }
            };
        }
    }
}
