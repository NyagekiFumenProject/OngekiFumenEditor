using System.Windows.Input;
using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.AddObject;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : PersistedDocument
{
    private void RunCommand(CommandDefinition def)
    {
        new TargetableCommand(new(def)).Execute(null);
    }
    
    public void Brush_AddNewLaneLeft()
        => RunCommand(new AddLaneLeftCommandDefinition());
    public void Brush_AddNewLaneCenter()
        => RunCommand(new AddLaneCenterCommandDefinition());
    public void Brush_AddNewLaneRight()
        => RunCommand(new AddLaneRightCommandDefinition());
    public void Brush_AddNewWallLeft()
        => RunCommand(new AddWallLeftCommandDefinition());
    public void Brush_AddNewWallRight()
        => RunCommand(new AddWallRightCommandDefinition());
}