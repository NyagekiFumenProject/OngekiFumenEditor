using System;
using System.Reflection;

namespace OngekiFumenEditor.CommandLine
{
    internal class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Assembly assembly;
            try
            {
                // 主程序集已被打入本 single-file bundle, 通过 AssemblyName 走默认 ALC 即可解析,
                // 同时其依赖也都在同一 bundle 里, 避免 LoadFile 引入独立 ALC 后依赖无法解析.
                assembly = Assembly.Load(new AssemblyName("OngekiFumenEditor"));
            }
            catch (Exception ex)
            {
                var backup = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"failed to load OngekiFumenEditor: {ex.Message}");
                Console.ForegroundColor = backup;
                return -1;
            }

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

