// 𝙃𝙚𝙧𝙚'𝙨 𝙩𝙝𝙚 𝙘𝙤𝙙𝙚 𝙞𝙣 ddmhyang/workpartner2/WorkPartner2-4/WorkPartner/ActiveWindowHelper.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.ComponentModel; // Win32Exception 처리를 위해 추가

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
            // ✨ [수정] 안정성을 위해 예외 처리 추가
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
                return null; // 오류 발생 시 안전하게 null 반환
            }
        }

        public static string GetActiveProcessName()
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
            // ✨ [수정] 권한 문제로 인한 충돌을 막기 위한 예외 처리 강화
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"[Handled Error] GetActiveProcessName (Permission Denied?): {ex.Message}");
                return string.Empty; // 권한 오류 발생 시 안전하게 빈 문자열 반환
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] GetActiveProcessName: {ex.Message}");
                return string.Empty; // 기타 오류 발생 시에도 안전하게 반환
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
            // ✨ [수정] UI 자동화 오류로 인한 프로그램 멈춤을 방지하기 위해 전체를 try-catch로 감쌈
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
                // Figma와 같은 앱에서 호환성 오류가 발생해도 무시하고 넘어가도록 처리
                Debug.WriteLine($"[Handled Error] GetActiveBrowserTabUrl (Compatibility issue?): {ex.Message}");
            }
            return null; // 오류 발생 시 안전하게 null 반환
        }
    }
}