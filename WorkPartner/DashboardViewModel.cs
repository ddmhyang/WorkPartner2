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
        public event Action TaskStoppedAndSaved;

        private Dictionary<string, TimeSpan> _dailyTaskTotals = new Dictionary<string, TimeSpan>();
        private TimeSpan _totalTimeTodayFromLogs;
        public event EventHandler TimerStoppedAndSaved;

        #endregion

        #region --- UI와 바인딩될 속성 ---

        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        public string _totalTimeTodayDisplayText = "오늘의 작업 시간 | 00:00:00";
        public string TotalTimeTodayDisplayText
        {
            get => _totalTimeTodayDisplayText;
            set => SetProperty(ref _totalTimeTodayDisplayText, value);
        }

        private string _currentTaskDisplayText = "없음";
        public string CurrentTaskDisplayText
        {
            get => _currentTaskDisplayText;
            set => SetProperty(ref _currentTaskDisplayText, value);
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

        // 🎯 수정 후
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

            // ✨ [버그 2 수정] DataManager의 설정 업데이트 이벤트를 구독합니다.
            DataManager.SettingsUpdated += OnSettingsUpdated;

            LoadInitialDataAsync();
        }

        // ✨ [버그 2 수정] 파일의 아무 곳에나 이 메서드를 추가하세요 (예: LoadInitialDataAsync 근처)
        private void OnSettingsUpdated()
        {
            // ISettingsService를 통해 최신 설정을 다시 로드합니다.
            _settings = _settingsService.LoadSettings();
            System.Diagnostics.Debug.WriteLine("DashboardViewModel: Settings reloaded.");
        }

        private async void LoadInitialDataAsync()
        {
            _settings = _settingsService.LoadSettings();

            var loadedTasks = await _taskService.LoadTasksAsync();
            foreach (var task in loadedTasks) TaskItems.Add(task);

            var loadedLogs = await _timeLogService.LoadTimeLogsAsync();
            foreach (var log in loadedLogs) TimeLogEntries.Add(log);

            RecalculateDailyTotals();
            UpdateLiveTimeDisplays();
            _timerService.Start();
        }

        private void RecalculateDailyTotals()
        {
            _dailyTaskTotals.Clear();
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == DateTime.Today);

            _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));

            _dailyTaskTotals = todayLogs
                .GroupBy(log => log.TaskText)
                .ToDictionary(g => g.Key, g => new TimeSpan(g.Sum(l => l.Duration.Ticks)));
        }

        private void OnSelectedTaskChanged(TaskItem newSelectedTask)
        {
            CurrentTaskDisplayText = newSelectedTask?.Text ?? "없음";
            CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
            UpdateLiveTimeDisplays();

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

        // ✨ [추가] AI 태그 규칙을 검사하고 현재 과목을 자동 변경하는 메서드
        private void CheckTagRules(string activeTitle, string activeUrl)
        {
            if (_settings.TagRules == null || _settings.TagRules.Count == 0) return;

            // 창 제목과 URL을 합쳐서 키워드 검사
            string combinedText = (activeTitle + " " + activeUrl).ToLower();

            foreach (var rule in _settings.TagRules)
            {
                string keyword = rule.Key.ToLower();
                if (combinedText.Contains(keyword))
                {
                    string targetTaskName = rule.Value;

                    // 현재 선택된 과목과 규칙이 일치하는 과목이 다를 경우에만 변경
                    if (SelectedTaskItem == null || !SelectedTaskItem.Text.Equals(targetTaskName, StringComparison.OrdinalIgnoreCase))
                    {
                        var foundTask = TaskItems.FirstOrDefault(t => t.Text.Equals(targetTaskName, StringComparison.OrdinalIgnoreCase));
                        if (foundTask != null)
                        {
                            SelectedTaskItem = foundTask;
                            Debug.WriteLine($"AI Tag Rule applied: '{rule.Key}' -> '{targetTaskName}'");
                            break; // 첫 번째 일치하는 규칙만 적용
                        }
                    }
                }
            }
        }

        private void HandleStopwatchMode()
        {
            if (_settings == null) return;

            // ✨ [수정] 활성 창 제목을 가져오도록 추가
            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;
            string activeTitle = ActiveWindowHelper.GetActiveWindowTitle()?.ToLower() ?? string.Empty; // ✨[추가]

            // ✨ [추가] 태그 규칙을 먼저 검사하여 현재 과목(SelectedTaskItem)을 변경
            CheckTagRules(activeTitle, activeUrl);

            Action stopAndLogAction = () =>
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : null);
                    _stopwatch.Reset();
                    IsRunningChanged?.Invoke(false);
                    // ✨ [수정] 태그 규칙에 의해 과목이 선택되었을 수 있으므로 "없음"으로 강제 변경하지 않습니다.
                    // CurrentTaskDisplayText = "없음"; 
                    // CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
                }
                _isPausedForIdle = false;
            };

            // ✨ [수정] 방해 프로세스 검사에 창 제목(activeTitle)도 포함 (더 정밀한 차단)
            if (_settings.DistractionProcesses.Any(p => activeProcess.Contains(p) ||
                                                        (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                                                        (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))))
            {
                stopAndLogAction();
                return;
            }

            // ✨ [수정] 작업 프로세스 검사에 창 제목(activeTitle)도 포함
            if (_settings.WorkProcesses.Any(p => activeProcess.Contains(p) ||
                                                  (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                                                  (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))))
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
                        // ✨ [수정] AI 태그 규칙에 의해 SelectedTaskItem이 이미 설정되었을 수 있습니다.
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
            // ✨ [오류 수정] .ToList()를 제거하여 올바른 타입으로 데이터를 넘겨줍니다.
            _timeLogService.SaveTimeLogsAsync(TimeLogEntries);

            TaskStoppedAndSaved?.Invoke();

            var duration = entry.Duration;
            if (_dailyTaskTotals.ContainsKey(entry.TaskText))
            {
                _dailyTaskTotals[entry.TaskText] += duration;
            }
            else
            {
                _dailyTaskTotals[entry.TaskText] = duration;
            }
            _totalTimeTodayFromLogs += duration;
            // ✨ [추가] 모든 저장이 끝났으니, 이 이벤트를 구독하는 모든 곳(DashboardPage)에 신호를 보냅니다.
            TimerStoppedAndSaved?.Invoke(this, EventArgs.Empty);
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