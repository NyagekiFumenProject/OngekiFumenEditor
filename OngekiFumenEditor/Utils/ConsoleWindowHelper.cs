using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    internal static class ConsoleWindowHelper
    {
        [DllImport("kernel32.dll")]
        extern static IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        extern static bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void SetConsoleWindowVisible(bool isShow)
        {
            if (isShow)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private static void Hide()
        {
            ShowWindow(GetConsoleWindow(), 0);
            Log.Instance.RemoveOutput<ConsoleLogOutput>();
        }

        private static void Show()
        {
            ShowWindow(GetConsoleWindow(), 1);
            Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();
        }
    }
}
