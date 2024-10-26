using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using static OngekiFumenEditor.App;

namespace OngekiFumenEditor
{
    internal class Startup
    {
		[DllImport("Shcore.dll")]
		static extern int SetProcessDpiAwareness(int value);
        
        [STAThread]
        public static int Main(string[] args)
        {
			SetProcessDpiAwareness(2);           
            var isGUIMode = !(args.Length > 0 && !args[0].StartsWith("--"));

            if (isGUIMode)
            {
                IPCHelper.Init(args);
            }

            var app = new App(isGUIMode);
            app.InitializeComponent();

            return app.Run();
        }
    }
}
