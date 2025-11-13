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
using System.IO;          // 👈 File.Exists 사용
using System.Text.Json;   // 👈 JsonSerializer 사용


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
        public AppSettings Settings => _settings; // '얼굴'이 _settings를 읽을 수 있게 공개
        public bool IsTimerRunning => _stopwatch.IsRunning;
        private bool _isInGracePeriod = false;
        private DateTime _gracePeriodStartTime;
        private const int GracePeriodSeconds = 120; // 👈 2분(120초)간의 유예 시간 (이 시간은 조절 가능)

        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }
        public ObservableCollection<TodoItem> TodoItems { get; private set; }
        public ObservableCollection<MemoItem> AllMemos { get; private set; }
        private readonly string _todosFilePath = DataManager.TodosFilePath;
        private readonly string _memosFilePath = DataManager.MemosFilePath;
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

            TodoItems = new ObservableCollection<TodoItem>();
            AllMemos = new ObservableCollection<MemoItem>();

            _timerService.Tick += OnTimerTick;

            DataManager.SettingsUpdated += OnSettingsUpdated;

            LoadInitialDataAsync();
            _ = InitializeTasksAsync(); // 👈 과목 데이터를 비동기로 로드합니다.
            _ = InitializeTodosAsync();
            _ = InitializeMemosAsync();
        }

        // 파일: DashboardViewModel.cs
        // (클래스 내부에 이 메서드 전체를 추가하세요)

        /// <summary>
        /// ViewModel이 생성될 때 과목 목록을 비동기적으로 불러옵니다.
        /// </summary>
        private async Task InitializeTasksAsync()
        {
            // 1. 서비스(ITaskService)를 통해 과목 데이터를 로드합니다.
            var loadedTasks = await _taskService.LoadTasksAsync();
            if (loadedTasks == null) return;

            // 2. (중요) ViewModel의 TaskItems 컬렉션을 채웁니다.
            TaskItems.Clear();
            foreach (var task in loadedTasks)
            {
                // 3. 설정에 저장된 과목별 색상을 적용합니다.
                if (_settings.TaskColors.TryGetValue(task.Text, out var colorHex))
                {
                    try
                    {
                        // (이 부분은 UI와 관련되지만, TaskItem 모델 자체가 Brush를 갖고 있으므로 허용)
                        task.ColorBrush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(colorHex);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Invalid color hex for task {task.Text}: {colorHex} - {ex.Message}");
                        task.ColorBrush = System.Windows.Media.Brushes.Gray; // 👈 오류 시 기본색
                    }
                }
                TaskItems.Add(task);
            }
        }
        private async Task InitializeTodosAsync()
        {
            if (!File.Exists(_todosFilePath)) return;
            try
            {
                await using var stream = new FileStream(_todosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var loadedTodos = await JsonSerializer.DeserializeAsync<ObservableCollection<TodoItem>>(stream);
                if (loadedTodos == null) return;

                // (UI 스레드에서 컬렉션을 채우도록 보장 - WPF 안정성)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TodoItems.Clear();
                    foreach (var todo in loadedTodos) TodoItems.Add(todo);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading todos: {ex.Message}"); }
        }

        public void SaveSettings()
        {
            // '두뇌'가 가진 서비스(_settingsService)를 이용해 저장합니다.
            _settingsService.SaveSettings(_settings);
        }

        public void SaveTodos()
        {
            // ViewModel이 소유한 TodoItems 컬렉션을 DataManager를 통해 저장합니다.
            DataManager.SaveTodos(TodoItems);
        }

        /// <summary>
        /// ViewModel이 생성될 때 메모 목록을 비동기적으로 불러옵니다.
        /// </summary>
        private async Task InitializeMemosAsync()
        {
            if (!File.Exists(_memosFilePath)) return;
            try
            {
                await using var stream = new FileStream(_memosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var loadedMemos = await JsonSerializer.DeserializeAsync<ObservableCollection<MemoItem>>(stream);
                if (loadedMemos == null) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AllMemos.Clear();
                    foreach (var memo in loadedMemos) AllMemos.Add(memo);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading memos: {ex.Message}"); }
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


        // 파일: DashboardViewModel.cs

        // ▼▼▼ 이 메서드 전체를 교체하세요 ▼▼▼
        private void HandleStopwatchMode()
        {
            if (_settings == null) return;

            // --- 1. 현재 상태 파악 ---
            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;
            string activeTitle = ActiveWindowHelper.GetActiveWindowTitle()?.ToLower() ?? string.Empty;

            CheckTagRules(activeTitle, activeUrl); // (AI 태그 자동 변경)

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

            // --- 2. [활성화] 자리 비움 감지 로직 ---
            bool isCurrentlyIdle = false;
            if (!isPassiveApp) // '수동 앱'(영상 시청 등)이 아닐 때만
            {
                if (ActiveWindowHelper.GetIdleTime().TotalSeconds >= IdleGraceSeconds) // 10초 이상 유휴 상태면
                {
                    isCurrentlyIdle = true;
                    if (!_isPausedForIdle)
                    {
                        // "방금" 유휴 상태가 됨
                        _isPausedForIdle = true;
                        _idleStartTime = DateTime.Now; // 유휴 시작 시간 기록 (현재는 사용X, 추후 분석용)
                        Debug.WriteLine("Idle detected: Pausing timer.");
                    }
                }
                else
                {
                    isCurrentlyIdle = false;
                    if (_isPausedForIdle)
                    {
                        // "방금" 유휴 상태에서 복귀함
                        _isPausedForIdle = false;
                        Debug.WriteLine("User returned: Resuming timer.");
                    }
                }
            }
            else
            {
                _isPausedForIdle = false; // 수동 앱 시청 중에는 유휴 상태가 아님
            }
            // --- [활성화 완료] ---

            // 3. 최종 '업무 상태' 판정
            bool isWorkState = (isWorkApp || isPassiveApp) && !isCurrentlyIdle; // 👈 isCurrentlyIdle 변수 사용

            // 4. '업무 상태'일 때
            if (isWorkState)
            {
                if (_isInGracePeriod) // (딴짓하다가 120초 안에 복귀)
                {
                    _isInGracePeriod = false;
                    _stopwatch.Start();
                }
                else if (!_stopwatch.IsRunning) // (새 업무 시작 또는 유휴 상태에서 복귀)
                {
                    _currentWorkingTask = SelectedTaskItem ?? TaskItems.FirstOrDefault();
                    if (_currentWorkingTask != null)
                    {
                        // [로그 병합 로직] - 마지막 로그가 120초 이내 같은 과목이면 이어붙이기
                        var lastLog = TimeLogEntries.LastOrDefault();
                        if (lastLog != null &&
                            lastLog.TaskText == _currentWorkingTask.Text &&
                            (DateTime.Now - lastLog.EndTime).TotalSeconds < GracePeriodSeconds)
                        {
                            _sessionStartTime = lastLog.StartTime; // 마지막 로그의 시작 시간 계승
                            TimeLogEntries.Remove(lastLog); // 이전 로그 삭제 (나중에 합쳐서 새로 저장)
                            Debug.WriteLine($"Log stitched: Resuming '{_currentWorkingTask.Text}'");
                        }
                        else
                        {
                            _sessionStartTime = DateTime.Now; // 새 세션 시작
                        }

                        _stopwatch.Start();
                        IsRunningChanged?.Invoke(true);
                        CurrentTaskDisplayText = _currentWorkingTask.Text;
                        CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
                    }
                }
            }
            // 5. '업무 상태'가 아닐 때 (딴짓 또는 자리 비움)
            else
            {
                bool isDistraction = _settings.DistractionProcesses.Any(p =>
                    activeProcess.Contains(p) ||
                    (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                    (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
                );

                if (_stopwatch.IsRunning) // (방금 딴짓/자리비움 시작)
                {
                    _stopwatch.Stop();
                    _isInGracePeriod = true; // 120초 로그 병합 유예 시간 시작
                    _gracePeriodStartTime = DateTime.Now;
                }
                else if (_isInGracePeriod) // (유예 시간 진행 중)
                {
                    // 120초가 지났는데도 복귀 안 함
                    if ((DateTime.Now - _gracePeriodStartTime).TotalSeconds > GracePeriodSeconds)
                    {
                        LogWorkSession(); // 로그 저장
                        IsRunningChanged?.Invoke(false);
                        _isInGracePeriod = false; // 유예 시간 종료
                    }

                    // [방해금지 경고] - 유예 시간 *중에도* 경고는 울림 (자리 비움 아닐 때만)
                    if (isDistraction && !_isPausedForIdle && _settings.IsFocusModeEnabled)
                    {
                        var elapsedSinceLastNag = (DateTime.Now - _lastFocusNagTime).TotalSeconds;
                        if (elapsedSinceLastNag > _settings.FocusModeNagIntervalSeconds)
                        {
                            _lastFocusNagTime = DateTime.Now;
                            _dialogService.ShowAlert(_settings.FocusModeNagMessage, "집중 모드 경고");
                        }
                    }
                }
                else // (유예 시간도 끝난 상태 - 완전히 멈춤)
                {
                    // [방해금지 경고] - (자리 비움 아닐 때만)
                    if (isDistraction && !_isPausedForIdle && _settings.IsFocusModeEnabled)
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
            _timeLogService.SaveTimeLogs(TimeLogEntries); // 👈 'Async' 삭제
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
            if (_stopwatch.IsRunning || _isInGracePeriod)
            {
                totalTimeToday += _stopwatch.Elapsed;
            }
            // [유지] 하단 텍스트는 '오늘' 기준이 맞으므로 둡니다.
            TotalTimeTodayDisplayText = $"오늘의 작업 시간 | {totalTimeToday:hh\\:mm\\:ss}";

            var timeForSelectedTask = TimeSpan.Zero;
            if (SelectedTaskItem != null && _dailyTaskTotals.TryGetValue(SelectedTaskItem.Text, out var storedTime))
            {
                timeForSelectedTask = storedTime;
            }
            if ((_stopwatch.IsRunning || _isInGracePeriod) && _currentWorkingTask == SelectedTaskItem)
            {
                timeForSelectedTask += _stopwatch.Elapsed;
            }

            string newTime = timeForSelectedTask.ToString(@"hh\:mm\:ss");

            // ▼▼▼ [수정] '두뇌'가 메인 시간을 덮어쓰지 않도록 주석 처리합니다. ▼▼▼
            // MainTimeDisplayText = newTime; // 👈 [수정 1]
            // ▲▲▲

            // [유지] 미니 타이머는 '오늘' 실시간 시간이 필요하므로 둡니다.
            TimeUpdated?.Invoke(newTime);


            // ▼▼▼ [수정] '두뇌'가 과목 목록 시간을 덮어쓰지 않도록 주석 처리합니다. ▼▼▼
            /* // 👈 [수정 2]
            foreach (var task in TaskItems)
            {
                TimeSpan taskTotalTime = TimeSpan.Zero;
                if (_dailyTaskTotals.TryGetValue(task.Text, out var storedTaskTime))
                {
                    taskTotalTime = storedTaskTime;
                }
                if ((_stopwatch.IsRunning || _isInGracePeriod) && _currentWorkingTask == task)
                {
                    taskTotalTime += _stopwatch.Elapsed;
                }
                task.TotalTime = taskTotalTime;
            }
            */ // 👈 [수정 3]
               // ▲▲▲
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

        /// <summary>
        /// '두뇌'가 소유한 TaskItems 목록을 파일에 저장합니다.
        /// </summary>
        public void SaveTasks()
        {
            // '두뇌'가 가진 _taskService를 이용해 저장합니다.
            _taskService.SaveTasks(TaskItems);
        }

        /// <summary>
        /// '두뇌'가 과목 삭제의 모든 로직을 처리합니다.
        /// </summary>
        public void DeleteTask(TaskItem taskToDelete)
        {
            if (taskToDelete == null) return;

            string taskNameToDelete = taskToDelete.Text;

            // 1. '두뇌'의 과목 리스트에서 제거
            TaskItems.Remove(taskToDelete);

            // 2. '두뇌'의 설정(Settings)에서 해당 과목 색상 정보 제거
            if (_settings.TaskColors.ContainsKey(taskNameToDelete))
            {
                _settings.TaskColors.Remove(taskNameToDelete);
                // '두뇌'의 설정 서비스를 통해 즉시 저장
                _settingsService.SaveSettings(_settings);
            }

            // 3. '두뇌'의 시간 기록(TimeLogEntries)에서 관련 기록 모두 제거
            var logsToRemove = TimeLogEntries.Where(l => l.TaskText == taskNameToDelete).ToList();
            foreach (var log in logsToRemove)
            {
                TimeLogEntries.Remove(log);
            }
            // '두뇌'의 시간 기록 서비스를 통해 즉시 저장
            _timeLogService.SaveTimeLogs(TimeLogEntries);

            // 4. '두뇌'의 과목 리스트를 파일에 저장
            SaveTasks();
        }


        // 파일: DashboardViewModel.cs

        /// <summary>
        /// '두뇌'가 과목 이름 변경의 모든 복잡한 로직을 처리합니다.
        /// </summary>
        /// <param name="taskToUpdate">이름을 변경할 과목 객체</param>
        /// <param name="newName">새로운 과목 이름</param>
        /// <returns>성공하면 true, (중복 이름 등으로) 실패하면 false</returns>
        public bool UpdateTask(TaskItem taskToUpdate, string newName)
        {
            if (taskToUpdate == null || string.IsNullOrWhiteSpace(newName) || taskToUpdate.Text == newName)
            {
                return false; // 변경할 필요 없음
            }

            // 1. 이름 중복 검사
            if (TaskItems.Any(t => t.Text.Equals(newName, StringComparison.OrdinalIgnoreCase) && t != taskToUpdate))
            {
                _dialogService.ShowAlert("이미 존재하는 과목 이름입니다.", "오류");
                return false;
            }

            string oldName = taskToUpdate.Text;

            // 2. '두뇌'의 시간 기록(TimeLogEntries) 업데이트
            foreach (var log in TimeLogEntries.Where(l => l.TaskText == oldName))
            {
                log.TaskText = newName;
            }
            // 시간 기록 서비스로 저장
            _timeLogService.SaveTimeLogs(TimeLogEntries);

            // 3. '두뇌'의 설정(Settings)에서 색상 정보 이전
            if (_settings.TaskColors.ContainsKey(oldName))
            {
                var color = _settings.TaskColors[oldName];
                _settings.TaskColors.Remove(oldName);
                _settings.TaskColors[newName] = color;
                // 설정 서비스로 저장
                _settingsService.SaveSettings(_settings);
            }

            // 4. 과목 객체 자체의 이름 변경 (INotifyPropertyChanged가 UI 갱신)
            taskToUpdate.Text = newName;

            // 5. '두뇌'의 과목 리스트를 파일에 저장 (지난 단계에서 만든 메서드)
            SaveTasks();

            return true;
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
                DataManager.SaveTimeLogsImmediately(TimeLogEntries);
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

        // 파일: DashboardViewModel.cs

        /// <summary>
        /// '두뇌'가 '할 일' 삭제 로직을 처리합니다.
        /// </summary>
        public void DeleteTodo(TodoItem todoToDelete)
        {
            if (todoToDelete == null) return;

            // 1. '두뇌'의 TodoItems 리스트(및 하위 리스트)에서 제거
            RemoveTodoItem(this.TodoItems, todoToDelete);

            // 2. '두뇌'의 저장 메서드를 호출
            SaveTodos();
        }

        /// <summary>
        /// '두뇌'가 '할 일' 수정 로직을 처리합니다.
        /// </summary>
        public void UpdateTodo(TodoItem todoToUpdate, string newText)
        {
            if (todoToUpdate == null || string.IsNullOrWhiteSpace(newText)) return;

            // 1. 항목의 텍스트를 변경 (INotifyPropertyChanged가 UI 갱신)
            todoToUpdate.Text = newText;

            // 2. '두뇌'의 저장 메서드를 호출
            SaveTodos();
        }

        /// <summary>
        /// 재귀적으로 컬렉션을 탐색하여 '할 일' 항목을 제거합니다.
        /// (DashboardPage.xaml.cs에서 그대로 가져온 헬퍼 메서드)
        /// </summary>
        private bool RemoveTodoItem(ObservableCollection<TodoItem> collection, TodoItem itemToRemove)
        {
            if (collection.Remove(itemToRemove)) return true;
            foreach (var item in collection)
            {
                if (item.SubTasks != null && RemoveTodoItem(item.SubTasks, itemToRemove)) return true;
            }
            return false;
        }

        // 파일: DashboardViewModel.cs

        /// <summary>
        /// '두뇌'가 '할 일' 추가 로직을 처리합니다.
        /// </summary>
        /// <param name="newTodoText">새 할 일의 텍스트</param>
        /// <param name="parentTodo">부모 할 일 (없으면 null)</param>
        /// <param name="date">할 일이 속한 날짜</param>
        public void AddTodo(string newTodoText, TodoItem parentTodo, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(newTodoText)) return;

            // 1. '두뇌'가 직접 새 TodoItem 객체를 생성합니다.
            var newTodo = new TodoItem
            {
                Text = newTodoText,
                Date = date.Date // '얼굴'로부터 전달받은 날짜를 사용
            };

            // 2. 부모가 있는지 확인하고 올바른 리스트에 추가합니다.
            if (parentTodo != null)
            {
                // 하위 작업으로 추가
                parentTodo.SubTasks.Add(newTodo);
            }
            else
            {
                // 최상위 작업으로 추가
                TodoItems.Add(newTodo);
            }

            // 3. '두뇌'의 저장 메서드를 호출합니다. (이전 단계에서 만듦)
            SaveTodos();
        }

        public void SaveMemos()
        {
            // '두뇌'가 직접 DataManager를 호출해 저장합니다.
            // (IMemoService가 없으므로 임시로 이 방식을 사용)
            DataManager.SaveMemos(AllMemos);
        }
    }
}