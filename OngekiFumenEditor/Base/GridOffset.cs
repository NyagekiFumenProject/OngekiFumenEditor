using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base
{
	public sealed record GridOffset(float Unit, int Grid)
	{
		public static GridOffset Zero { get; } = new GridOffset(0, 0);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int TotalGrid(uint gridRadix) => (int)(Unit * gridRadix + Grid);
	}
}
