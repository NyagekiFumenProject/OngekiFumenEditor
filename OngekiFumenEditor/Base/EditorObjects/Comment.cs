using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class Comment : OngekiTimelineObjectBase
    {
        public static string CommandName => "[CMT]";
        public override string IDShortName => CommandName;

        private string content = string.Empty;

        public string Content
        {
            get { return content; }
            set { content = value; }
        }

        public override string ToString() => $"{base.ToString()} Content[{Content}]";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not Comment from)
                return;

            Content = from.Content;
        }
    }
}
