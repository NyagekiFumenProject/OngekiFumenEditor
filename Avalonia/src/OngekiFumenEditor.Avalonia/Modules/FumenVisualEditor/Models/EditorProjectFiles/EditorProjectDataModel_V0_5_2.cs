using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	// Legacy deserialization contract. File locators from this type are discarded during migration.
	public class EditorProjectDataModel_V0_5_2 : EditorProjectDataModelBase
	{
		public readonly static Version VERSION = new(0, 5, 2);

		public override Version Version => VERSION;

		private string audioFilePath = default;
		[JsonInclude]
		public string AudioFilePath
		{
			get => audioFilePath;
			set => SetProperty(ref audioFilePath, value);
		}

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

	}
}



