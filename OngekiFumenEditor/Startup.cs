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
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        public static int Main(string[] args)
        {
            var isGUIMode = false;

            if (args.Length == 0)
            {
                isGUIMode = true;
            }
            else
            {
                //there is args, check others
                if (args.IsOnlyOne(out var firstArg) && File.Exists(firstArg))
                {
                    isGUIMode = true;
                }
            }

            if (isGUIMode)
            {
                ShowWindow(GetConsoleWindow(), 0);
                IPCHelper.Init(args);
            }

            var app = new App(isGUIMode);
            app.InitializeComponent();

            return app.Run();
        }
    }
}
