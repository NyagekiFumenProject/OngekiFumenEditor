using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.EditorDataProcesser
{
    [Export(typeof(IEditorDataProcesser))]
    [Export(typeof(ICommandParser))]
    public class BulletPalleteEditorDataProcesser : IEditorDataProcesser
    {
        public string CommandLineHeader => "#BPL_EDITOR_DATA";
        private ColorConverter converter = new ();

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var strId = args.GetData<string>(1);
            if (fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == strId) is not BulletPallete pallete)
                return default;

            var editorName = args.GetData<string>(2);
            var color = args.GetData<string>(3);

            pallete.EditorName = editorName;
            pallete.EditorAxuiliaryLineColor = (Color)ColorConverter.ConvertFromString(color);

            return default;
        }

        public string SerializeAll(OngekiFumen fumen)
        {
            using var d = ObjectPool<StringBuilder>.GetWithUsingDisposable(out var sb, out var _);
            sb.Clear();

            foreach (var pallete in fumen.BulletPalleteList)
            {
                sb.AppendLine($"{CommandLineHeader} {pallete.StrID} {pallete.EditorName} {converter.ConvertToString(pallete.EditorAxuiliaryLineColor)}");
            }

            return sb.ToString();
        }
    }
}
