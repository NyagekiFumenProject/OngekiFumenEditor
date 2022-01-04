using Gemini.Framework;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer
{
    public interface IFumenBulletPalleteListViewer : ITool
    {
        public OngekiFumen Fumen { get; set; }
    }
}
