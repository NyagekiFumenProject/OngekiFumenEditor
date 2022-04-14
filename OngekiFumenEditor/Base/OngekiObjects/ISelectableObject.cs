using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public interface ISelectableObject
    {
        bool IsSelected { get; set; }
    }
}
