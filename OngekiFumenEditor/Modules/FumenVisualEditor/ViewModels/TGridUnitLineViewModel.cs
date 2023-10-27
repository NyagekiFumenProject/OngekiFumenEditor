using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
	public class TGridUnitLineViewModel
	{
		public double Y { get; set; }
		public TGrid TGrid { get; set; }
		public int BeatRhythm { get; set; }
		public override string ToString() => $"{Y:F4} {TGrid}";
	}

}
