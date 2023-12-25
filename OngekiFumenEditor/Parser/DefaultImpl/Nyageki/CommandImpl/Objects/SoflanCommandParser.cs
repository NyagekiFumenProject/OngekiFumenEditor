using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class SoflanCommandParser : INyagekiCommandParser
	{
		public virtual string CommandName => "Soflan";

		public virtual void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"Soflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])"
			var soflan = new Soflan();
			Apply(soflan, seg);
			fumen.AddObject(soflan);
		}

		public void Apply(ISoflan soflan, string[] seg)
		{
			var data = seg[1].Split(":");

			soflan.Speed = float.Parse(data[0]);
			var tgridRange = data[1]
				.Split("->")
				.Select(x => x.Trim().TrimStart('(').TrimEnd(')'))
				.Select(x => x.ParseToTGrid())
				.ToArray();

			soflan.TGrid = tgridRange[0];
			soflan.EndTGrid = tgridRange[1];
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class InterpolatableSoflanCommandParser : SoflanCommandParser
	{
		public override string CommandName => "InterpolatableSoflan";

		public override void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"InterpolatableSoflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])"
			var soflan = new InterpolatableSoflan();
			Apply(soflan, seg);
			var data = seg[1].Split(":");

			using var d = data[2].GetValuesMapWithDisposable(out var map);
			soflan.Easing = Enum.Parse<EasingTypes>(map["Easing"]);
			((InterpolatableSoflan.InterpolatableSoflanIndicator)soflan.EndIndicator).Speed = float.Parse(map["EndSpeed"]);

			fumen.AddObject(soflan);
		}
	}


	[Export(typeof(INyagekiCommandParser))]
	public class KeyframeSoflanCommandParser : SoflanCommandParser
	{
		public override string CommandName => "KeyframeSoflan";

		public override void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"InterpolatableSoflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])"
			var soflan = new KeyframeSoflan();
			Apply(soflan, seg);

			fumen.AddObject(soflan);
		}
	}
}
