using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	internal class HoldTapDrawingTarget : CommonDrawTargetBase<Hold>
	{
		public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

		public override int DefaultRenderOrder => 1200;

		private TapDrawingTarget tapDraw;

		public HoldTapDrawingTarget() : base()
		{
			tapDraw = IoC.Get<TapDrawingTarget>();
		}

		public override void Draw(IFumenEditorDrawingContext target, Hold hold)
		{
			var start = hold.ReferenceLaneStart;
			var holdEnd = hold.HoldEnd;
			var laneType = start?.LaneType;

			//draw taps
			if (target.CheckDrawingVisible(tapDraw.Visible))
			{
				tapDraw.Begin(target);
				tapDraw.Draw(target, laneType, hold, hold.IsCritical);
				if (holdEnd != null)
					tapDraw.Draw(target, laneType, holdEnd, false);
				tapDraw.End();
			}
		}
	}
}
