using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
    public record VersionID(string Name, int Id, string Title) : IEnumStruct
    {
        public string DisplayName => Name;
    }
}
