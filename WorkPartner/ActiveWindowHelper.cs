using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.ComponentModel;

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

        private const int ApiTimeoutMs = 200;

        public static string GetActiveWindowTitle()
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] GetActiveWindowTitle: {ex.Message}");
                return null;
            }
        }

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
                        return proc.ProcessName.ToLower();
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
            string url = null;
            try
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        IntPtr handle = GetForegroundWindow();
                        if (handle == IntPtr.Zero) return null;

                        AutomationElement element = AutomationElement.FromHandle(handle);
                        if (element == null) return null;

                        var conditions = new OrCondition(
                            new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"),
                            new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"),
                            new PropertyCondition(AutomationElement.NameProperty, "주소 표시줄"),
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "urlbar-input"),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                        );

                        var addressBar = element.FindFirst(TreeScope.Descendants, conditions);

                        if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                        {
                            return ((ValuePattern)pattern).Current.Value as string;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Handled Error in Task] GetActiveBrowserTabUrl: {ex.Message}");
                    }
                    return null;
                });

                if (task.Wait(TimeSpan.FromMilliseconds(ApiTimeoutMs)))
                {
                    url = task.Result;
                }
                else
                {
                    Debug.WriteLine("[Timeout] GetActiveBrowserTabUrl timed out.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] GetActiveBrowserTabUrl: {ex.Message}");
            }

            return url;
        }
    }
}