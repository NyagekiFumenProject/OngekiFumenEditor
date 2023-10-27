namespace OngekiFumenEditor.Kernel.Audio
{
	public record SampleInfo()
	{
		public int SampleRate { get; set; }
		public int Channels { get; set; }
		public int BitsPerSample { get; set; }

		public int BytesPerSample => BitsPerSample / 8;
	}
}
