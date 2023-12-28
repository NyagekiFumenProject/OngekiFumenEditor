using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
	public interface ISoflan : ITimelineObject, INotifyPropertyChanged, IDisplayableObject
	{
		float Speed { get; set; }
		bool ApplySpeedInDesignMode { get; set; }

		public float SpeedInEditor => ApplySpeedInDesignMode ? Speed : Math.Abs(Speed);

		TGrid EndTGrid { get; set; } // 考虑到SoflanList的间隔树使用
	}
}
