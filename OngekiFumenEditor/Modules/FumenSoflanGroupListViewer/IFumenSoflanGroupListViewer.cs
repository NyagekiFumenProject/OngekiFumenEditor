using Gemini.Framework;
using OngekiFumenEditor.Base.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public interface IFumenSoflanGroupListViewer : ITool
    {
        SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem { get; }
        SoflanGroupWrapItem CurrentSoflansDisplaySoflanGroupWrapItem { get; }
    }
}
