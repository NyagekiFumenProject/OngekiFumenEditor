using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	public abstract class EditorProjectDataModelBase : ObservableObject
	{
		[JsonInclude]
		public abstract Version Version { get; }

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


