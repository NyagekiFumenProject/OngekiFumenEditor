using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
    public record Genre(string Name, int Id) : IEnumStruct
    {
        public string DisplayName => Name;
    }
}
