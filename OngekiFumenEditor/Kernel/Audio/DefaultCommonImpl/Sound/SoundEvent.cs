using System;

namespace OngekiFumenEditor.Kernel.Audio.DefaultCommonImpl.Sound
{
	public class SoundEvent
	{
		public SoundControl Sounds { get; set; }
		public TimeSpan Time { get; set; }
		//public TGrid TGrid { get; set; }

		public override string ToString() => $"{Time} {Sounds} ";
	}
}
