using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Toolbox.Models;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public interface IToolboxGenerator
{
    OngekiObjectBase CreateDisplayObject();
}

public abstract class ToolboxGenerator : ToolboxItem<FumenVisualEditorViewModel>, IToolboxGenerator
{
    protected ToolboxGenerator(string name, string category)
    {
        Name = LocalizedString.CreateFromRawText(name);
        Category = LocalizedString.CreateFromRawText(category);
        CategoryGroupId = category;
    }

    public override LocalizedString Name { get; }

    public override LocalizedString Category { get; }

    public override string CategoryGroupId { get; }

    public override Uri IconSource => default!;

    public abstract OngekiObjectBase CreateDisplayObject();
}

public abstract class ToolboxGenerator<T> : ToolboxGenerator where T : OngekiObjectBase, new()
{
    protected ToolboxGenerator(string name, string category) : base(name, category)
    {
    }

    public override OngekiObjectBase CreateDisplayObject()
    {
        return new T();
    }
}
