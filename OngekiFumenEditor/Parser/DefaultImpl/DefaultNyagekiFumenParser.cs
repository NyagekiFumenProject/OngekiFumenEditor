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

        Dictionary<string, INyagekiCommandParser> commandParsers;

        [ImportingConstructor]
        public DefaultNyagekiFumenParser([ImportMany] IEnumerable<INyagekiCommandParser> commandParsers)
        {
            this.commandParsers = commandParsers.ToDictionary(x => x.CommandName.Trim().ToLower(), x => x);
        }

        public async Task<OngekiFumen> DeserializeAsync(Stream stream)
        {
            using var reader = new StreamReader(stream);

            var fumen = new OngekiFumen();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                var seg = line.Split(':', 2);
                var commandName = seg[0].ToLower().Trim();

                if (commandParsers.TryGetValue(commandName, out var commandParser))
                    commandParser.ParseAndApply(fumen, seg);
            }

            fumen.Setup();
            return fumen;
        }
    }
}
