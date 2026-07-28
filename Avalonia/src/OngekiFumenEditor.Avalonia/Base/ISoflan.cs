using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Base
{
	public interface ISoflan : ITimelineObject, INotifyPropertyChanged, IDisplayableObject
	{
		float Speed { get; set; }
		bool ApplySpeedInDesignMode { get; set; }
        int SoflanGroup { get; set; }

        public float SpeedInEditor => ApplySpeedInDesignMode ? Speed : Math.Abs(Speed);

		TGrid EndTGrid { get; set; } // 考虑到SoflanList的间隔树使用
    }
}

