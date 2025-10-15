using System;
using System.Linq;
using System.Windows.Threading;
using WorkPartner.Services;

namespace WorkPartner.Services.Implementations
{
    public class TimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;
        private readonly ITimeLogService _timeLogService;
        private readonly ISettingsService _settingsService;

        public bool IsRunning => _timer.IsEnabled;
        public string CurrentTask { get; private set; }
        public event Action TimerStateChanged;
        public event Action<TimeSpan> TimeUpdated;

        public TimerService(ITimeLogService timeLogService, ISettingsService settingsService)
        {
            _timeLogService = timeLogService;
            _settingsService = settingsService;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
        }

        public void Start(string task)
        {
            CurrentTask = task;
            _timer.Start();
            TimerStateChanged?.Invoke();
        }

        public void Stop()
        {
            _timer.Stop();
            CurrentTask = null;
            TimerStateChanged?.Invoke();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var settings = _settingsService.LoadSettings();
            var activeAppName = ActiveWindowHelper.GetActiveWindowInfo().ProcessName;
            bool isProductive = settings.ProductiveApps.Any(p => p.IsSelected && p.Name.Equals(activeAppName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(CurrentTask) && isProductive)
            {
                _timeLogService.LogTime(CurrentTask, activeAppName, 1);
            }

            var totalTime = _timeLogService.GetTotalTimeForTask(CurrentTask, DateTime.Now);
            TimeUpdated?.Invoke(totalTime); // 1초마다 신호 보내기
        }
    }
}