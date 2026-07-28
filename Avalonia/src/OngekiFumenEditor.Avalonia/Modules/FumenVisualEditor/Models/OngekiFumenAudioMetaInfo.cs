using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	public class OngekiFumenAudioMetaInfo : ObservableObject
	{
		private string title = default;
		[JsonInclude]
		public string Title
		{
			get => title;
			set => SetProperty(ref title, value);
		}

		private string artist = default;
		[JsonInclude]
		public string Artist
		{
			get => artist;
			set => SetProperty(ref artist, value);
		}

		private TimeSpan audioDuration = default;
		[JsonInclude]
		public TimeSpan AudioDuration
		{
			get => audioDuration;
			set => SetProperty(ref audioDuration, value);
		}
	}
}


