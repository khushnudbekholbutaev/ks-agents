using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Common.Interfaces;

namespace Common.Helpers
{
    public class ForegroundWindowHelper
    {
        private readonly ILogger logger;

        public ForegroundWindowHelper(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();
        }

        public string GetActiveWindowTitle()
        {
            IntPtr hwnd = GetForegroundWindow();
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);

            string title = null;
            if (GetWindowText(hwnd, buff, nChars) > 0)
            {
                title = buff.ToString();
                logger.LogInformation($"Active window title detected: {title}");
            }
            else
            {
                logger.LogInformation("No active window title detected.");
            }

            return title;
        }

        public string GetProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);
            string processName = string.Empty;

            try
            {
                Process proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
                logger.LogInformation($"Foreground process detected: {processName} (PID: {pid})");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get process for PID {pid}: {ex.Message}");
            }

            return processName;
        }

        #region WinAPI

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion
    }
}
