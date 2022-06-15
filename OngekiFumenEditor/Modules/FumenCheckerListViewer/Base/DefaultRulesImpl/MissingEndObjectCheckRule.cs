using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class MissingEndObjectCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, object fumenHostViewModel)
        {
            const string RuleName = "MissingEndObject";

            IEnumerable<ICheckResult> CheckBeamList(IEnumerable<BeamStart> objs)
            {
                foreach (var missingObject in objs.Where(x => !x.Children.OfType<ConnectableEndObject>().Any()))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = $"物件{missingObject.IDShortName}(id:{missingObject.RecordId})缺少中止物件",
                        LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
                        NavigateTGridLocation = missingObject.MaxTGrid,
                        RuleName = RuleName,
                    };
                }
            }

            IEnumerable<ICheckResult> CheckList(IEnumerable<ConnectableStartObject> objs)
            {
                foreach (var missingObject in objs.Where(x => !x.Children.OfType<ConnectableEndObject>().Any()))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = $"物件{missingObject.IDShortName}(id:{missingObject.RecordId})缺少中止物件",
                        LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
                        NavigateTGridLocation = missingObject.MaxTGrid,
                        RuleName = RuleName,
                    };
                }
            }

            var starts = Enumerable.Empty<ConnectableStartObject>()
                .Concat(fumen.Lanes)
                .Concat(fumen.Holds);

            foreach (var start in CheckList(starts))
            {
                yield return start;
            }

            foreach (var start in CheckBeamList(fumen.Beams))
            {
                yield return start;
            }
        }
    }
}

