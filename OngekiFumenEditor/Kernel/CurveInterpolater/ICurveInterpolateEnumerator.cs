using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.CurveInterpolater
{
    public interface ICurveInterpolateEnumerator
    {
        CurvePoint? EnumerateNext();
        void PushBack(CurvePoint point);
    }
}
