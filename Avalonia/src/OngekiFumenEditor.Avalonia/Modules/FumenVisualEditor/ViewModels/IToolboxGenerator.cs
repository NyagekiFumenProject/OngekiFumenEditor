using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public interface IToolboxGenerator
{
    OngekiObjectBase CreateDisplayObject();
}

public abstract class ToolboxGenerator : IToolboxGenerator
{
    public abstract OngekiObjectBase CreateDisplayObject();
}

public class ToolboxGenerator<T> : ToolboxGenerator where T : OngekiObjectBase, new()
{
    public override OngekiObjectBase CreateDisplayObject()
    {
        return new T();
    }
}
