using OngekiFumenEditor.Utils;
using System;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var subs in new[] { 1, 2, 3, 4, 5, 4, 3, 2, 1, 1, 2, 3, 4, 5 }.SplitByTurningGradient(x => x))
            {
                Console.WriteLine(string.Join(" ",subs));
            }
        }
    }
}
