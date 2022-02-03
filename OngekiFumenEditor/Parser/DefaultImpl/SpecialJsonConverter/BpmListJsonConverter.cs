using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.Json.Serialization;
using static OngekiFumenEditor.Parser.DefaultImpl.BpmListJsonConverter;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(JsonConverter))]
    public class BpmListJsonConverter : SimpleJsonConverter<BpmList, TempBpmList>
    {
        public override BpmList Read(TempBpmList jsonObj)
        {
            return new BpmList(jsonObj.changedBpmList);
        }

        public override TempBpmList Write(BpmList jsonObj)
        {
            return new TempBpmList()
            {
                changedBpmList = jsonObj.Where(x => x != jsonObj.FirstBpm)
            };
        }

        public class TempBpmList
        {
            public IEnumerable<BPMChange> changedBpmList { get; set; }
        }
    }
}
