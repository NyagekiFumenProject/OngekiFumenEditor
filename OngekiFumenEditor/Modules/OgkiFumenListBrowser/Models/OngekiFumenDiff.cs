namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models
{
	public class OngekiFumenDiff
	{
		public OngekiFumenDiff(OngekiFumenSet refSet)
		{
			RefSet = refSet;
		}

		public float Level { get; set; }
		public string FilePath { get; set; }
		public int DiffIdx { get; set; }
		public float Bpm { get; set; }
		public string Creator { get; set; }

		public string DiffName => DiffIdx switch
		{
			0 => "Basic",
			1 => "Advanced",
			2 => "Expert",
			3 => "Master",
			4 => "Lunatic",
			_ => string.Empty
		};

		public OngekiFumenSet RefSet { get; }
	}
}