using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiObjectBase : PropertyChangedBase
    {
        public abstract string IDShortName { get; }

        public override string ToString() => IDShortName;

        /// <summary>
        /// 复制物件参数和内容
        /// </summary>
        /// <param name="fromObj">复制源，本对象的仿制目标</param>
        public abstract void Copy(OngekiObjectBase fromObj, OngekiFumen fumen);
    }
}
