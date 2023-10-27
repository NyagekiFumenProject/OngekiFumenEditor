namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
	public class XGridUnitLineViewModel
	{
		public double X { get; set; }
		public double Unit { get; set; }
		public bool IsCenterLine { get; set; }
		public override string ToString() => $"{X:F4} {Unit} {(IsCenterLine ? "Center" : string.Empty)}";
	}
}
