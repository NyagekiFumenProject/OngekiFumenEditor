namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public record VersionID(string Name, int Id, string Title) : IEnumStruct
	{
		public string DisplayName => Name;
	}
}
