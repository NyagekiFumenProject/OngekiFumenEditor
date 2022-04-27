using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenDeserializable))]
    public class DefaultNyagekiFumenParser : IFumenDeserializable
    {
        public const string FormatName = "Nyageki Fumen File";
        public string FileFormatName => FormatName;

        public static readonly string[] FumenFileExtensions = new[] { ".nyageki" };
        public string[] SupportFumenFileExtensions => FumenFileExtensions;

        public async Task<OngekiFumen> DeserializeAsync(Stream stream)
        {
            using var reader = new StreamReader(stream);

            var fumen = new OngekiFumen();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                var seg = line.Split(':', 1);
                switch (seg[0].ToLower().Trim())
                {
                    case "":
                        break;
                    default:
                        break;
                }
            }

            fumen.Setup();

            return fumen;
        }
    }
}
