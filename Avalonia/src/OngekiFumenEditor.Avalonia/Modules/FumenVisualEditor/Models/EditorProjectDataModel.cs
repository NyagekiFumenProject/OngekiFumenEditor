using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	public class EditorProjectDataModel : EditorProjectDataModel_V0_5_2
	{
		private ISimpleFile audioFile;
		private ISimpleFile audioAwbFile;
		private ISimpleFile fumenFile;
		private ISimpleFile projectFile;
		private ISimpleDirectory projectRoot;

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
		public ISimpleFile AudioAwbFile
		{
			get => audioAwbFile;
			set
			{
				if (ReferenceEquals(audioAwbFile, value))
					return;

				audioAwbFile?.Dispose();
				audioAwbFile = value;
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

		[JsonIgnore]
		public ISimpleFile ProjectFile
		{
			get => projectFile;
			set
			{
				if (ReferenceEquals(projectFile, value))
					return;

				projectFile?.Dispose();
				projectFile = value;
			}
		}

		[JsonIgnore]
		public ISimpleDirectory ProjectRoot
		{
			get => projectRoot;
			set
			{
				if (ReferenceEquals(projectRoot, value))
					return;

				projectRoot?.Dispose();
				projectRoot = value;
			}
		}

		[JsonIgnore]
		public string ProjectFileLocator { get; set; }

		[JsonIgnore]
		public Guid RecentRecordId { get; set; }

		public void DisposeRuntimeFiles()
		{
			if (Fumen is { } fumen)
			{
				foreach (var svg in fumen.SvgPrefabs.ToArray())
					svg.Dispose();
			}

			AudioAwbFile = null;
			AudioFile = null;
			FumenFile = null;
			ProjectFile = null;
			ProjectRoot = null;
		}
	}
}


