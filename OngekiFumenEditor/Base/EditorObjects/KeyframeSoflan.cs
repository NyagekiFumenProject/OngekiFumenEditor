using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
	public class KeyframeSoflan : OngekiTimelineObjectBase, IKeyframeSoflan
	{
		public override string IDShortName => "[KEY_SFL]";

		private float speed = 1;
		public float Speed
		{
			get => speed;
			set => Set(ref speed, value);
		}

		private bool applySpeedInDesignMode = false;
		public bool ApplySpeedInDesignMode
		{
			get => applySpeedInDesignMode;
			set => Set(ref applySpeedInDesignMode, value);
		}

		public float SpeedInEditor => ApplySpeedInDesignMode ? speed : 1;

		public TGrid EndTGrid
		{
			get => TGrid; set
			{
				TGrid = value;
				NotifyOfPropertyChange(() => EndTGrid);
			}
		}

		public override string ToString() => $"{base.ToString()} Speed[{speed}x]";

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not KeyframeSoflan sfl)
				return;

			Speed = sfl.Speed;
			ApplySpeedInDesignMode = sfl.ApplySpeedInDesignMode;
		}
	}
}
