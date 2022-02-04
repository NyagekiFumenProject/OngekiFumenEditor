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
        private double soundOffset = 200;
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

        private bool disableXGridMagneticDock;
        /// <summary>
        /// 表示物件或者其他在X轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
        public bool DisableXGridMagneticDock
        {
            get => disableXGridMagneticDock;
            set => Set(ref disableXGridMagneticDock, value);
        }

        private bool forceMagneticDock = false;
        public bool ForceMagneticDock
        {
            get => forceMagneticDock;
            set => Set(ref forceMagneticDock, value);
        }

        private bool disableTGridMagneticDock;
        /// <summary>
        /// 表示物件或者其他在时间轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
        public bool DisableTGridMagneticDock
        {
            get => disableTGridMagneticDock;
            set => Set(ref disableTGridMagneticDock, value);
        }

        private double xGridUnitSpace = 4;
        /// <summary>
        /// X轴上单位线间距大小
        /// </summary>
        public double XGridUnitSpace
        {
            get => xGridUnitSpace;
            set => Set(ref xGridUnitSpace, value);
        }

        private int tGridUnitLength = 50;
        /// <summary>
        /// 时间轴上单位线间距大小
        /// </summary>
        public int TGridUnitLength
        {
            get
            {
                return tGridUnitLength;
            }
            set
            {
                tGridUnitLength = value;
                NotifyOfPropertyChange(() => TGridUnitLength);
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

        private int xGridDisplayMaxUnit = 28;
        /// <summary>
        /// 横轴长度
        /// </summary>
        public int XGridDisplayMaxUnit
        {
            get
            {
                return xGridDisplayMaxUnit;
            }
            set
            {
                xGridDisplayMaxUnit = value;
                NotifyOfPropertyChange(() => XGridDisplayMaxUnit);
            }
        }
    }
}
