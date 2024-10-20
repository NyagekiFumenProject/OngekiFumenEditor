using OngekiFumenEditor.Kernel.KeyBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class KeyBindingDefinitions
    {
        [Export]
        public static KeyBindingDefinition KeyBindingDefinition_FastSetObjectIsCritical = new KeyBindingDefinition(
            "kbd_FastSetObjectIsCritical",
             Key.C);
    }
}
