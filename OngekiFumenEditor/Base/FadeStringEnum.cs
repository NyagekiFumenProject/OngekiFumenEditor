using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class FadeStringEnum
    {
        public string Value { get; }
        public FadeStringEnum(string value) => Value = value;

        public static implicit operator string(FadeStringEnum s)
        {
            return s.Value;
        }
    }
}
