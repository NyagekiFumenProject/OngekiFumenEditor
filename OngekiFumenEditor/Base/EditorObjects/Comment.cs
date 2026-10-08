using OngekiFumenEditor.Base.Attributes;
using System.Drawing;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class Comment : OngekiTimelineObjectBase
    {
        public static string CommandName => "[CMT]";
        public override string IDShortName => CommandName;

        public static readonly Color DefaultColor = Color.LightYellow;

        private Color color = DefaultColor;

        [LocalizableObjectPropertyBrowserAlias("CommentColor")]
        [ObjectPropertyBrowserTipText("CommentColorTip")]
        public Color Color
        {
            get => color;
            set => Set(ref color, value);
        }

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
            Color = from.Color;
        }
    }
}
