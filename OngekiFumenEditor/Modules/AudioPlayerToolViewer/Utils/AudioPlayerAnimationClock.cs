using OngekiFumenEditor.Kernel.Audio;
using System;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Utils
{
	public class AudioPlayerAnimationClock : AnimationClock
	{
		private readonly IAudioPlayer audioPlayer;

		public AudioPlayerAnimationClock(IAudioPlayer audioPlayer, AnimationTimeline timeline) : this(timeline)
		{
			this.audioPlayer = audioPlayer;
		}

		protected AudioPlayerAnimationClock(AnimationTimeline timeline) : base(timeline)
		{

		}

		protected override bool GetCanSlip() => true;

		protected override void Stopped()
		{
			base.Stopped();
		}

		protected override TimeSpan GetCurrentTimeCore()
		{
			return audioPlayer.CurrentTime;
		}
	}
}
