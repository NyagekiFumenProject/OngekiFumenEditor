using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OngekiFumenEditor.CommandLine
{
    internal class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var editorDllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "OngekiFumenEditor.dll");
            if (!File.Exists(editorDllPath))
            {
                var backup = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"editor dll file not found:{editorDllPath}");
                Console.ForegroundColor = backup;
                return -1;
            }
            var assembly = Assembly.LoadFile(editorDllPath);

            var attachMethod = assembly.GetType("Costura.AssemblyLoader")?.GetMethod("Attach");
            /*
            if (attachMethod is null)
            {
                Console.WriteLine($"Costura.AssemblyLoader.Attach() not found");
                return -1;
            }
            */
            attachMethod?.Invoke(null, []);

            var appType = assembly.GetType("OngekiFumenEditor.App");
            if (appType is null)
            {
                var backup = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"OngekiFumenEditor.App not found");
                Console.ForegroundColor = backup;
                return -2;
            }
            dynamic app = Activator.CreateInstance(appType, args: [false]);
            app.InitializeComponent();

            return app.Run();
        }
    }
}
