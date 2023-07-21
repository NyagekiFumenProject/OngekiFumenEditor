using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
    public abstract class EditorProjectDataModelBase : PropertyChangedBase
    {
        [JsonInclude]
        public abstract Version Version { get; }
    }
}
