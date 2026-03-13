namespace OngekiFumenEditor.Avalonia.Base
{
	public interface IDurationSoflan : ISoflan
	{
		IEnumerable<IKeyframeSoflan> GenerateKeyframeSoflans();
		float CalculateSpeed(TGrid tGrid);
	}
}
