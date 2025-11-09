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
            if (_settings == null) return;

            // 1. "notepad" ( .exe가 없는) 이름을 가져옵니다.
            string activeProcessName = ActiveWindowHelper.GetActiveProcessName()?.ToLower();
            if (string.IsNullOrEmpty(activeProcessName))
            {
                Pause();
                return;
            }

            // 2. [방해 앱 - 프로세스]
            // ✨ [수정] "notepad" == "notepad" 비교
            if (_settings.DistractionProcesses.Any(p => activeProcessName == p))
            {
                Pause();
                return;
            }

            // 3. [작업 앱 - 프로세스]
            // ✨ [수정] "notepad" == "notepad" 비교
            if (_settings.WorkProcesses.Any(p => activeProcessName == p))
            {
                if (IsPaused) Resume();
                return;
            }

            // 4. https://www.wordreference.com/enko/%EA%B2%80%EC%82%AC
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower();
            if (!string.IsNullOrEmpty(activeUrl))
            {
                // 5. [방해 앱 - URL] (이건 Contains가 맞습니다)
                if (_settings.DistractionProcesses.Any(p => activeUrl.Contains(p)))
                {
                    Pause();
                    return;
                }
            }

            // 6. [수동 앱]
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