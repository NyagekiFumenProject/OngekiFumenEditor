using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models
{
	public class EditorSetting : PropertyChangedBase, IDisposable
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
				NotifyOfPropertyChange(() => JudgeLineOffsetY);
			}
		}

		private bool disableXGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock;
		/// <summary>
		/// 表示物件或者其他在X轴上移动时，是否可以自动吸附到最近的单位线上
		/// </summary>
		public bool DisableXGridMagneticDock
		{
			get => disableXGridMagneticDock;
			set
			{
				disableXGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableXGridMagneticDock = value;
				RequestSave();
				NotifyOfPropertyChange(() => DisableXGridMagneticDock);
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
				NotifyOfPropertyChange(() => ForceMagneticDock);
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
				NotifyOfPropertyChange(() => ForceTapHoldMagneticDockToLane);
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
				NotifyOfPropertyChange(() => JudgeLineAlignBeat);
			}
		}

		private bool disableTGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock;
		/// <summary>
		/// 表示物件或者其他在时间轴上移动时，是否可以自动吸附到最近的单位线上
		/// </summary>
		public bool DisableTGridMagneticDock
		{
			get => disableTGridMagneticDock;
			set
			{
				disableTGridMagneticDock = Properties.EditorGlobalSetting.Default.DisableTGridMagneticDock = value;
				RequestSave();
				NotifyOfPropertyChange(() => DisableTGridMagneticDock);
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
				NotifyOfPropertyChange(() => EnableXOffset);
			}
		}

		private double xOffset = Properties.EditorGlobalSetting.Default.XOffset;
		/// <summary>
		/// X轴上单位线间距大小
		/// </summary>
		public double XOffset
		{
			get => EnableXOffset ? xOffset : 0;
			set
			{
				xOffset = Properties.EditorGlobalSetting.Default.XOffset = value;
				RequestSave();
				NotifyOfPropertyChange(() => XOffset);
			}
		}

		private double xGridUnitSpace = Properties.EditorGlobalSetting.Default.XGridUnitSpace;
		/// <summary>
		/// X轴上单位线间距大小
		/// </summary>
		public double XGridUnitSpace
		{
			get => xGridUnitSpace;
			set
			{
				xGridUnitSpace = Properties.EditorGlobalSetting.Default.XGridUnitSpace = value;
				RequestSave();
				NotifyOfPropertyChange(() => XGridUnitSpace);
			}
		}

		private int beatSplit = Properties.EditorGlobalSetting.Default.BeatSplit;
		/// <summary>
		/// 时间轴上单位线划分密度
		/// </summary>
		public int BeatSplit
		{
			get => beatSplit;
			set
			{
				beatSplit = Properties.EditorGlobalSetting.Default.BeatSplit = value;
				RequestSave();
				NotifyOfPropertyChange(() => BeatSplit);
			}
		}

		private int xGridDisplayMaxUnit = Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit;
		/// <summary>
		/// 横轴长度
		/// </summary>
		public int XGridDisplayMaxUnit
		{
			get => xGridDisplayMaxUnit;
			set
			{
				xGridDisplayMaxUnit = Properties.EditorGlobalSetting.Default.XGridDisplayMaxUnit = value;
				RequestSave();
				NotifyOfPropertyChange(() => XGridDisplayMaxUnit);
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
				NotifyOfPropertyChange(() => ForceXGridMagneticDock);
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
				NotifyOfPropertyChange(() => ShowXOffsetScrollBar);
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
				NotifyOfPropertyChange(() => VerticalDisplayScale);
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
				NotifyOfPropertyChange(() => MouseWheelLength);
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
				NotifyOfPropertyChange(() => AdjustPastedObjects);
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
				NotifyOfPropertyChange(() => loopPlayTiming);
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
				NotifyOfPropertyChange(() => DisplayTimeFormat);
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

			NotifyOfPropertyChange(e.PropertyName);
		}

		public void Dispose()
		{
			Properties.EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged;
		}
	}
}
