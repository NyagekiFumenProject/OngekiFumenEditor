namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public record Genre(string Name, int Id) : IEnumStruct
	{
		public string DisplayName => Name;
	}
}
