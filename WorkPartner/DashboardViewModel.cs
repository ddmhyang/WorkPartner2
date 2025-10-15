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
        private string _lastActiveProcessName = string.Empty;

        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs;
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; private set; }
        public event Action<string> TimeUpdated;

        #endregion

        #region --- UI와 바인딩될 속성 ---

        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        private TaskItem _selectedTask;
        public TaskItem SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    SelectTask(_selectedTask);
                }
            }
        }

        #endregion

        #region --- 명령어(Commands) 선언 ---

        public ICommand StopTimerCommand { get; }

        #endregion

        public DashboardViewModel(ITimerService timerService, ITimeLogService timeLogService, ITaskService taskService, ISettingsService settingsService)
        {
            _timerService = timerService;
            _timeLogService = timeLogService;
            _taskService = taskService;
            _settingsService = settingsService;

            _stopwatch = new Stopwatch();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();

            StopTimerCommand = new RelayCommand(PauseTimer);

            _timerService.Tick += _ => UpdateLiveTimeDisplays();
        }

        public async Task InitializeAsync()
        {
            _settings = await _settingsService.LoadSettingsAsync();
            var logs = await _timeLogService.LoadTimeLogsAsync();
            // ✨ 오타 수정: ObservableObservableCollection -> ObservableCollection
            TimeLogEntries = new ObservableCollection<TimeLogEntry>(logs);
            RecalculateTotalTimeToday();
            UpdateLiveTimeDisplays();
        }

        public void SelectTask(TaskItem task)
        {
            if (task == _currentWorkingTask)
            {
                PauseTimer(null);
                _currentWorkingTask = null;
                SelectedTask = null;
                return;
            }

            if (_stopwatch.IsRunning)
            {
                SaveCurrentSession();
            }

            _currentWorkingTask = task;

            if (_currentWorkingTask != null)
            {
                _sessionStartTime = DateTime.Now;
                _stopwatch.Restart();
                _timerService.Start();
            }
            else
            {
                PauseTimer(null);
            }
        }

        public void PauseTimer(object param)
        {
            if (_stopwatch.IsRunning)
            {
                _timerService.Stop();
                _stopwatch.Stop();
                SaveCurrentSession();
                _currentWorkingTask = null;
                UpdateLiveTimeDisplays();
            }
        }

        public void ResumeTimer()
        {
            if (!_stopwatch.IsRunning && _currentWorkingTask != null)
            {
                _sessionStartTime = DateTime.Now - _stopwatch.Elapsed;
                _timerService.Start();
                _stopwatch.Start();
            }
        }

        private void RecalculateTotalTimeToday()
        {
            _totalTimeTodayFromLogs = new TimeSpan(TimeLogEntries
                .Where(log => log.StartTime.Date == DateTime.Today)
                .Sum(log => log.Duration.Ticks));
        }

        private void SaveCurrentSession(DateTime? endTime = null)
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

        private void UpdateLiveTimeDisplays(object sender, EventArgs e)
        {
            var timeToDisplay = _totalTimeTodayFromLogs;
            if (_stopwatch.IsRunning)
            {
                timeToDisplay += _stopwatch.Elapsed;
            }
            string newTime = timeToDisplay.ToString(@"hh\:mm\:ss");
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