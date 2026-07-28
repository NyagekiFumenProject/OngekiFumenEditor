using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using Gekimini.Avalonia.Framework;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer
{
    public interface IFumenSoflanGroupListViewer : IToolViewModel
    {
        SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem { get; }
        SoflanGroupWrapItem CurrentSoflansDisplaySoflanGroupWrapItem { get; }
    }
}


