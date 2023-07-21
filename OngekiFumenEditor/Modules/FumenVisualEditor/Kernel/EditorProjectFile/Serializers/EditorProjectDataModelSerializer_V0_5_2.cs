using MigratableSerializer;
using MigratableSerializer.Wrapper;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Parser.DefaultImpl;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
    public class EditorProjectDataModelSerializer_V0_5_2 : CommonEditorProjectFileSerializer<EditorProjectDataModel_V0_5_2>
    {
        public override Version Version => EditorProjectDataModel_V0_5_2.VERSION;
    }
}
