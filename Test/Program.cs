using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Utils;
using System;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var sortList = new SortableCollection<TGrid, TGrid>(x => x, nameof(ITimelineObject.TGrid))
            {
                new TGrid(1,0),
                new TGrid(3,0),
                new TGrid(2,0),
                new TGrid(4,0),
                new TGrid(6,0),
                new TGrid(5,0),
            };

            foreach (var item in sortList)
            {
                Console.WriteLine(item);
            }

            Console.WriteLine(sortList.FastContains(new (4,2)));
        }
    }
}
