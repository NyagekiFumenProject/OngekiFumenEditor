using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public interface IOngekiObject : ISerializable
    {
        public string Group { get; }
        public string IDShortName { get; }
        public string Name { get; }
    }
}
