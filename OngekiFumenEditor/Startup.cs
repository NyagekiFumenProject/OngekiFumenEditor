using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.App;

namespace OngekiFumenEditor
{
    internal class Startup
    {
        [STAThread]
        public static int Main(string[] args)
        {
            IPCHelper.Init();

            App app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
