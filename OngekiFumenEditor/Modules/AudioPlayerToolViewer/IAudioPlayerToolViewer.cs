using Gemini.Framework;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer
{
	public interface IAudioPlayerToolViewer : ITool, IDisposable
	{
		IAudioPlayer AudioPlayer { get; }
		float SoundVolume { get; set; }
		FumenVisualEditorViewModel Editor { get; }
		void RequestPlayOrPause();
	}
}
