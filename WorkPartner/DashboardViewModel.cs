// 𝙃𝙚𝙧𝙚'𝙨 𝙩𝙝𝙚 𝙘𝙤𝙙𝙚 𝙞𝙣 ddmhyang/workpartner2/WorkPartner2-4/WorkPartner/DashboardViewModel.cs
using System;
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
        private TimeSpan _totalTimeTodayFromLogs;
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }

        public event Action<string> TimeUpdated;
        public event Action<string> CurrentTaskChanged;
        public event Action<bool> IsRunningChanged;

        #endregion

        #region --- UI와 바인딩될 속성 ---

        // ✨ [수정] 이제 '선택된 과목'의 시간을 표시합니다.
        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        private string _currentTaskDisplayText = "없음";
        public string CurrentTaskDisplayText
        {
            get => _currentTaskDisplayText;
            set => SetProperty(ref _currentTaskDisplayText, value);
        }

        // ✨ [추가] '오늘의 총 작업 시간'을 표시하기 위한 새 속성
        private string _totalTimeTodayDisplayText = "오늘의 작업 시간 | 00:00:00";
        public string TotalTimeTodayDisplayText
        {
            get => _totalTimeTodayDisplayText;
            set => SetProperty(ref _totalTimeTodayDisplayText, value);
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
            UpdateLiveTimeDisplays(); // 초기 UI 업데이트
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
            CurrentTaskDisplayText = newSelectedTask?.Text ?? "없음";
            CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
            UpdateLiveTimeDisplays(); // ✨ [추가] 과목 선택 시 즉시 시간 표시 업데이트

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
            HandleStopwatchMode();
            UpdateLiveTimeDisplays();
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
                    IsRunningChanged?.Invoke(false);
                    CurrentTaskDisplayText = "없음";
                    CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
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
                            IsRunningChanged?.Invoke(true);
                            CurrentTaskDisplayText = _currentWorkingTask.Text;
                            CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
                        }
                    }
                }
            }
            else
            {
                stopAndLogAction();
            }
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

        // ✨ [수정] 두 개의 시간 표시를 모두 업데이트하도록 로직 변경
        private void UpdateLiveTimeDisplays()
        {
            // 1. 오늘의 '총' 작업 시간 계산 (작은 TextBlock용)
            var totalTimeToday = _totalTimeTodayFromLogs;
            if (_stopwatch.IsRunning)
            {
                totalTimeToday += _stopwatch.Elapsed;
            }
            TotalTimeTodayDisplayText = $"오늘의 작업 시간 | {totalTimeToday:hh\\:mm\\:ss}";

            // 2. '선택된 과목'의 작업 시간 계산 (큰 TextBlock용)
            var timeForSelectedTask = TimeSpan.Zero;
            if (SelectedTaskItem != null)
            {
                var selectedTaskLogs = TimeLogEntries
                    .Where(log => log.TaskText == SelectedTaskItem.Text && log.StartTime.Date == DateTime.Today);
                timeForSelectedTask = new TimeSpan(selectedTaskLogs.Sum(log => log.Duration.Ticks));

                // 선택된 과목이 현재 작업중인 과목과 같을 때만 실시간 시간 추가
                if (_stopwatch.IsRunning && _currentWorkingTask == SelectedTaskItem)
                {
                    timeForSelectedTask += _stopwatch.Elapsed;
                }
            }
            MainTimeDisplayText = timeForSelectedTask.ToString(@"hh\:mm\:ss");

            // 3. 미니 타이머에는 '선택된 과목'의 시간을 전송
            TimeUpdated?.Invoke(MainTimeDisplayText);
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