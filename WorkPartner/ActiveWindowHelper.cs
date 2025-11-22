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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 cbSize;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);


        public static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (GetLastInputInfo(ref lastInputInfo))
            {
                var lastInputTick = lastInputInfo.dwTime;
                var idleTime = Environment.TickCount - lastInputTick;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            return TimeSpan.Zero;
        }

        private const int ApiTimeoutMs = 200;
        public static string GetActiveProcessName()
        {
            string processName = string.Empty;
            try
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        IntPtr handle = GetForegroundWindow();
                        if (handle == IntPtr.Zero) return string.Empty;

                        GetWindowThreadProcessId(handle, out uint processId);
                        if (processId == 0) return string.Empty;

                        Process proc = Process.GetProcessById((int)processId);

                        string name = proc.ProcessName.ToLower();
                        if (name.EndsWith(".exe"))
                        {
                            name = name.Substring(0, name.Length - 4);
                        }
                        return name;
                    }
                    catch { return string.Empty; }
                });

                if (task.Wait(TimeSpan.FromMilliseconds(ApiTimeoutMs)))
                {
                    processName = task.Result;
                }
                else
                {
                    Debug.WriteLine($"[Timeout] GetActiveProcessName timed out.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] GetActiveProcessName: {ex.Message}");
            }
            return processName;
        }

        public static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return null;

                int length = GetWindowTextLength(handle);
                if (length == 0) return null;

                StringBuilder builder = new StringBuilder(length + 1);
                GetWindowText(handle, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string GetActiveBrowserTabUrl()
        {
            try
            {
                string processName = GetActiveProcessName();
                if (string.IsNullOrEmpty(processName)) return null;

                IntPtr handle = GetForegroundWindow();
                GetWindowThreadProcessId(handle, out uint processId);
                Process proc = Process.GetProcessById((int)processId);

                switch (processName)
                {
                    case "chrome":
                    case "msedge":
                    case "whale":
                        return GetUrlFromBrowser(proc, processName);

                    case "firefox":
                        return GetUrlFromBrowser(proc, "firefox");

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string GetUrlFromBrowser(Process proc, string browserName)
        {
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                AutomationElement rootElement = AutomationElement.FromHandle(proc.MainWindowHandle);
                if (rootElement == null) return null;

                Condition editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                AutomationElement addressBar = rootElement.FindFirst(TreeScope.Descendants, editCondition);

                if (addressBar == null) return null;

                if (addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    string url = ((ValuePattern)pattern).Current.Value;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return url;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}