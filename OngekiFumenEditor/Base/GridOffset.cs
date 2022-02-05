using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public sealed record GridOffset(float Unit, int Grid)
    {
        public static GridOffset Zero { get; } = new GridOffset(0, 0);

        public int TotalGrid(uint gridRadix) => (int)(Unit * gridRadix + Grid);
    }
}
