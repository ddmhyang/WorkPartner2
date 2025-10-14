using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private string _lastActiveProcessName = string.Empty;

        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs;
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }

        #endregion

        #region --- UI와 바인딩될 속성 ---

        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        public ObservableCollection<TaskItem> TaskItems { get; private set; }

        private TaskItem _selectedTaskItem;
        public TaskItem SelectedTaskItem
        {
            get => _selectedTaskItem;
            set
            {
                if (SetProperty(ref _selectedTaskItem, value))
                {
                    OnSelectedTaskChanged(value);
                }
            }
        }

        #endregion

        public DashboardViewModel(ITaskService taskService, IDialogService dialogService, ISettingsService settingsService, ITimerService timerService, ITimeLogService timeLogService)
        {
            _taskService = taskService;
            _settingsService = settingsService;
            _timerService = timerService;
            _timeLogService = timeLogService;

            _stopwatch = new Stopwatch();
            TaskItems = new ObservableCollection<TaskItem>();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();

            _timerService.Tick += OnTimerTick;
            LoadInitialDataAsync();
        }

        private async void LoadInitialDataAsync()
        {
            _settings = _settingsService.LoadSettings();

            var loadedTasks = await _taskService.LoadTasksAsync();
            foreach (var task in loadedTasks) TaskItems.Add(task);

            var loadedLogs = await _timeLogService.LoadTimeLogsAsync();
            foreach (var log in loadedLogs) TimeLogEntries.Add(log);

            RecalculateTotalTimeToday();
            _timerService.Start();
        }

        private void RecalculateTotalTimeToday()
        {
            _totalTimeTodayFromLogs = new TimeSpan(TimeLogEntries
                .Where(log => log.StartTime.Date == DateTime.Today)
                .Sum(log => log.Duration.Ticks));
        }

        private void OnSelectedTaskChanged(TaskItem newSelectedTask)
        {
            if (_currentWorkingTask != newSelectedTask)
            {
                if (_stopwatch.IsRunning)
                {
                    LogWorkSession();
                    _stopwatch.Reset();
                }
                _currentWorkingTask = newSelectedTask;
            }
        }

        private void OnTimerTick(TimeSpan ignored)
        {
            string activeProcess = ActiveWindowHelper.GetActiveProcessName();

            if (activeProcess == _lastActiveProcessName && !string.IsNullOrEmpty(activeProcess))
            {
                UpdateLiveTimeDisplays();
                return;
            }

            _lastActiveProcessName = activeProcess;
            HandleStopwatchMode();
        }

        private void HandleStopwatchMode()
        {
            if (_settings == null) return;

            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;

            Action stopAndLogAction = () =>
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : null);
                    _stopwatch.Reset();
                }
                _isPausedForIdle = false;
            };

            if (_settings.DistractionProcesses.Any(p => activeProcess.Contains(p) || (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p))))
            {
                stopAndLogAction();
                return;
            }

            if (_settings.WorkProcesses.Any(p => activeProcess.Contains(p) || (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p))))
            {
                bool isPassive = _settings.PassiveProcesses.Any(p => activeProcess.Contains(p));
                bool isCurrentlyIdle = _settings.IsIdleDetectionEnabled && !isPassive && ActiveWindowHelper.GetIdleTime().TotalSeconds > _settings.IdleTimeoutSeconds;

                if (isCurrentlyIdle)
                {
                    if (_stopwatch.IsRunning)
                    {
                        _stopwatch.Stop();
                        _isPausedForIdle = true;
                        _idleStartTime = DateTime.Now;
                    }
                    else if (_isPausedForIdle && (DateTime.Now - _idleStartTime).TotalSeconds > IdleGraceSeconds)
                    {
                        LogWorkSession(_sessionStartTime.Add(_stopwatch.Elapsed));
                        _stopwatch.Reset();
                        _isPausedForIdle = false;
                    }
                }
                else
                {
                    if (_isPausedForIdle)
                    {
                        _isPausedForIdle = false;
                        _stopwatch.Start();
                    }
                    else if (!_stopwatch.IsRunning)
                    {
                        _currentWorkingTask = SelectedTaskItem;
                        if (_currentWorkingTask == null && TaskItems.Any())
                        {
                            SelectedTaskItem = TaskItems.First();
                            _currentWorkingTask = SelectedTaskItem;
                        }

                        if (_currentWorkingTask != null)
                        {
                            _sessionStartTime = DateTime.Now;
                            _stopwatch.Start();
                        }
                    }
                }
            }
            else
            {
                stopAndLogAction();
            }

            UpdateLiveTimeDisplays();
        }

        private void LogWorkSession(DateTime? endTime = null)
        {
            if (_currentWorkingTask == null || _stopwatch.Elapsed.TotalSeconds < 1)
            {
                _stopwatch.Reset();
                return;
            }

            var entry = new TimeLogEntry
            {
                StartTime = _sessionStartTime,
                EndTime = endTime ?? _sessionStartTime.Add(_stopwatch.Elapsed),
                TaskText = _currentWorkingTask.Text
            };

            TimeLogEntries.Insert(0, entry);
            _timeLogService.SaveTimeLogsAsync(TimeLogEntries);
            RecalculateTotalTimeToday();
        }

        private void UpdateLiveTimeDisplays()
        {
            var timeToDisplay = _totalTimeTodayFromLogs;
            if (_stopwatch.IsRunning)
            {
                timeToDisplay += _stopwatch.Elapsed;
            }
            MainTimeDisplayText = timeToDisplay.ToString(@"hh\:mm\:ss");
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