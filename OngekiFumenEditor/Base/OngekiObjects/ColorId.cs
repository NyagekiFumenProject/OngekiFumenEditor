using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public struct ColorId
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public Color Color { get; set; }

		public override string ToString() => $"{Id} {Name}";
	}
}