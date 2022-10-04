using OngekiFumenEditor.Base.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public interface ISelectableObject
    {
        bool IsSelected { get; set; }
    }
}
