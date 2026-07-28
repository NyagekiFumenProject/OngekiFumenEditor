using CommunityToolkit.Mvvm.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects
{
    public abstract class SoflanGroupDisplayItemListViewBase : ObservableObject
    {
        private SoflanGroupWrapItemGroup parent;
        public SoflanGroupWrapItemGroup Parent
        {
            get => parent;
            set
            {
                SetProperty(ref parent, value);
                Refresh();
            }
        }

        public abstract string DisplayName { get; set; }

        private int level;
        public int Level
        {
            get => level;
            set => SetProperty(ref level, value);
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

