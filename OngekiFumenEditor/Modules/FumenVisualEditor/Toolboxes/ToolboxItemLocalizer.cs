using System.ComponentModel.Composition;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolboxes
{
    /// <summary>
    /// Localizes the toolbox item descriptions: the generators store a resource key
    /// (e.g. "ToolboxItemBell") and this hook resolves it through the editor resources.
    /// </summary>
    [Export(typeof(IToolboxItemLocalizer))]
    public class ToolboxItemLocalizer : IToolboxItemLocalizer
    {
        public string LocalizeDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return description;

            return Resources.ResourceManager.GetString(description) ?? description;
        }
    }
}
