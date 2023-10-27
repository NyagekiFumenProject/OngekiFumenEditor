namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public record Stage(string Name, int Id) : IEnumStruct
	{
		public string DisplayName => Name;
	}
}
