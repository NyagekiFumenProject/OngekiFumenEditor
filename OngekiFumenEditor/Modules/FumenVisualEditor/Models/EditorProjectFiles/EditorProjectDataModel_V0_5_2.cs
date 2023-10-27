using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles
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
			init => Set(ref guid, value);
		}

		private string audioFilePath = default;
		[JsonInclude]
		public string AudioFilePath
		{
			get => audioFilePath;
			set => Set(ref audioFilePath, value);
		}

		private TimeSpan audioDuration = default;
		[JsonInclude]
		public TimeSpan AudioDuration
		{
			get => audioDuration;
			set => Set(ref audioDuration, value);
		}

		public EditorSetting EditorSetting { get; } = new EditorSetting();

		private string fumenFilePath = default;
		[JsonInclude]
		public string FumenFilePath
		{
			get => fumenFilePath;
			set => Set(ref fumenFilePath, value);
		}

		private TimeSpan rememberLastDisplayTime = TimeSpan.FromMilliseconds(0);
		[JsonInclude]
		public TimeSpan RememberLastDisplayTime
		{
			get => rememberLastDisplayTime;
			set => Set(ref rememberLastDisplayTime, value);
		}

		private OngekiFumen fumen = new();
		[JsonIgnore]
		public OngekiFumen Fumen
		{
			get => fumen;
			set
			{
				Set(ref fumen, value);
				NotifyOfPropertyChange(() => BaseBPM);
			}
		}

		[JsonIgnore]
		public double BaseBPM
		{
			get => Fumen.MetaInfo.BpmDefinition.First;
			set
			{
				if (Fumen is not null)
				{
					Fumen.MetaInfo.BpmDefinition.First = value;
					Fumen.BpmList.FirstBpm.BPM = value;
				}
				NotifyOfPropertyChange(() => BaseBPM);
			}
		}

		public class StoreBulletPalleteEditorData
		{
			public string Name { get; set; }
			public Color AuxiliaryLineColor { get; set; }
		}

		public Dictionary<string, StoreBulletPalleteEditorData> StoreBulletPalleteEditorDatas { get; set; } = new();
	}
}
