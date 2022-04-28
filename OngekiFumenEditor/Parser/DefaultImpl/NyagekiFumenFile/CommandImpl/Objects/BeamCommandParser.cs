using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.UI.ValueConverters;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.NyagekiFumenFile.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class BeamCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Beam";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //Beam:0:(Type[BMN],X[-12,0],T[69,1350]) -> (Type[BME],X[1.0094876,0],T[70,216])
            var data = seg[1].Split(":");

            var recordId = int.Parse(data[0]);
            var maps = data[1].Split("->").Select(x => x.Trim().TrimStart('(').TrimEnd(')')).Select(x => (x.GetValuesMapWithDisposable(out var d), d)).ToArray();
            var notes = maps.Select(x => x.d).ToArray();

            void buildCommon(ConnectableObjectBase obj, Dictionary<string, string> map)
            {
                obj.TGrid = map["T"].ParseToTGrid();
                obj.XGrid = map["X"].ParseToXGrid();
            }

            void buildBeam(IBeamObject obj, Dictionary<string, string> map)
            {
                obj.WidthId = int.Parse(map["W"]);
            }

            // Start
            var startData = notes.FirstOrDefault();
            var startObject = new BeamStart();
            buildCommon(startObject, startData);
            startObject.RecordId = recordId;
            buildBeam((IBeamObject)startObject, startData);

            // Next/End
            foreach (var childData in notes.Skip(1))
            {
                var childObject = LambdaActivator.CreateInstance(childData["Type"].Last() switch
                {
                    'N' => startObject.NextType,
                    'E' => startObject.EndType,
                    _ => default
                }) as ConnectableChildObjectBase;

                buildCommon(childObject, childData);
                buildBeam((IBeamObject)childObject, childData);
                startObject.AddChildObject(childObject);
            }

            foreach ((var d, _) in maps)
                d.Dispose();

            fumen.AddObject(startObject);
        }
    }
}
