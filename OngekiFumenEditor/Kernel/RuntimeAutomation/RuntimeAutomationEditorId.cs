using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal static class RuntimeAutomationEditorId
    {
        public static string Generate(FumenVisualEditorViewModel editor)
        {
            return editor is null ? string.Empty : $"editor-{RuntimeHelpers.GetHashCode(editor):x8}";
        }
    }
}
