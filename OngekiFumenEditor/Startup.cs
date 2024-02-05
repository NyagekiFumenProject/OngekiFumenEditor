using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (args.Length == 0)
                ShowWindow(GetConsoleWindow(), 0);

            IPCHelper.Init(args);

            var app = new App();
            app.InitializeComponent();

            return app.Run();
        }
    }
}
