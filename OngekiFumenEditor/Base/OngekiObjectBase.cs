using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiObjectBase : PropertyChangedBase
    {
        private static int ID_GEN = 0;

        [ObjectPropertyBrowserReadOnly]
        public int Id { get; init; } = ID_GEN++;

        [ObjectPropertyBrowserHide]
        public abstract string IDShortName { get; }

        [ObjectPropertyBrowserHide]
        public string Name => GetType().GetTypeName();

        public override string ToString() => $"{{{IDShortName}}} OID[{Id}]";

        [ObjectPropertyBrowserHide]
        public override bool IsNotifying
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => base.IsNotifying;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base.IsNotifying = value;
        }

        private string tag = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        [ObjectPropertyBrowserTipText("表示用户自己自定义的标签，一般用于脚本区分")]
        public string Tag
        {
            get => tag;
            set => Set(ref tag, value);
        }

        /// <summary>
        /// 复制物件参数和内容
        /// </summary>
        /// <param name="fromObj">复制源，本对象的仿制目标</param>
        public abstract void Copy(OngekiObjectBase fromObj);

        public OngekiObjectBase CopyNew()
        {
            if (this is not IDisplayableObject displayable
                //暂不支持 以下类型的复制粘贴
                //|| obj is ConnectableObjectBase
                )
                return default;

            var newObj = CacheLambdaActivator.CreateInstance(GetType()) as OngekiObjectBase;
            newObj.Copy(this);
            return newObj;
        }
    }
}
