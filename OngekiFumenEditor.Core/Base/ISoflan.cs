using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Base
{
	public interface ISoflan : ITimelineObject, INotifyPropertyChanged, IDisplayableObject
	{
		float Speed { get; set; }
		bool ApplySpeedInDesignMode { get; set; }
        int SoflanGroup { get; set; }

        float SpeedInEditor { get; }

		TGrid EndTGrid { get; set; } // 考虑到SoflanList的间隔树使用
    }
}
