using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
    public interface IEditorDropHandler
    {
        void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint);
    }
}
