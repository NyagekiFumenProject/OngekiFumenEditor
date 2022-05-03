using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
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
        public string Name => GetType().GetTypeName();

        public override string ToString() => IDShortName;

        private string tag = string.Empty;
        /// <summary>
        /// 表示用户自定义的标签，一般用于脚本区分
        /// </summary>
        public string Tag
        {
            get => tag;
            set => Set(ref tag, value);
        }

        /// <summary>
        /// 复制物件参数和内容
        /// </summary>
        /// <param name="fromObj">复制源，本对象的仿制目标</param>
        public abstract void Copy(OngekiObjectBase fromObj, OngekiFumen fumen);
    }
}
