using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	public class EditorProjectDataModel_V0_5_2 : EditorProjectDataModelBase
	{
		public readonly static Version VERSION = new(0, 5, 2);

		public override Version Version => VERSION;

		public Guid guid = Guid.NewGuid();

		[JsonInclude]
		public Guid Id
		{
			get => guid;
			init => SetProperty(ref guid, value);
		}

		private string audioFilePath = default;
		[JsonInclude]
		public string AudioFilePath
		{
			get => audioFilePath;
			set => SetProperty(ref audioFilePath, value);
		}

		private TimeSpan audioDuration = default;
		[JsonInclude]
		public TimeSpan AudioDuration
		{
			get => audioDuration;
			set => SetProperty(ref audioDuration, value);
		}

		public EditorSetting EditorSetting { get; } = new EditorSetting();

		private string fumenFilePath = default;
		[JsonInclude]
		public string FumenFilePath
		{
			get => fumenFilePath;
			set
			{
				if (SetProperty(ref fumenFilePath, value))
					OnPropertyChanged(nameof(CanEditBaseBpm));
			}
		}

		[JsonIgnore]
		public bool CanEditBaseBpm => string.IsNullOrWhiteSpace(FumenFilePath);

		private TimeSpan rememberLastDisplayTime = TimeSpan.FromMilliseconds(0);
		[JsonInclude]
		public TimeSpan RememberLastDisplayTime
		{
			get => rememberLastDisplayTime;
			set => SetProperty(ref rememberLastDisplayTime, value);
		}

		public class StoreBulletPalleteEditorData
		{
			public string Name { get; set; }
			public Color AuxiliaryLineColor { get; set; }
		}

		public Dictionary<string, StoreBulletPalleteEditorData> StoreBulletPalleteEditorDatas { get; set; } = new();
	}
}



