// 파일: DashboardViewModel.cs
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

        private bool _isInGracePeriod = false;
        private DateTime _gracePeriodStartTime;
        private const int GracePeriodSeconds = 120; // 👈 2분(120초)간의 유예 시간 (이 시간은 조절 가능)

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
            _dialogService = dialogService;
            _settingsService = settingsService;
            _timerService = timerService;
            _timeLogService = timeLogService;

            _stopwatch = new Stopwatch();
            TaskItems = new ObservableCollection<TaskItem>();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();

            _timerService.Tick += OnTimerTick;

            DataManager.SettingsUpdated += OnSettingsUpdated;

            LoadInitialDataAsync();
        }

        private void OnSettingsUpdated()
        {
            _settings = _settingsService.LoadSettings();
            System.Diagnostics.Debug.WriteLine("DashboardViewModel: Settings reloaded.");
        }



        private async void LoadInitialDataAsync()
        {
            _settings = _settingsService.LoadSettings();

            var loadedTasks = await _taskService.LoadTasksAsync();
            foreach (var task in loadedTasks) TaskItems.Add(task);

            // ▼▼▼ [수정] 원본 로직으로 되돌립니다. ▼▼▼
            var loadedLogs = await _timeLogService.LoadTimeLogsAsync();
            foreach (var log in loadedLogs) TimeLogEntries.Add(log);
            // ▲▲▲ [수정 완료] ▲▲▲

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
                    // _stopwatch.Reset(); // ◀ LogWorkSession에서 Reset하므로 여기서 제거
                }
                _currentWorkingTask = newSelectedTask;
            }
        }

        private void OnTimerTick(TimeSpan ignored)
        {
            HandleStopwatchMode();
            UpdateLiveTimeDisplays();
        }

        private void CheckTagRules(string activeTitle, string activeUrl)
        {
            if (_settings.TagRules == null || _settings.TagRules.Count == 0) return;

            string combinedText = (activeTitle + " " + activeUrl).ToLower();

            foreach (var rule in _settings.TagRules)
            {
                string keyword = rule.Key.ToLower();
                if (combinedText.Contains(keyword))
                {
                    string targetTaskName = rule.Value;

                    if (SelectedTaskItem == null || !SelectedTaskItem.Text.Equals(targetTaskName, StringComparison.OrdinalIgnoreCase))
                    {
                        var foundTask = TaskItems.FirstOrDefault(t => t.Text.Equals(targetTaskName, StringComparison.OrdinalIgnoreCase));
                        if (foundTask != null)
                        {
                            SelectedTaskItem = foundTask;
                            Debug.WriteLine($"AI Tag Rule applied: '{rule.Key}' -> '{targetTaskName}'");
                            break;
                        }
                    }
                }
            }
        }


        private void HandleStopwatchMode()
        {
            if (_settings == null) return;

            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;
            string activeTitle = ActiveWindowHelper.GetActiveWindowTitle()?.ToLower() ?? string.Empty;

            CheckTagRules(activeTitle, activeUrl);

            bool isWorkApp = _settings.WorkProcesses.Any(p =>
                activeProcess.Contains(p) ||
                (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
            );
            bool isPassiveApp = _settings.PassiveProcesses.Any(p =>
                activeProcess.Contains(p) ||
                (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
            );

            bool isCurrentlyIdle = false;

            if (!isPassiveApp)
            {
                isCurrentlyIdle = ActiveWindowHelper.GetIdleTime().TotalSeconds >= 10;
            }

            bool isWorkState = (isWorkApp || isPassiveApp) && !isCurrentlyIdle;

            if (isWorkState)
            {
                if (_isInGracePeriod)
                {
                    _isInGracePeriod = false;
                    _stopwatch.Start();
                }
                else if (!_stopwatch.IsRunning)
                {
                    _currentWorkingTask = SelectedTaskItem ?? TaskItems.FirstOrDefault();
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

            // 업무상태 아닐 때 로직
            else
            {
                bool isDistraction = _settings.DistractionProcesses.Any(p =>
                    activeProcess.Contains(p) ||
                    (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                    (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
                );
                if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                    _isInGracePeriod = true;
                    _gracePeriodStartTime = DateTime.Now;
                }
                else if (_isInGracePeriod)
                {
                    if ((DateTime.Now - _gracePeriodStartTime).TotalSeconds > GracePeriodSeconds)
                    {
                        LogWorkSession();
                        // _stopwatch.Reset(); // ◀ LogWorkSession에서 Reset
                        IsRunningChanged?.Invoke(false);
                        _isInGracePeriod = false;
                    }
                    if (isDistraction && _settings.IsFocusModeEnabled)
                    {
                        var elapsedSinceLastNag = (DateTime.Now - _lastFocusNagTime).TotalSeconds;
                        if (elapsedSinceLastNag > _settings.FocusModeNagIntervalSeconds)
                        {
                            _lastFocusNagTime = DateTime.Now;
                            _dialogService.ShowAlert(_settings.FocusModeNagMessage, "집중 모드 경고");
                        }
                    }
                }
                else
                {
                    if (isDistraction && _settings.IsFocusModeEnabled)
                    {
                        var elapsedSinceLastNag = (DateTime.Now - _lastFocusNagTime).TotalSeconds;
                        if (elapsedSinceLastNag > _settings.FocusModeNagIntervalSeconds)
                        {
                            _lastFocusNagTime = DateTime.Now;
                            _dialogService.ShowAlert(_settings.FocusModeNagMessage, "집중 모드 경고");
                        }
                    }
                }
            }
        }

        private void LogWorkSession(DateTime? endTime = null)
        {
            if (_currentWorkingTask == null || _stopwatch.Elapsed.TotalSeconds < 1)
            {
                _stopwatch.Reset();
                return;
            }


            // --- (기존 시간 저장 로직) ---
            var entry = new TimeLogEntry
            {
                StartTime = _sessionStartTime,
                EndTime = endTime ?? _sessionStartTime.Add(_stopwatch.Elapsed),
                TaskText = _currentWorkingTask.Text
            };
            GrantExperience(_stopwatch.Elapsed);

            TimeLogEntries.Insert(0, entry);
            // ▼▼▼ [수정] 비동기 저장을 호출
            _timeLogService.SaveTimeLogsAsync(TimeLogEntries);
            // ▲▲▲

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
            TimerStoppedAndSaved?.Invoke(this, EventArgs.Empty);

            _stopwatch.Reset(); // ◀ (중요) 모든 계산이 끝난 후 리셋
            DataManager.SaveTimeLogsImmediately(TimeLogEntries);
        }

        private void UpdateLiveTimeDisplays()
        {
            var totalTimeToday = _totalTimeTodayFromLogs;
            // (이전 수정) 스톱워치가 실행 중이거나, 유예 기간 중일 때
            if (_stopwatch.IsRunning || _isInGracePeriod)
            {
                totalTimeToday += _stopwatch.Elapsed;
            }
            TotalTimeTodayDisplayText = $"오늘의 작업 시간 | {totalTimeToday:hh\\:mm\\:ss}";

            var timeForSelectedTask = TimeSpan.Zero;
            if (SelectedTaskItem != null && _dailyTaskTotals.TryGetValue(SelectedTaskItem.Text, out var storedTime))
            {
                timeForSelectedTask = storedTime;
            }

            // (이전 수정) 스톱워치가 실행 중이거나 유예 기간 중이고 + 현재 선택된 과목일 때
            if ((_stopwatch.IsRunning || _isInGracePeriod) && _currentWorkingTask == SelectedTaskItem)
            {
                timeForSelectedTask += _stopwatch.Elapsed;
            }

            string newTime = timeForSelectedTask.ToString(@"hh\:mm\:ss");
            MainTimeDisplayText = newTime;

            TimeUpdated?.Invoke(newTime);


            // ▼▼▼ [이 코드 블록을 여기에 추가하세요] ▼▼▼
            // (1초마다 모든 과목 목록의 시간을 실시간으로 업데이트)
            foreach (var task in TaskItems)
            {
                // 1. 저장된 로그에서 기본 시간 가져오기
                TimeSpan taskTotalTime = TimeSpan.Zero;
                if (_dailyTaskTotals.TryGetValue(task.Text, out var storedTaskTime))
                {
                    taskTotalTime = storedTaskTime;
                }

                // 2. 이 과목이 현재 실행 중인 과목이라면, 실시간 스톱워치 시간을 더하기
                if ((_stopwatch.IsRunning || _isInGracePeriod) && _currentWorkingTask == task)
                {
                    taskTotalTime += _stopwatch.Elapsed;
                }

                // 3. TaskItem의 TotalTime 속성을 업데이트합니다.
                //    (이 속성이 변경되면 TaskItem.cs가 자동으로 UI를 갱신합니다)
                task.TotalTime = taskTotalTime;
            }
            // ▲▲▲ [여기까지 추가] ▲▲▲
        }

        #region --- Public CRUD Methods for Page ---

        /// <summary>
        /// (Page에서 호출) 새 수동 로그를 VM 리스트에 추가하고 즉시 저장합니다.
        /// </summary>
        public void AddManualLog(TimeLogEntry newLog)
        {
            if (newLog == null) return;
            TimeLogEntries.Add(newLog);
            DataManager.SaveTimeLogsImmediately(TimeLogEntries);

            // ▼▼▼ [추가] 수동 추가 시 경험치 부여 ▼▼▼
            GrantExperience(newLog.Duration);

            RecalculateDailyTotals();
            UpdateLiveTimeDisplays();
        }

        /// <summary>
        /// (Page에서 호출) 기존 로그를 찾아 삭제하고 즉시 저장합니다.
        /// </summary>
        public void DeleteLog(TimeLogEntry logFromPage)
        {
            if (logFromPage == null) return;

            // 1. VM 리스트에서 '내용'이 같은 원본 객체를 찾습니다.
            var logInVm = TimeLogEntries.FirstOrDefault(l =>
                l.StartTime == logFromPage.StartTime &&
                l.TaskText == logFromPage.TaskText &&
                l.EndTime == logFromPage.EndTime
            );

            if (logInVm != null)
            {
                TimeLogEntries.Remove(logInVm);
                DataManager.SaveTimeLogsImmediately(TimeLogEntries);

                // ▼▼▼ [추가] 삭제 시 경험치 차감 ▼▼▼
                GrantExperience(logInVm.Duration.Negate()); // 시간을 음수로 전달

                RecalculateDailyTotals();
                UpdateLiveTimeDisplays();
            }
        }

        /// <summary>
        /// (Page에서 호출) 기존 로그를 찾아 수정하고 즉시 저장합니다.
        /// </summary>
        public void UpdateLog(TimeLogEntry originalLog, TimeLogEntry updatedLog)
        {
            if (originalLog == null || updatedLog == null) return;

            var logInVm = TimeLogEntries.FirstOrDefault(l =>
                l.StartTime == originalLog.StartTime &&
                l.TaskText == originalLog.TaskText &&
                l.EndTime == originalLog.EndTime
            );

            if (logInVm != null)
            {
                // ▼▼▼ [추가] 변경 전/후 시간 차이 계산 ▼▼▼
                var oldDuration = logInVm.Duration;
                var newDuration = updatedLog.Duration;
                var durationDifference = newDuration - oldDuration;
                // ▲▲▲
                logInVm.StartTime = updatedLog.StartTime;
                logInVm.EndTime = updatedLog.EndTime;
                logInVm.TaskText = updatedLog.TaskText;
                logInVm.FocusScore = updatedLog.FocusScore;

                DataManager.SaveTimeLogsImmediately(TimeLogEntries);
                GrantExperience(durationDifference);
                RecalculateDailyTotals();
                UpdateLiveTimeDisplays();
            }
        }

        #endregion

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


        public void RecalculateAllTotalsFromLogs()
        {
            RecalculateDailyTotals();
            UpdateLiveTimeDisplays();
        }


        // 파일: DashboardViewModel.cs
        // (약 455줄 근처)

        public void Shutdown()
        {
            if (_stopwatch.IsRunning || _isInGracePeriod)
            {
                // ▼▼▼ [수정] 비동기 저장을 동기(즉시) 저장으로 변경 ▼▼▼

                // 1. 마지막 로그 항목 생성
                var entry = new TimeLogEntry
                {
                    StartTime = _sessionStartTime,
                    EndTime = DateTime.Now, // 종료 시점의 현재 시간
                    TaskText = _currentWorkingTask.Text
                };

                // 2. VM 리스트에 추가
                TimeLogEntries.Add(entry);

                // 3. '즉시 저장' 호출
                _timeLogService.SaveTimeLogsAsync(TimeLogEntries); // 👈 [문제의 코드]
                // ▲▲▲ [수정 완료] ▲▲▲

                Debug.WriteLine("VM Shutdown: Final session saved.");
            }
        }

        /// <summary>
/// 작업 시간을 기반으로 경험치와 레벨을 계산하고 적용합니다.
/// (TimeSpan.Negate()를 사용하여 경험치를 차감할 수도 있습니다.)
/// </summary>
/// <param name="workDuration">적용할 작업 시간</param>
private void GrantExperience(TimeSpan workDuration)
{
    try
    {
        double minutesWorked = workDuration.TotalMinutes;
        if (Math.Abs(minutesWorked) < 0.01) return; // 변경 값 없음

        // (중요) 기존 설정을 '미리' 로드합니다.
        _settings = _settingsService.LoadSettings();

        double totalPendingMinutes = _settings.PendingWorkMinutes + minutesWorked;
        int currentLevel = _settings.Level;
        int minutesPerXpChunk = currentLevel;
        int xpPerChunk = 10;
        int xpToLevelUp = 100;
        int coinsPerLevel = 50;

        if (totalPendingMinutes >= minutesPerXpChunk)
        {
            // ... (기존 레벨업 로직과 동일) ...
            int chunksEarned = (int)Math.Floor(totalPendingMinutes / minutesPerXpChunk);
            int xpGained = chunksEarned * xpPerChunk;
            double remainingMinutes = totalPendingMinutes % minutesPerXpChunk;

            _settings.Experience += xpGained;
            _settings.PendingWorkMinutes = remainingMinutes;

            bool leveledUp = false;
            while (_settings.Experience >= xpToLevelUp)
            {
                _settings.Level++;
                _settings.Experience -= xpToLevelUp;
                _settings.Coins += coinsPerLevel;
                leveledUp = true;
            }

            if (leveledUp)
            {
                _dialogService.ShowAlert(
                    $"🎉 축하합니다! 레벨 업! 🎉\n\n레벨 {_settings.Level}이(가) 되었습니다.\n보상으로 {coinsPerLevel}코인을 획득했습니다!",
                    "레벨 업!"
                );
            }
        }
        else if (totalPendingMinutes < 0)
        {
            // (경험치 차감 로직 - 레벨 다운은 구현되지 않음)
            _settings.PendingWorkMinutes = totalPendingMinutes;
            // 참고: 경험치(XP)가 음수가 되는 것을 방지하는 로직이 필요할 수 있습니다.
            // 예: _settings.Experience = Math.Max(0, _settings.Experience + xpGained);
        }
        else
        {
            _settings.PendingWorkMinutes = totalPendingMinutes;
        }

        _settingsService.SaveSettings(_settings);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Error] GrantExperience Failed: {ex.Message}");
    }
}
    }
}