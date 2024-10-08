﻿using System.ComponentModel;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
	public interface IWaveformDrawingOption : INotifyPropertyChanged
	{
		void Reset();
		void Reload();
		void Save();
	}
}
