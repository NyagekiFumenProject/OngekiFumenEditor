using System;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils;

public class NativeMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static void Show(string message)
    {
        MessageBox(IntPtr.Zero, message, "提示", 0);
    }

    public static void Show(string message, string title)
    {
        MessageBox(IntPtr.Zero, message, title, 0);
    }
}