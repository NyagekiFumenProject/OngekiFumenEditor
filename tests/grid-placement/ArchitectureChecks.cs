using System;
using System.Linq;
using System.Reflection;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl;

internal static class ArchitectureChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var assembly = typeof(FumenVisualEditorViewModel).Assembly;
        foreach (var name in new[] {
            "OngekiFumenEditor.Modules.FumenVisualEditor.Base.GridClipboardGeometry",
            "OngekiFumenEditor.Modules.FumenVisualEditor.Base.LaneGeometry",
            "OngekiFumenEditor.Modules.FumenVisualEditor.Base.LaneSwitchResolver" })
        {
            var type = assembly.GetType(name, true);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var dependencies = type.GetFields(flags).Select(x => x.FieldType)
                .Concat(type.GetMethods(flags).SelectMany(x => x.GetParameters()).Select(x => x.ParameterType))
                .Concat(type.GetConstructors(flags).SelectMany(x => x.GetParameters()).Select(x => x.ParameterType));
            check(dependencies.All(t => !t.ToString().Contains("ViewModels") && !t.ToString().Contains("System.Windows")), name + " 不依赖VM或WPF输入");
        }
        var actionType = typeof(OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.ObjectInteractiveActionBase);
        check(!actionType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Any(x => x.IsAbstract && x.IsAssembly),
            "公开交互基类不要求外部派生类实现不可访问的抽象成员");
        check(typeof(FumenVisualEditorViewModel).GetNestedType("SelectionGridDrag", BindingFlags.NonPublic) == null,
            "VM不再拥有群体拖动策略及快照类型");
    }
}
