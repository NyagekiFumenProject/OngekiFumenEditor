using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using OngekiFumenEditor.Utils.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace OngekiFumenEditor
{
    public partial class App : Application
    {
        public bool IsGUIMode { get; }

        public App(bool isGUIMode = true)
        {
            ApplicationSettingsBaseInjector.EnsureInitializedAndInjectedProvider();
            IsGUIMode = isGUIMode;
        }
    }
}
