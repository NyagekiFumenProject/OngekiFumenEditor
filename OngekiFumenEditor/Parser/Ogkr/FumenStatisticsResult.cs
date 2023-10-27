namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	public class FumenStatisticsResult
	{
		public int BellObjects { get; set; }
		public int FlickObjects { get; set; }
		public int SideObjects { get; set; }
		public int TapObjects { get; internal set; }
		public int SideHoldObjects { get; internal set; }
		public int TotalObjects { get; internal set; }
		public int HoldObjects { get; internal set; }
	}
}