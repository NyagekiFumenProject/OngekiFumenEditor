namespace OngekiFumenEditor.CommandLine
{
    internal class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var app = new App(false);
            app.InitializeComponent();

            return app.Run();
        }
    }
}
