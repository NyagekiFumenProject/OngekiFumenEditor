using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models
{
    public class EditorSetting : PropertyChangedBase
    {
        private double soundOffset = 240;
        public double SoundOffset
        {
            get => soundOffset;
            set => Set(ref soundOffset, value);
        }

        private double judgeLineOffsetY = 0;
        public double JudgeLineOffsetY
        {
            get => judgeLineOffsetY;
            set => Set(ref judgeLineOffsetY, value);
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
        /// <summary>
        /// 表示物件或者其他在X轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
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
        /// <summary>
        /// 表示物件或者其他在时间轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
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
        /// <summary>
        /// X轴上单位线间距大小
        /// </summary>
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
        /// <summary>
        /// 时间轴上单位线间距大小
        /// </summary>
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
        /// <summary>
        /// 时间轴上单位线划分密度
        /// </summary>
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

        private float mouseWheelTimelineSpeed = 4f;
        /// <summary>
        /// 鼠标滚轮移动时间轴速度倍率
        /// </summary>
        public float MouseWheelTimelineSpeed
        {
            get
            {
                return mouseWheelTimelineSpeed;
            }
            set
            {
                mouseWheelTimelineSpeed = value;
                NotifyOfPropertyChange(() => MouseWheelTimelineSpeed);
            }
        }

        private int xGridMaxUnit = 28;
        /// <summary>
        /// 横轴长度
        /// </summary>
        public int XGridMaxUnit
        {
            get
            {
                return xGridMaxUnit;
            }
            set
            {
                xGridMaxUnit = value;
                NotifyOfPropertyChange(() => XGridMaxUnit);
            }
        }
    }
}
