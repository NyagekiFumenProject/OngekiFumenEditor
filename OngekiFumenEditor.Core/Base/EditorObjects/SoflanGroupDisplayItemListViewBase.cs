using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Base.EditorObjects
{
    public abstract class SoflanGroupDisplayItemListViewBase : PropertyChangedBase
    {
        private SoflanGroupWrapItemGroup parent;
        public SoflanGroupWrapItemGroup Parent
        {
            get => parent;
            set
            {
                Set(ref parent, value);
                Refresh();
            }
        }

        public abstract string DisplayName { get; set; }

        private int level;
        public int Level
        {
            get => level;
            set => Set(ref level, value);
        }

        /// <summary>
        /// 是否在制谱器设计模式绘制
        /// </summary>
        public abstract bool IsDisplayInDesignMode { get; set; }
        /// <summary>
        /// 是否在制谱器预览模式绘制
        /// </summary>
        public abstract bool IsDisplayInPreviewMode { get; set; }
    }
}
