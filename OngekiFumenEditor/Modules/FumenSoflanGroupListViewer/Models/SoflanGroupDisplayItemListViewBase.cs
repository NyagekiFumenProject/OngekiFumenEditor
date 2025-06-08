using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
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

        public abstract bool IsSelected { get; set; }
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
