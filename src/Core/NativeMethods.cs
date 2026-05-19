using System.Runtime.InteropServices;

namespace WPDAutomatic.Core;

internal static class NativeMethods
{
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    public static void SendMouseInput(uint dwFlags, int dx, int dy)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = 0,
                mi = new MOUSEINPUT
                {
                    dwFlags = dwFlags,
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }
}
