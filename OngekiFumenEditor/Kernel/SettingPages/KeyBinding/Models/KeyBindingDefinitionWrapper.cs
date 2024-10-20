using Caliburn.Micro;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Models
{
    public class KeyBindingDefinitionWrapper : PropertyChangedBase
    {
        private readonly KeyBindingDefinition definition;

        public KeyBindingDefinitionWrapper(KeyBindingDefinition definition)
        {
            this.definition = definition;
        }

        public KeyBindingDefinition Definition => definition;
    }
}
