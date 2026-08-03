using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	public class EditorProjectDataModel : EditorProjectDataModel_V0_5_2
	{
		private ISimpleFile audioFile;
		private ISimpleFile fumenFile;

		public readonly static Version VERSION = new(0, 5, 4);
		public override Version Version => VERSION;

		[JsonIgnore]
		public ISimpleFile AudioFile
		{
			get => audioFile;
			set
			{
				if (ReferenceEquals(audioFile, value))
					return;

				audioFile?.Dispose();
				audioFile = value;
			}
		}

		[JsonIgnore]
		public ISimpleFile FumenFile
		{
			get => fumenFile;
			set
			{
				if (ReferenceEquals(fumenFile, value))
					return;

				fumenFile?.Dispose();
				fumenFile = value;
			}
		}

		public void DisposeRuntimeFiles()
		{
			AudioFile = null;
			FumenFile = null;
		}
	}
}


