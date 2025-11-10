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

        private readonly IDialogService _dialogService;
        private DateTime _lastFocusNagTime = DateTime.MinValue; // 경고 스팸 방지용

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
            _dialogService = dialogService; // ✨ [버그 2-2 수정] dialogService를 멤버 변수에 저장합니다.
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

        // 🎯 [수정] DashboardViewModel.cs (HandleStopwatchMode 메서드)
        // 기존 HandleStopwatchMode 메서드 전체를 아래 코드로 교체하세요.

        private void HandleStopwatchMode()
        {
            // 1. 설정 확인
            if (_settings == null)
            {
                return;
            }

            // 2. 현재 활성 창 정보 가져오기
            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;
            string activeTitle = ActiveWindowHelper.GetActiveWindowTitle()?.ToLower() ?? string.Empty;

            // 3. (AI 태그 규칙 검사 - 기존 코드)
            CheckTagRules(activeTitle, activeUrl);

            // 4. 타이머 중지 및 저장을 위한 람다 (기존 코드)
            Action stopAndLogAction = () =>
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : null);
                    _stopwatch.Reset();
                    IsRunningChanged?.Invoke(false);
                }
                _isPausedForIdle = false;
            };

            // 5. '방해 앱'인지 확인
            bool isDistraction = _settings.DistractionProcesses.Any(p =>
                activeProcess.Contains(p) ||
                (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
            );

            if (isDistraction)
            {
                // 6. '집중 모드'가 켜져 있는지 확인
                if (_settings.IsFocusModeEnabled)
                {
                    var elapsedSinceLastNag = (DateTime.Now - _lastFocusNagTime).TotalSeconds;

                    // 7. 경고창 스팸 방지 시간 확인
                    if (elapsedSinceLastNag > _settings.FocusModeNagIntervalSeconds)
                    {
                        _lastFocusNagTime = DateTime.Now;
                        _dialogService.ShowAlert(_settings.FocusModeNagMessage, "집중 모드 경고");
                    }
                }

                // 8. 방해 앱이므로 타이머 중지
                stopAndLogAction();
                return;
            }

            // 9. '작업 앱'인지 확인 (기존 코드)
            if (_settings.WorkProcesses.Any(p => activeProcess.Contains(p) ||
                                                  (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                                                  (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))))
            {
                // (자리 비움 감지 로직...)
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
                        // (타이머 시작 로직...)
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
            else // 10. '작업 앱'도 '방해 앱'도 아닌 경우
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


        // ✨ [버그 1-1 수정] 이 public 메서드를 DashboardViewModel.cs에 새로 추가하세요.
        public void RecalculateAllTotalsFromLogs()
        {
            RecalculateDailyTotals(); // 1. VM의 내부 합계(Dictionary)를 다시 계산합니다.
            UpdateLiveTimeDisplays(); // 2. VM의 UI 속성(Text)을 새 합계로 업데이트합니다.
        }

    }
}