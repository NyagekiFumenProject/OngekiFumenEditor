using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
    public class EditorSetting : ObservableObject, IDisposable
    {
        public EditorSetting()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        private async void RequestSave()
        {
            if (isRequestSave)
                return;
            isRequestSave = true;
            await Task.Delay(2000);
            Properties.EditorGlobalSetting.Default.Save();
            isRequestSave = false;
        }

        private double judgeLineOffsetY = Properties.EditorGlobalSetting.Default.JudgeLineOffsetY;
        public double JudgeLineOffsetY
        {
            get => judgeLineOffsetY;
            set
            {
                judgeLineOffsetY = Properties.EditorGlobalSetting.Default.JudgeLineOffsetY = value;
                RequestSave();
                OnPropertyChanged(nameof(JudgeLineOffsetY));
            }
        }

        private bool disableXGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock;
        /// <summary>
        /// 琛ㄧず鐗╀欢鎴栬€呭叾浠栧湪X杞翠笂绉诲姩鏃讹紝鏄惁鍙互鑷姩鍚搁檮鍒版渶杩戠殑鍗曚綅绾夸笂
        /// </summary>
        public bool DisableXGridMagneticDock
        {
            get => disableXGridMagneticDock;
            set
            {
                disableXGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock = value;
                RequestSave();
                OnPropertyChanged(nameof(DisableXGridMagneticDock));
            }
        }

        private bool forceMagneticDock = Properties.EditorGlobalSetting.Default.ForceMagneticDock;
        public bool ForceMagneticDock
        {
            get => forceMagneticDock;
            set
            {
                forceMagneticDock = Properties.EditorGlobalSetting.Default.ForceMagneticDock = value;
                RequestSave();
                OnPropertyChanged(nameof(ForceMagneticDock));
            }
        }

        private bool forceTapHoldMagneticDockToLane = Properties.EditorGlobalSetting.Default.ForceTapHoldMagneticDockToLane;
        public bool ForceTapHoldMagneticDockToLane
        {
            get => forceTapHoldMagneticDockToLane;
            set
            {
                forceTapHoldMagneticDockToLane = Properties.EditorGlobalSetting.Default.ForceTapHoldMagneticDockToLane = value;
                RequestSave();
                OnPropertyChanged(nameof(ForceTapHoldMagneticDockToLane));
            }
        }

        private bool judgeLineAlignBeat = Properties.EditorGlobalSetting.Default.JudgeLineAlignBeat;
        public bool JudgeLineAlignBeat
        {
            get => judgeLineAlignBeat;
            set
            {
                judgeLineAlignBeat = Properties.EditorGlobalSetting.Default.JudgeLineAlignBeat = value;
                RequestSave();
                OnPropertyChanged(nameof(JudgeLineAlignBeat));
            }
        }

        private bool disableTGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock;
        /// <summary>
        /// 琛ㄧず鐗╀欢鎴栬€呭叾浠栧湪鏃堕棿杞翠笂绉诲姩鏃讹紝鏄惁鍙互鑷姩鍚搁檮鍒版渶杩戠殑鍗曚綅绾夸笂
        /// </summary>
        public bool DisableTGridMagneticDock
        {
            get => disableTGridMagneticDock;
            set
            {
                disableTGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock = value;
                RequestSave();
                OnPropertyChanged(nameof(DisableTGridMagneticDock));
            }
        }

        private bool enableXOffset = Properties.EditorGlobalSetting.Default.EnableXOffset;
        public bool EnableXOffset
        {
            get => enableXOffset;
            set
            {
                enableXOffset = Properties.EditorGlobalSetting.Default.EnableXOffset = value;
                RequestSave();
                OnPropertyChanged(nameof(EnableXOffset));
            }
        }

        private double xOffset = Properties.EditorGlobalSetting.Default.XOffset;
        /// <summary>
        /// X杞翠笂鍗曚綅绾块棿璺濆ぇ灏?
        /// </summary>
        public double XOffset
        {
            get => EnableXOffset ? xOffset : 0;
            set
            {
                xOffset = Properties.EditorGlobalSetting.Default.XOffset = value;
                RequestSave();
                OnPropertyChanged(nameof(XOffset));
            }
        }

        private double xGridUnitSpace = Properties.EditorGlobalSetting.Default.XGridUnitSpace;
        /// <summary>
        /// X杞翠笂鍗曚綅绾块棿璺濆ぇ灏?
        /// </summary>
        public double XGridUnitSpace
        {
            get => xGridUnitSpace;
            set
            {
                xGridUnitSpace = Properties.EditorGlobalSetting.Default.XGridUnitSpace = value;
                RequestSave();
                OnPropertyChanged(nameof(XGridUnitSpace));
            }
        }

        private int beatSplit = Properties.EditorGlobalSetting.Default.BeatSplit;
        /// <summary>
        /// 鏃堕棿杞翠笂鍗曚綅绾垮垝鍒嗗瘑搴?
        /// </summary>
        public int BeatSplit
        {
            get => beatSplit;
            set
            {
                beatSplit = Properties.EditorGlobalSetting.Default.BeatSplit = value;
                RequestSave();
                OnPropertyChanged(nameof(BeatSplit));
            }
        }

        private int xGridDisplayMaxUnit = Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit;
        /// <summary>
        /// 妯酱闀垮害
        /// </summary>
        public int XGridDisplayMaxUnit
        {
            get => xGridDisplayMaxUnit;
            set
            {
                xGridDisplayMaxUnit = Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit = value;
                RequestSave();
                OnPropertyChanged(nameof(XGridDisplayMaxUnit));
            }
        }

        private bool forceXGridMagneticDock = Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock;
        public bool ForceXGridMagneticDock
        {
            get => forceXGridMagneticDock;
            set
            {
                forceXGridMagneticDock = Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock = value;
                RequestSave();
                OnPropertyChanged(nameof(ForceXGridMagneticDock));
            }
        }

        private bool showXOffsetScrollBar = Properties.EditorGlobalSetting.Default.ShowXOffsetScrollBar;
        public bool ShowXOffsetScrollBar
        {
            get => showXOffsetScrollBar;
            set
            {
                showXOffsetScrollBar = Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock = value;
                RequestSave();
                OnPropertyChanged(nameof(ShowXOffsetScrollBar));
            }
        }

        private double verticalDisplayScale = Properties.EditorGlobalSetting.Default.VerticalDisplayScale;
        public double VerticalDisplayScale
        {
            get => verticalDisplayScale;
            set
            {
                verticalDisplayScale = Properties.EditorGlobalSetting.Default.VerticalDisplayScale = value;
                RequestSave();
                OnPropertyChanged(nameof(VerticalDisplayScale));
            }
        }

        private int mouseWheelLength = Properties.EditorGlobalSetting.Default.MouseWheelLength;
        public int MouseWheelLength
        {
            get => mouseWheelLength;
            set
            {
                mouseWheelLength = Properties.EditorGlobalSetting.Default.MouseWheelLength = value;
                RequestSave();
                OnPropertyChanged(nameof(MouseWheelLength));
            }
        }

        private bool adjustPastedObjects = Properties.EditorGlobalSetting.Default.AdjustPastedObjects;
        public bool AdjustPastedObjects
        {
            get => adjustPastedObjects;
            set
            {
                adjustPastedObjects = Properties.EditorGlobalSetting.Default.AdjustPastedObjects = value;
                RequestSave();
                OnPropertyChanged(nameof(AdjustPastedObjects));
            }
        }

        private bool loopPlayTiming = Properties.EditorGlobalSetting.Default.LoopPlayTiming;
        public bool LoopPlayTiming
        {
            get => loopPlayTiming;
            set
            {
                loopPlayTiming = Properties.EditorGlobalSetting.Default.LoopPlayTiming = value;
                RequestSave();
                OnPropertyChanged(nameof(LoopPlayTiming));
            }
        }

        public enum TimeFormat
        {
            TGrid,
            AudioTime
        }

        private TimeFormat displayTimeFormat = (TimeFormat)Properties.EditorGlobalSetting.Default.DisplayTimeFormat;
        private bool isRequestSave;

        public TimeFormat DisplayTimeFormat
        {
            get => displayTimeFormat;
            set
            {
                Properties.EditorGlobalSetting.Default.DisplayTimeFormat = (int)value;
                displayTimeFormat = value;
                RequestSave();
                OnPropertyChanged(nameof(DisplayTimeFormat));
            }
        }

        private void Default_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.EditorGlobalSetting.JudgeLineOffsetY):
                    judgeLineOffsetY = Properties.EditorGlobalSetting.Default.JudgeLineOffsetY;
                    break;
                case nameof(Properties.EditorGlobalSetting.DisableXGridMagneticDock):
                    disableXGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock;
                    break;
                case nameof(Properties.EditorGlobalSetting.ForceMagneticDock):
                    forceMagneticDock = Properties.EditorGlobalSetting.Default.ForceMagneticDock;
                    break;
                case nameof(Properties.EditorGlobalSetting.ForceTapHoldMagneticDockToLane):
                    forceTapHoldMagneticDockToLane = Properties.EditorGlobalSetting.Default.ForceTapHoldMagneticDockToLane;
                    break;
                case nameof(Properties.EditorGlobalSetting.DisableTGridMagneticDock):
                    disableTGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock;
                    break;
                case nameof(Properties.EditorGlobalSetting.XGridUnitSpace):
                    xGridUnitSpace = Properties.EditorGlobalSetting.Default.XGridUnitSpace;
                    break;
                case nameof(Properties.EditorGlobalSetting.BeatSplit):
                    beatSplit = Properties.EditorGlobalSetting.Default.BeatSplit;
                    break;
                case nameof(Properties.EditorGlobalSetting.XGridDisplayMaxUnit):
                    xGridDisplayMaxUnit = Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit;
                    break;
                case nameof(Properties.EditorGlobalSetting.ForceXGridMagneticDock):
                    forceXGridMagneticDock = Properties.EditorGlobalSetting.Default.ForceXGridMagneticDock;
                    break;
                case nameof(Properties.EditorGlobalSetting.VerticalDisplayScale):
                    verticalDisplayScale = Properties.EditorGlobalSetting.Default.VerticalDisplayScale;
                    break;
                case nameof(Properties.EditorGlobalSetting.DisplayTimeFormat):
                    displayTimeFormat = (TimeFormat)Properties.EditorGlobalSetting.Default.DisplayTimeFormat;
                    break;
                case nameof(Properties.EditorGlobalSetting.JudgeLineAlignBeat):
                    judgeLineAlignBeat = Properties.EditorGlobalSetting.Default.JudgeLineAlignBeat;
                    break;
                case nameof(Properties.EditorGlobalSetting.MouseWheelLength):
                    mouseWheelLength = Properties.EditorGlobalSetting.Default.MouseWheelLength;
                    break;
                case nameof(Properties.EditorGlobalSetting.XOffset):
                    xOffset = Properties.EditorGlobalSetting.Default.XOffset;
                    break;
                case nameof(Properties.EditorGlobalSetting.ShowXOffsetScrollBar):
                    showXOffsetScrollBar = Properties.EditorGlobalSetting.Default.ShowXOffsetScrollBar;
                    break;
                case nameof(Properties.EditorGlobalSetting.EnableXOffset):
                    enableXOffset = Properties.EditorGlobalSetting.Default.EnableXOffset;
                    break;
                case nameof(Properties.EditorGlobalSetting.AdjustPastedObjects):
                    adjustPastedObjects = Properties.EditorGlobalSetting.Default.AdjustPastedObjects;
                    break;
                case nameof(Properties.EditorGlobalSetting.LoopPlayTiming):
                    loopPlayTiming = Properties.EditorGlobalSetting.Default.LoopPlayTiming;
                    break;
                default:
                    Log.LogWarn($"unknown Properties.EditorGlobalSetting property changed : {e.PropertyName}");
                    break;
            }

            OnPropertyChanged(e.PropertyName);
        }

        public void Dispose()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged;
        }
    }
}



