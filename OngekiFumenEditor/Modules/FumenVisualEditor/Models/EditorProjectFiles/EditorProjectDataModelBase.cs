using Caliburn.Micro;
using System;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	public abstract class EditorProjectDataModelBase : PropertyChangedBase
	{
		[JsonInclude]
		public abstract Version Version { get; }
	}
}
