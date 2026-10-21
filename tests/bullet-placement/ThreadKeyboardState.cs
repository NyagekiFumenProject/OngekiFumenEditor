using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

internal sealed class ThreadKeyboardState : IDisposable
{
    private readonly byte[] original = new byte[256];
    private readonly byte[] current = new byte[256];

    public ThreadKeyboardState()
    {
        if (!GetKeyboardState(original))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        SetModifiers(ModifierKeys.None);
    }

    public void SetModifiers(ModifierKeys modifiers)
    {
        Array.Clear(current);
        if ((modifiers & ModifierKeys.Alt) != 0)
            current[0x12] = current[0xA4] = 0x80;
        if ((modifiers & ModifierKeys.Control) != 0)
            current[0x11] = current[0xA2] = 0x80;
        if ((modifiers & ModifierKeys.Shift) != 0)
            current[0x10] = current[0xA0] = 0x80;
        if ((modifiers & ModifierKeys.Windows) != 0)
            current[0x5B] = 0x80;
        if (!SetKeyboardState(current))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (!SetKeyboardState(original))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    // These APIs only change this test thread's key-state table; they send no input
    // and do not activate a window or change the user's foreground input stream.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] state);
}
