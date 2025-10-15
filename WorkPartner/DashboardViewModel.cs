using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WorkPartner.Commands;
using WorkPartner.Services;

namespace WorkPartner
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        // --- Services ---
        private readonly ITaskService _taskService;
        private readonly ITimeLogService _timeLogService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        public ITimerService TimerService { get; }

        // --- Properties ---
        private string _selectedTask;
        public string SelectedTask
        {
            get => _selectedTask;
            set { _selectedTask = value; OnPropertyChanged(); }
        }

        private TimeSpan _totalTime;
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set { _totalTime = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TaskItem> Tasks { get; private set; }
        public ObservableCollection<TimeLogEntry> TimeLogs { get; private set; }
        public ObservableCollection<MemoItem> Memos { get; private set; }
        public ObservableCollection<TodoItem> Todos { get; private set; }

        // --- Commands ---
        public ICommand StartTimerCommand { get; }
        public ICommand StopTimerCommand { get; }
        public ICommand AddTaskCommand { get; }
        public ICommand AddMemoCommand { get; }
        public ICommand AddTodoCommand { get; }



        private MiniTimerWindow _miniTimer;

        public DashboardViewModel(ITaskService taskService, ITimeLogService timeLogService, ISettingsService settingsService, IDialogService dialogService, ITimerService timerService)
        {
            _taskService = taskService;
            _timeLogService = timeLogService;
            _settingsService = settingsService;
            _dialogService = dialogService;
            TimerService = timerService;

            Tasks = new ObservableCollection<TaskItem>();
            TimeLogs = new ObservableCollection<TimeLogEntry>();
            Memos = new ObservableCollection<MemoItem>();
            Todos = new ObservableCollection<TodoItem>();

            StartTimerCommand = new RelayCommand(StartTimer, () => !string.IsNullOrEmpty(SelectedTask));
            StopTimerCommand = new RelayCommand(StopTimer, () => TimerService.IsRunning);

            TimerService = timerService;
            StartTimerCommand = new RelayCommand(StartTimer, () => !string.IsNullOrEmpty(SelectedTask));
            StopTimerCommand = new RelayCommand(StopTimer, () => TimerService.IsRunning);

            // [핵심] 타이머의 시간 갱신 신호를 받도록 연결합니다.
            TimerService.TimeUpdated += OnTimeUpdated;
        }
        private void OnTimeUpdated(TimeSpan newTime)
        {
            TotalTime = newTime;
            LoadTimeLogsAsync();
        }

        private void StartTimer()
        {
            TimerService.Start(SelectedTask);
            _miniTimer?.SetCurrentTask(SelectedTask);
        }

        private void StopTimer()
        {
            TimerService.Stop();
        }

        public async Task LoadTasksAsync()
        {
            var tasks = await _taskService.GetAllTasksAsync(); // GetAllTasksAsync로 수정
            Tasks.Clear();
            foreach (var task in tasks)
            {
                Tasks.Add(task);
            }
        }

        public async Task LoadTimeLogsAsync()
        {
            var logs = await _timeLogService.GetLogsForDateAsync(DateTime.Now); // GetLogsForDateAsync로 수정
            TimeLogs.Clear();
            foreach (var log in logs)
            {
                TimeLogs.Add(log);
            }
        }

        public void LoadMemos()
        {
            // Placeholder
        }

        public void LoadTodos()
        {
            // Placeholder
        }

        public void SetMiniTimer(MiniTimerWindow miniTimer)
        {
            _miniTimer = miniTimer;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}