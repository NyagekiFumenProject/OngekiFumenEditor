namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public record MusicRight(string Name, int Id) : IEnumStruct
	{
		public string DisplayName => Name;
	}
}
