using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public class EditorSetting : PropertyChangedBase
    {
        private TGrid currentDisplayTimePosition = new TGrid(4, 500);
        /// <summary>
        /// 表示当前显示物件的时间
        /// </summary>
        public TGrid CurrentDisplayTimePosition
        {
            get
            {
                return currentDisplayTimePosition;
            }
            set
            {
                currentDisplayTimePosition = value;
                NotifyOfPropertyChange(() => CurrentDisplayTimePosition);
            }
        }

        public string editorDisplayName;
        public string EditorDisplayName
        {
            get
            {
                return
                  editorDisplayName;
            }
            set
            {
                editorDisplayName = value;
                NotifyOfPropertyChange(() => EditorDisplayName);
            }
        }

        private bool isPreventXAutoClose;
        public bool IsPreventXAutoClose
        {
            get
            {
                return isPreventXAutoClose;
            }
            set
            {
                isPreventXAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private bool isPreventTimelineAutoClose;
        public bool IsPreventTimelineAutoClose
        {
            get
            {
                return isPreventTimelineAutoClose;
            }
            set
            {
                isPreventTimelineAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private double unitCloseSize = 4;
        public double UnitCloseSize
        {
            get
            {
                return unitCloseSize;
            }
            set
            {
                unitCloseSize = value;
                NotifyOfPropertyChange(() => UnitCloseSize);
            }
        }

        private int baseLineY = 50;
        public int BaseLineY
        {
            get
            {
                return baseLineY;
            }
            set
            {
                baseLineY = value;
                NotifyOfPropertyChange(() => BaseLineY);
            }
        }

        private int beatSplit = 1;
        public int BeatSplit
        {
            get
            {
                return beatSplit;
            }
            set
            {
                beatSplit = value;
                NotifyOfPropertyChange(() => BeatSplit);
            }
        }
    }
}
