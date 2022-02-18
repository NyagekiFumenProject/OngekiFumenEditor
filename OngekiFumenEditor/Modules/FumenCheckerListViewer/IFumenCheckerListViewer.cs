using Gemini.Framework;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer
{
    public interface IFumenCheckerListViewer : ITool
    {
        void RefreshCurrentFumen();
    }
}
