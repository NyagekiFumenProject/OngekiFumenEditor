using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base
{
    public readonly record struct GridOffset(float Unit, int Grid)
    {
        public static GridOffset Zero { get; } = new GridOffset(0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int TotalGrid(uint gridRadix) => (int)(Unit * gridRadix + Grid);
    }
}
