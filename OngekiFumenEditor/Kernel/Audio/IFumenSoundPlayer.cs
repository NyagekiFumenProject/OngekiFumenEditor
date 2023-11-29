using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
	public interface IFumenSoundPlayer
	{
		SoundControl SoundControl { get; set; }
		Task Prepare(FumenVisualEditorViewModel editor, IAudioPlayer player);
		Task Clean();
		void Stop();
		void Play();
		void Pause();
		float? GetVolume(SoundControl sound);
		void SetVolume(SoundControl sound, float volume);
		void Seek(TimeSpan msec, bool pause);

		Task<bool> ReloadSoundFiles();
	}
}
