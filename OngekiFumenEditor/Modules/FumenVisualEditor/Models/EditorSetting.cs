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
        private void Save()
        {
            Properties.EditorGlobalSetting.Default.Save();
        }

        public double JudgeLineOffsetY
        {
            get => Properties.EditorGlobalSetting.Default.JudgeLineOffsetY;
            set
            {
                Properties.EditorGlobalSetting.Default.JudgeLineOffsetY = value;
                Save();
                NotifyOfPropertyChange(() => JudgeLineOffsetY);
            }
        }

        /// <summary>
        /// 表示物件或者其他在X轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
        public bool DisableXGridMagneticDock
        {
            get => Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock;
            set
            {
                Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock = value;
                Save();
                NotifyOfPropertyChange(() => DisableXGridMagneticDock);
            }
        }

        public bool ForceMagneticDock
        {
            get => Properties.EditorGlobalSetting.Default.ForceMagneticDock;
            set
            {
                Properties.EditorGlobalSetting.Default.ForceMagneticDock = value;
                Save();
                NotifyOfPropertyChange(() => ForceMagneticDock);
            }
        }

        public bool ForceTapHoldMagneticDockToLane
        {
            get => Properties.EditorGlobalSetting.Default.ForceTapHoldMagneticDockToLane;
            set
            {
                Properties.EditorGlobalSetting.Default.ForceTapHoldMagneticDockToLane = value;
                Save();
                NotifyOfPropertyChange(() => ForceTapHoldMagneticDockToLane);
            }
        }

        /// <summary>
        /// 表示物件或者其他在时间轴上移动时，是否可以自动吸附到最近的单位线上
        /// </summary>
        public bool DisableTGridMagneticDock
        {
            get => Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock;
            set
            {
                Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock = value;
                Save();
                NotifyOfPropertyChange(() => DisableTGridMagneticDock);
            }
        }

        /// <summary>
        /// X轴上单位线间距大小
        /// </summary>
        public double XGridUnitSpace
        {
            get => Properties.EditorGlobalSetting.Default.XGridUnitSpace;
            set
            {
                Properties.EditorGlobalSetting.Default.XGridUnitSpace = value;
                Save();
                NotifyOfPropertyChange(() => XGridUnitSpace);
            }
        }

        /// <summary>
        /// 时间轴上单位线间距大小
        /// </summary>
        public int TGridUnitLength
        {
            get => Properties.EditorGlobalSetting.Default.TGridUnitLength;
            set
            {
                Properties.EditorGlobalSetting.Default.TGridUnitLength = value;
                Save();
                NotifyOfPropertyChange(() => TGridUnitLength);
            }
        }


        /// <summary>
        /// 时间轴上单位线划分密度
        /// </summary>
        public int BeatSplit
        {
            get => Properties.EditorGlobalSetting.Default.BeatSplit;
            set
            {
                Properties.EditorGlobalSetting.Default.BeatSplit = value;
                Save();
                NotifyOfPropertyChange(() => BeatSplit);
            }
        }

        /// <summary>
        /// 横轴长度
        /// </summary>
        public int XGridDisplayMaxUnit
        {
            get => Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit;
            set
            {
                Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit = value;
                Save();
                NotifyOfPropertyChange(() => XGridDisplayMaxUnit);
            }
        }

        public bool ForceXGridMagneticDock
        {
            get => Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock;
            set
            {
                Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock = value;
                Save();
                NotifyOfPropertyChange(() => ForceXGridMagneticDock);
            }
        }

        public double VerticalDisplayScale
        {
            get => Properties.EditorGlobalSetting.Default.VerticalDisplayScale;
            set
            {
                Properties.EditorGlobalSetting.Default.VerticalDisplayScale = value;
                Save();
                NotifyOfPropertyChange(() => VerticalDisplayScale);
            }
        }
    }
}
