// 🎯 아래 코드로 ActiveWindowHelper.cs 파일 전체를 교체하세요.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation; // URL 감지를 위해 필요

namespace WorkPartner
{
    public static class ActiveWindowHelper
    {
        // --- Windows API 임포트 ---

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        // ✨ [추가] 유휴 시간 감지를 위해 Branch 6에서 가져온 코드
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 cbSize;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTime;
        }

        // ✨ [추가] 유휴 시간 감지를 위해 Branch 6에서 가져온 코드
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);


        // --- 공개 메서드 ---

        /// <summary>
        /// ✨ [추가] 유휴 시간 감지 메서드 (Branch 6)
        /// </summary>
        public static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (GetLastInputInfo(ref lastInputInfo))
            {
                var lastInputTick = lastInputInfo.dwTime;
                // Environment.TickCount는 부팅 후 경과 시간(ms)
                var idleTime = Environment.TickCount - lastInputTick;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            return TimeSpan.Zero;
        }

        private const int ApiTimeoutMs = 200;

        /// <summary>
        /// 현재 활성화된 창의 프로세스 이름을 가져옵니다. (예: "chrome", "explorer")
        /// </summary>
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

                        // ✨ [핵심 수정]
                        // "notepad.exe"에서 ".exe"를 제거하고 "notepad"만 반환하도록 수정
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

        /// <summary>
        /// 현재 활성화된 창의 제목을 가져옵니다.
        /// </summary>
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

        /// <summary>
        /// 활성화된 브라우저(Chrome, Edge, Firefox, Whale)의 URL을 가져옵니다.
        /// (안정적인 Branch 5 버전 로직)
        /// </summary>
        public static string GetActiveBrowserTabUrl()
        {
            try
            {
                // 1. 현재 활성 프로세스 이름 확인
                string processName = GetActiveProcessName();
                if (string.IsNullOrEmpty(processName)) return null;

                // 2. 프로세스 ID로 실제 프로세스 객체 가져오기
                IntPtr handle = GetForegroundWindow();
                GetWindowThreadProcessId(handle, out uint processId);
                Process proc = Process.GetProcessById((int)processId);

                // 3. 브라우저별로 분기
                switch (processName)
                {
                    case "chrome":
                    case "msedge":
                    case "whale":
                        return GetUrlFromBrowser(proc, processName);

                    case "firefox":
                        return GetUrlFromBrowser(proc, "firefox");

                    default:
                        return null; // 지원되는 브라우저가 아님
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// UI Automation을 사용해 브라우저 주소창의 URL을 가져오는 헬퍼 메서드
        /// (안정적인 Branch 5 버전 로직)
        /// </summary>
        private static string GetUrlFromBrowser(Process proc, string browserName)
        {
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // 1. 메인 창 핸들에서 AutomationElement 얻기
                AutomationElement rootElement = AutomationElement.FromHandle(proc.MainWindowHandle);
                if (rootElement == null) return null;

                // 2. 주소창(Edit Control) 찾기
                Condition editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                AutomationElement addressBar = rootElement.FindFirst(TreeScope.Descendants, editCondition);

                if (addressBar == null) return null;

                // 3. 주소창의 'Value' 패턴 가져오기
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
                // (예: 창이 닫히거나 권한이 없는 경우)
                return null;
            }
        }
    }
}