using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.UI.Controls
{
	public class StoryboardWrapper
	{
		private Storyboard storyboard = default;
		public Storyboard Storyboard
		{
			get => storyboard;
			set
			{
				storyboard = value;
				Clock = Storyboard.CreateClock();
			}
		}

		public FrameworkElement FrameworkElement { get; set; }
		public ClockGroup Clock { get; private set; }

		public StoryboardWrapper(Storyboard storyboard = default, FrameworkElement frameworkElement = default)
		{
			Storyboard = storyboard;
			FrameworkElement = frameworkElement;
		}

		public void Start()
		{
			Storyboard.Begin(FrameworkElement, true);
		}

		public void Resume()
		{
			Storyboard.Resume(FrameworkElement);
		}

		public void Stop()
		{
			Clock.Controller.Stop();
			//Storyboard.Stop(FrameworkElement);
		}

		public void Pause()
		{
			Clock.Controller.Pause();
			//Storyboard.Pause(FrameworkElement);
		}

		public void JumpAndPause(TimeSpan offset, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
		{
			Pause();
			Jump(offset, origin);
		}

		public void Jump(TimeSpan offset, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
		{
			Storyboard.Seek(FrameworkElement, offset, origin);
		}
	}
}
