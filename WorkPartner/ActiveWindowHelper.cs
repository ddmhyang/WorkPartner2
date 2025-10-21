// 𝙃𝙚𝙧𝙚'𝙨 𝙩𝙝𝙚 𝙘𝙤𝙙𝙚 𝙞𝙣 ddmhyang/workpartner2/WorkPartner2-4/WorkPartner/ActiveWindowHelper.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks; // ✨ Task(비동기) 사용을 위해 추가
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

        // ✨ [추가] 멈춤 현상을 방지하기 위한 타임아웃 시간 (밀리초 단위)
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
                // ✨ [수정] 모든 P/Invoke 및 프로세스 접근 코드를 Task.Run 내부로 이동시켰습니다.
                // 이렇게 하면 GetForegroundWindow() 또는 GetWindowThreadProcessId()에서 멈춤 현상이 발생해도
                // 메인 스레드가 정지하지 않고 ApiTimeoutMs 이후에 작업을 중단할 수 있습니다.
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

                // ✨ 지정된 시간 안에 작업이 완료되면 결과 반환, 아니면 빈 문자열 반환
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
                // ✨ [수정] UI 자동화 전체 로직을 별도 스레드에서 타임아웃을 갖고 실행
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

                // ✨ 지정된 시간 안에 작업이 완료되면 결과 반환, 아니면 null 반환
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