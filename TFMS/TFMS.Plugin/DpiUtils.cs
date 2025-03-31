using System.Runtime.InteropServices;

namespace TFMS.Plugin
{
    internal class DpiUtils
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwareness(int dpiAwareness);

        // You can use SetProcessDpiAwarenessContext instead of SetProcessDpiAwareness in .NET Core/5+
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);

        public enum DPI_AWARENESS_CONTEXT : int
        {
            DPI_AWARENESS_CONTEXT_UNAWARE = -1,
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = 1
        }

        public static void SetDpiAwareness()
        {
            // Set the process DPI awareness to SYSTEM_AWARE or PER_MONITOR_AWARE based on the needs
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);
            }
        }
    }
}
