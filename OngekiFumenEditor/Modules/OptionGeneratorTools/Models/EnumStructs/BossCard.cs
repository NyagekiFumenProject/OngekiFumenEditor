using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
    public record BossCard(string Name, int Id, BossAttritude Attritude, Rarity Rarity, string FilePath) : IEnumStruct
    {
        public string DisplayName => Name;
    }
}
