using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.ProgramProfile.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.ProgramProfile
{
    public interface IEditorProfile
    {
        IProfileHandle BeginEditorPlayProfile(FumenVisualEditorViewModel editor);
        void Tick(IProfileHandle profileHandle);
        void EndEditorPlayProfile(IProfileHandle handle);
    }
}
