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

        // 파일: WorkPartner/DashboardViewModel.cs
        // (메서드 전체를 교체하세요)

        /// <summary>
        /// 1초마다 호출되며, 현재 활성 창을 기준으로 타이머(스톱워치)를
        /// 시작, 일시정지, 또는 유예 시간 후 저장할지 결정하는 핵심 메서드입니다.
        /// </summary>
        private void HandleStopwatchMode()
        {
            // 1. 설정 확인
            if (_settings == null) return;

            // 2. 현재 활성 창 정보 가져오기
            string activeProcess = ActiveWindowHelper.GetActiveProcessName().ToLower();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl()?.ToLower() ?? string.Empty;
            string activeTitle = ActiveWindowHelper.GetActiveWindowTitle()?.ToLower() ?? string.Empty;

            // 3. AI 태그 규칙 검사
            CheckTagRules(activeTitle, activeUrl);

            // 4. "작업 상태"인지 판단
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

            // 5. "자리 비움" 상태인지 판단 (수동 앱(isPassiveApp)은 감지 안 함)
            bool isCurrentlyIdle = false;

            if (!isPassiveApp)
            {
                isCurrentlyIdle = ActiveWindowHelper.GetIdleTime().TotalSeconds >= 10;
            }

            // 6. 최종 "작업 상태" 정의: (작업 앱이거나 수동 앱) 그리고 (자리 비움이 아님)
            bool isWorkState = (isWorkApp || isPassiveApp) && !isCurrentlyIdle;

            // 7. 로직 분기
            if (isWorkState)
            {
                // --- 시나리오 A: 사용자가 현재 "작업 중" ---
                // A-1. (복귀) 유예 시간(2분) 중에 작업 앱으로 복귀한 경우
                if (_isInGracePeriod)
                {
                    _isInGracePeriod = false; // 유예 시간을 취소합니다.
                    _stopwatch.Start();       // 멈췄던 스톱워치를 다시 *이어갑니다.*
                }
                // A-2. (새 작업) 유예 시간이 아니었고, 스톱워치가 멈춰있던 경우
                else if (!_stopwatch.IsRunning)
                {
                    _currentWorkingTask = SelectedTaskItem ?? TaskItems.FirstOrDefault();
                    if (_currentWorkingTask != null)
                    {
                        _sessionStartTime = DateTime.Now; // 새 세션 시작 시간 기록
                        _stopwatch.Start();
                        IsRunningChanged?.Invoke(true);
                        CurrentTaskDisplayText = _currentWorkingTask.Text;
                        CurrentTaskChanged?.Invoke(CurrentTaskDisplayText);
                    }
                }
            }
            else
            {
                // --- 시나리오 B: 사용자가 현재 "작업 중이 아님" ---
                // B-1. (이탈 시작) 방금 전까지 스톱워치가 실행 중이었던 경우
                if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();          // 스톱워치를 *일시 정지*합니다. (로그 저장 안 함)
                    _isInGracePeriod = true;    // "유예 시간"을 시작합니다.
                    _gracePeriodStartTime = DateTime.Now;
                }
                // B-2. (이탈 지속) 이미 유예 시간이 진행 중이던 경우
                else if (_isInGracePeriod)
                {
                    // B-3. (유예 시간 만료) 유예 시간이 초과되었는지 확인
                    if ((DateTime.Now - _gracePeriodStartTime).TotalSeconds > GracePeriodSeconds)
                    {
                        // 유예 시간(2분)이 지났습니다. "진짜 휴식"으로 간주합니다.
                        LogWorkSession(); // ✨ 이때 비로소 일시 정지했던 세션을 "저장"합니다.
                        _stopwatch.Reset();
                        IsRunningChanged?.Invoke(false);
                        _isInGracePeriod = false; // 유예 시간을 완전히 종료합니다.
                    }
                    // (else) 유예 시간이 아직 남았다면 -> 아무것도 안 하고 다음 1초 틱을 대기합니다.
                }
                // B-4. (완전 비작업) 원래부터 작업 중이 아니었고 유예 시간도 아닌 경우
                else
                {
                    // (기존의 "방해 앱 경고" 로직은 여기에 해당합니다)
                    bool isDistraction = _settings.DistractionProcesses.Any(p =>
                        activeProcess.Contains(p) ||
                        (!string.IsNullOrEmpty(activeUrl) && activeUrl.Contains(p)) ||
                        (!string.IsNullOrEmpty(activeTitle) && activeTitle.Contains(p))
                    );

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