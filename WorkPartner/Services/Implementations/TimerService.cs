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
            if (_settings == null || IsPaused) return;

            string activeProcessName = ActiveWindowHelper.GetActiveProcessName()?.ToLower();
            if (string.IsNullOrEmpty(activeProcessName)) return;

            if (_settings.DistractionProcesses.Any(p => activeProcessName.Contains(p)))
            {
                Pause();
            }
            else if (_settings.WorkProcesses.Any(p => activeProcessName.Contains(p)))
            {
                if (IsPaused)
                {
                    Resume();
                }
            }
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