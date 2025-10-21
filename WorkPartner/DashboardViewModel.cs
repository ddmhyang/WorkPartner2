using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using WorkPartner.Services;

namespace WorkPartner.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region --- 서비스 및 멤버 변수 선언 ---

        private readonly ITimerService _timerService;
        private readonly ISettingsService _settingsService;
        private readonly ITaskService _taskService;
        private readonly ITimeLogService _timeLogService;

        private readonly Stopwatch _stopwatch;
        private AppSettings _settings;

        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }
        public event Action<string> TimeUpdated;
        public event Action<string> CurrentTaskChanged;
        public event Action<bool> IsRunningChanged;

        private Dictionary<string, TimeSpan> _dailyTaskTotals = new Dictionary<string, TimeSpan>();
        private TimeSpan _totalTimeTodayFromLogs;

        #endregion

        #region --- UI 바인딩 속성 ---

        public string Nickname => _settings.Nickname; // 추가
        public int Level => _settings.Level;         // 추가
        public int Points => _settings.Points;       // 기존 Points 속성과 연결

        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        private string _totalTimeTodayDisplayText;
        public string TotalTimeTodayDisplayText
        {
            get => _totalTimeTodayDisplayText;
            set => SetProperty(ref _totalTimeTodayDisplayText, value);
        }

        private TaskItem _selectedTaskItem;
        public TaskItem SelectedTaskItem
        {
            get => _selectedTaskItem;
            set
            {
                if (SetProperty(ref _selectedTaskItem, value))
                {
                    CurrentTaskChanged?.Invoke(value?.Text);
                    UpdateLiveTimeDisplays();
                }
            }
        }
        #endregion

        public DashboardViewModel(ITimerService timerService, ISettingsService settingsService, ITaskService taskService, ITimeLogService timeLogService)
        {
            _timerService = timerService;
            _settingsService = settingsService;
            _taskService = taskService;
            _timeLogService = timeLogService;

            _stopwatch = new Stopwatch();
            _settings = _settingsService.LoadSettings();

            TimeLogEntries = new ObservableCollection<TimeLogEntry>();

            _timerService.Tick += OnTick;
            _timerService.Start();
        }

        public bool IsRunning() => _stopwatch.IsRunning;

        public void Start(TaskItem task)
        {
            if (task == null) return;
            if (_stopwatch.IsRunning) Stop();

            _currentWorkingTask = task;
            _sessionStartTime = DateTime.Now;
            _stopwatch.Restart();

            IsRunningChanged?.Invoke(true);
            CurrentTaskChanged?.Invoke(_currentWorkingTask.Text);
        }

        public void Stop()
        {
            if (!_stopwatch.IsRunning) return;

            var elapsed = _stopwatch.Elapsed;
            _stopwatch.Stop();
            _stopwatch.Reset();

            if (elapsed > TimeSpan.FromSeconds(1))
            {
                SaveCurrentSession(elapsed);
            }

            _currentWorkingTask = null;
            IsRunningChanged?.Invoke(false);
            UpdateLiveTimeDisplays();
        }

        private void OnTick(object sender, EventArgs e)
        {
            UpdateLiveTimeDisplays();
        }

        private void SaveCurrentSession(TimeSpan duration)
        {
            var log = new TimeLogEntry
            {
                Timestamp = _sessionStartTime,
                TaskName = _currentWorkingTask.Text,
                Duration = (int)duration.TotalSeconds,
                ApplicationName = "manual"
            };
            TimeLogEntries.Add(log);
            _timeLogService.SaveTimeLog(log);

            if (_dailyTaskTotals.ContainsKey(log.TaskName))
            {
                _dailyTaskTotals[log.TaskName] += duration;
            }
            else
            {
                _dailyTaskTotals[log.TaskName] = duration;
            }
            _totalTimeTodayFromLogs += duration;
        }

        private void UpdateLiveTimeDisplays()
        {
            var totalTimeToday = _totalTimeTodayFromLogs;
            if (_stopwatch.IsRunning)
            {
                totalTimeToday += _stopwatch.Elapsed;
            }
            TotalTimeTodayDisplayText = $"오늘의 작업 시간 | {totalTimeToday:hh\\:mm\\:ss}";

            var timeForSelectedTask = TimeSpan.Zero;
            if (SelectedTaskItem != null && _dailyTaskTotals.TryGetValue(SelectedTaskItem.Text, out var storedTime))
            {
                timeForSelectedTask = storedTime;
            }

            if (_stopwatch.IsRunning && _currentWorkingTask == SelectedTaskItem)
            {
                timeForSelectedTask += _stopwatch.Elapsed;
            }

            string newTime = timeForSelectedTask.ToString(@"hh\:mm\:ss");
            MainTimeDisplayText = newTime;

            TimeUpdated?.Invoke(newTime);
        }

        #region --- INotifyPropertyChanged 구현 ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, newValue)) return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion
    }
}

