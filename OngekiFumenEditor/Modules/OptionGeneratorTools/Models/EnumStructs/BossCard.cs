namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public record BossCard(string Name, int Id, BossAttritude Attritude, Rarity Rarity, string FilePath) : IEnumStruct
	{
		public string DisplayName => Name;
	}
}
