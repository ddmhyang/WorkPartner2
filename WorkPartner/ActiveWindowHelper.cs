using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WorkPartner
{
    public static class ActiveWindowHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public static string GetActiveProcessName()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                GetWindowThreadProcessId(handle, out uint processId);
                Process proc = Process.GetProcessById((int)processId);
                return proc.ProcessName.ToLower();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint lastInputTick = lastInputInfo.dwTime;
                uint idleTime = GetTickCount() - lastInputTick;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            return TimeSpan.Zero;
        }

        public static string GetActiveBrowserTabUrl()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return null;

                AutomationElement element = AutomationElement.FromHandle(handle);
                if (element == null) return null;

                // Address bar conditions for various browsers
                var conditions = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"), // Chrome, Edge (Korean)
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"), // Chrome, Edge (English)
                    new PropertyCondition(AutomationElement.NameProperty, "주소 표시줄"), // Whale (Korean)
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "urlbar-input") // Firefox
                );

                var addressBar = element.FindFirst(TreeScope.Descendants, conditions);

                if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    return ((ValuePattern)pattern).Current.Value as string;
                }
            }
            catch { /* Accessibility errors can be ignored */ }

            // Fallback: If URL fetch fails, return the window title for browsers
            string processName = GetActiveProcessName();
            if (processName == "firefox" || processName == "chrome" || processName == "msedge" || processName == "whale")
            {
                return GetActiveWindowTitle();
            }

            return null;
        }
    }
}

