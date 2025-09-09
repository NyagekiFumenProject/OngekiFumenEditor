using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public record WidthId(int Id, int WidthJudge, int WidthDraw)
    {
        public static WidthId ParseFromId(string id) => int.TryParse(id, out var v) ? ParseFromId(v) : WidthIdConst.Id_1;
        public static WidthId ParseFromId(int id)
        {
            return WidthIdConst.AllWidthIds.FirstOrDefault(w => w.Id == id) ?? WidthIdConst.Id_1;
        }
    }

    public static class WidthIdConst
    {
        public static WidthId Id_1 { get; } = new WidthId(1, 4, 2);
        public static WidthId Id_2 { get; } = new WidthId(2, 6, 3);
        public static WidthId Id_3 { get; } = new WidthId(3, 8, 4);
        public static WidthId Id_4 { get; } = new WidthId(4, 16, 12);
        public static WidthId Id_5 { get; } = new WidthId(5, 24, 20);

        public static IEnumerable<WidthId> AllWidthIds { get; } = [
            Id_1,
            Id_2,
            Id_3,
            Id_4,
            Id_5
        ];
    }
}
