using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	public abstract class EditorProjectDataModelBase : ObservableObject
	{
		[JsonInclude]
		public abstract Version Version { get; }
	}
}


