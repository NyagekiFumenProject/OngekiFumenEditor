using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class EnemySet : OngekiTimelineObjectBase
    {

        public enum WaveChangeConst
        {
            Wave1 = 0,
            Wave2 = 1,
            Boss = 2,
        }

        private WaveChangeConst tagTblValue = WaveChangeConst.Boss;
        public WaveChangeConst TagTblValue
        {
            get { return tagTblValue; }
            set
            {
                tagTblValue = value;
                NotifyOfPropertyChange(() => TagTblValue);
            }
        }

        public static string CommandName => "EST";
        public override string IDShortName => CommandName;

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not EnemySet fromSet)
                return;

            TagTblValue = fromSet.TagTblValue;
        }
    }
}
