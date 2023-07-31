using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
    public interface IEnumStruct
    {
        string DisplayName { get; }
        int Id { get; }
    }
}
