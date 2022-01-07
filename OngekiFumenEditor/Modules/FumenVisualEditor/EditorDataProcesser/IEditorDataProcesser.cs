using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.EditorDataProcesser
{
    public interface IEditorDataProcesser : ICommandParser
    {
        public string SerializeAll(OngekiFumen ongekiFumen);
    }
}
