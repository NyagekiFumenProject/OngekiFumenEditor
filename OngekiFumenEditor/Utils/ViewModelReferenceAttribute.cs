using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public class ViewModelReferenceAttribute : Attribute
    {
        public Type ViewModelType { get; set; }
    }
}
