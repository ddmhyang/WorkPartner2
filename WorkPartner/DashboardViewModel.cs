// 파일: DashboardViewModel.cs (최종 수정)

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WorkPartner.Commands;
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
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }
        public event Action<string> TimeUpdated;
        #endregion

        #region --- UI 바인딩 속성 ---
        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText { get => _mainTimeDisplayText; set => SetProperty(ref _mainTimeDisplayText, value); }
        private bool _isTimerRunning;
        public bool IsTimerRunning { get => _isTimerRunning; set => SetProperty(ref _isTimerRunning, value); }
        #endregion

        #region --- Command ---
        public ICommand StartStopTimerCommand { get; }
        // ★ 추가: XAML에서 사용하는 StopTimerCommand를 정의합니다.
        public ICommand StopTimerCommand { get; }
        #endregion

        #region --- 생성자 ---
        public DashboardViewModel(ITimerService timerService, ISettingsService settingsService, ITaskService taskService, ITimeLogService timeLogService)
        {
            _timerService = timerService;
            _settingsService = settingsService;
            _taskService = taskService;
            _timeLogService = timeLogService;
            _stopwatch = new Stopwatch();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();

            // ★ 수정: RelayCommand를 람다식으로 명확하게 생성합니다.
            StartStopTimerCommand = new RelayCommand(() => ToggleTimer());
            StopTimerCommand = new RelayCommand(() => StopTimer());

            // ★ 수정: ITimerService의 TimeUpdated 이벤트를 구독합니다.
            _timerService.TimeUpdated += UpdateLiveTimeDisplays;
        }
        #endregion

        #region --- 핵심 로직 ---
        public async Task LoadAllDataAsync()
        {
            // ★ 수정: 인터페이스와 일치하는 LoadSettings() 동기 메소드 호출
            _settings = _settingsService.LoadSettings();
            var logs = await _timeLogService.LoadTimeLogsAsync();
            TimeLogEntries.Clear();
            foreach (var log in logs) TimeLogEntries.Add(log);
            RecalculateTotalTimeToday();
        }

        private void ToggleTimer()
        {
            if (_stopwatch.IsRunning) StopTimer();
            else StartTimer();
        }

        private void StartTimer()
        {
            _sessionStartTime = DateTime.Now;
            _stopwatch.Start();
            _timerService.Start();
            IsTimerRunning = true;
        }

        private void StopTimer(DateTime? endTime = null)
        {
            if (!_stopwatch.IsRunning) return; // 이미 중지된 경우 무시
            _timerService.Stop();
            _stopwatch.Stop();
            IsTimerRunning = false;
            SaveTimeLog(endTime);
        }

        private void SaveTimeLog(DateTime? endTime = null)
        {
            if (_stopwatch.Elapsed.TotalSeconds < 1) { _stopwatch.Reset(); return; }
            var entry = new TimeLogEntry
            {
                StartTime = _sessionStartTime,
                EndTime = endTime ?? _sessionStartTime.Add(_stopwatch.Elapsed),
                TaskText = _currentWorkingTask?.Text ?? "지정되지 않은 작업"
            };
            TimeLogEntries.Insert(0, entry);
            _timeLogService.SaveTimeLogsAsync(TimeLogEntries);
            RecalculateTotalTimeToday();
        }

        private void RecalculateTotalTimeToday()
        {
            _totalTimeTodayFromLogs = TimeLogEntries
                .Where(log => log.StartTime.Date == DateTime.Today)
                .Aggregate(TimeSpan.Zero, (total, log) => total + log.Duration);
            UpdateLiveTimeDisplays(_stopwatch.Elapsed); // 초기 시간 표시
        }

        // ★ 수정: ITimerService의 Action<TimeSpan> 델리게이트와 일치하도록 시그니처 변경
        private void UpdateLiveTimeDisplays(TimeSpan elapsed)
        {
            var timeToDisplay = _totalTimeTodayFromLogs + elapsed;
            string newTime = timeToDisplay.ToString(@"hh\:mm\:ss");
            MainTimeDisplayText = newTime;
            TimeUpdated?.Invoke(newTime);
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
    }
}