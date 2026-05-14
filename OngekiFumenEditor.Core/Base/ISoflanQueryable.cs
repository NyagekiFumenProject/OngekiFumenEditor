using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Core.Base.Collections.SoflanList;

namespace OngekiFumenEditor.Core.Base
{
    public interface ISoflanQueryable
    {
        IList<SoflanPoint> GetSoflanPositionList(BpmList bpmList, bool isDesignMode);
        IEnumerable<VisibleTGridRange> GetVisibleRanges(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale,bool isDesignMode);
        IEnumerable<KeyframeSoflan> GenerateDurationSoflans(BpmList bpmList);
        IEnumerable<KeyframeSoflan> GenerateKeyframeSoflans(BpmList bpmList);
    }
}
