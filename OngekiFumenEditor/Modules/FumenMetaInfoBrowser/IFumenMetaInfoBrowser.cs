using Gemini.Framework;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser
{
    public interface IFumenMetaInfoBrowser : ITool
    {
        public OngekiFumen Fumen { get; set; }
    }
}
