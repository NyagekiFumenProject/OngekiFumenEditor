using System;
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
}
