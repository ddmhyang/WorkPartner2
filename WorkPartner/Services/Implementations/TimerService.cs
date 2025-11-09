// 파일: WorkPartner/Services/Implementations/TimerService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace WorkPartner.Services.Implementations
{
    public class TimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch;
        private AppSettings _settings;

        // ✨ [수정] 인터페이스와 일치하도록 이벤트 이름을 'Tick'으로 변경합니다.
        public event Action<TimeSpan> Tick;

        public TimerService()
        {
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;

            LoadSettings();
            DataManager.SettingsUpdated += LoadSettings;
        }

        private void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
            Debug.WriteLine("TimerService: Settings reloaded.");
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // ✨ [수정] 변경된 이벤트 이름 'Tick'을 사용합니다.
            Tick?.Invoke(_stopwatch.Elapsed);
            CheckActiveWindow();
        }

        private void CheckActiveWindow()
        {
            // 0. 설정이 로드되지 않았으면 아무것도 하지 않음
            if (_settings == null) return;

            // 1. 활성 프로세스 이름 가져오기 (이제 "notepad.exe"가 아닌 "notepad"가 반환됨)
            string activeProcessName = ActiveWindowHelper.GetActiveProcessName()?.ToLower();
            if (string.IsNullOrEmpty(activeProcessName))
            {
                // 활성 창이 없으면 (예: 바탕화면 클릭) '수동 앱'으로 간주
                Pause();
                return;
            }

            // 2. [방해 앱 검사 - 프로세스]
            // "notepad" == "notepad" 비교 (정확한 일치)
            if (_settings.DistractionProcesses.Any(p => activeProcessName == p))
            {
                Pause();
                return;
            }

            // 3. [작업 앱 검사 - 프로세스]
            // "notepad" == "notepad" 비교 (정확한 일치)
            if (_settings.WorkProcesses.Any(p => activeProcessName == p))
            {
                if (IsPaused)
                {
                    Resume();
                }
                return; // 작업 앱이므로 URL 검사 불필요
            }

            // --- 여기까지 왔다면, 프로세스가 '작업 앱'도 '방해 앱'도 아닌 경우 ---

            // 4. https://www.wordreference.com/enko/%EA%B2%80%EC%82%AC 브라우저 URL 검사
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower();
            if (!string.IsNullOrEmpty(activeUrl))
            {
                // 5. [방해 앱 검사 - URL]
                // (예: "www.youtube.com".Contains("youtube.com"))
                if (_settings.DistractionProcesses.Any(p => activeUrl.Contains(p)))
                {
                    Pause();
                    return;
                }
            }

            // 6. [수동 앱 처리] (모두 아닐 경우)
            // '수동 앱'(탐색기, 카톡 등) 또는 URL 검사를 통과한 브라우저(예: google.com)로 간주
            Pause();
        }

        public bool IsRunning => _timer.IsEnabled;
        public bool IsPaused { get; private set; }

        public void Start()
        {
            if (!IsRunning)
            {
                _stopwatch.Start();
                _timer.Start();
                IsPaused = false;
            }
        }

        public void Stop()
        {
            _stopwatch.Reset();
            _timer.Stop();
            IsPaused = false;
            // ✨ [수정] 변경된 이벤트 이름 'Tick'을 사용합니다.
            Tick?.Invoke(TimeSpan.Zero);
        }

        public void Pause()
        {
            if (IsRunning && !IsPaused)
            {
                _stopwatch.Stop();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (IsRunning && IsPaused)
            {
                _stopwatch.Start();
                IsPaused = false;
            }
        }
    }
}