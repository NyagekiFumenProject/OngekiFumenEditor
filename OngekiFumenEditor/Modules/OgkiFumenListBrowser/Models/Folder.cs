using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models
{
	public class Folder
	{
		public List<OngekiFumenSet> FumenSets { get; } = new();
		public List<Folder> SubFolders { get; } = new();

		public bool IsEmpty => FumenSets.Count == 0 && SubFolders.All(x => x.IsEmpty);

		public string Name { get; set; }

		public override string ToString() => $"Folder:{Name}  Sets:{FumenSets.Count}  SubFolders:{SubFolders.Count}";
	}
}
