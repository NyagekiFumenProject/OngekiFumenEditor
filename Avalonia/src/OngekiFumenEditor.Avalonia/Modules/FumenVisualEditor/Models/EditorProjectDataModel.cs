using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	// Pure persistable data class: only project-related settings and options live here.
	// Runtime state (parsed fumen, file handles and recent-record identity) is owned
	// by the companion EditorContext.
	public class EditorProjectDataModel : EditorProjectDataModelBase
	{
		public readonly static Version VERSION = new(0, 5, 5);
		public override Version Version => VERSION;

		public Guid guid = Guid.NewGuid();

		[JsonInclude]
		public Guid Id
		{
			get => guid;
			init => SetProperty(ref guid, value);
		}

		private TimeSpan audioDuration = default;
		[JsonInclude]
		public TimeSpan AudioDuration
		{
			get => audioDuration;
			set => SetProperty(ref audioDuration, value);
		}

		public EditorSetting EditorSetting { get; } = new EditorSetting();

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
