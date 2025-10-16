using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WorkPartner.Commands;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Media;

namespace WorkPartner.ViewModels
{
    // ✨ 모든 ViewModel의 기반이 되는 BaseViewModel 상속
    public class DashboardViewModel : BaseViewModel
    {
        #region Fields (필드)
        // ViewModel 내부에서만 사용할 변수들
        private AppSettings _settings;
        private DateTime _currentDateForTimeline = DateTime.Today;
        #endregion

        #region Properties (속성)
        // UI와 데이터 바인딩으로 연결될 속성들
        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; }
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; }
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<MemoItem> AllMemos { get; set; }

        private string _newTaskText;
        public string NewTaskText
        {
            get => _newTaskText;
            set
            {
                _newTaskText = value;
                OnPropertyChanged(); // 값이 바뀌면 UI에 알려줌
            }
        }

        private string _newTodoText;
        public string NewTodoText
        {
            get => _newTodoText;
            set
            {
                _newTodoText = value;
                OnPropertyChanged();
            }
        }

        // UI에 표시될 날짜 문자열
        public string CurrentDateDisplay => _currentDateForTimeline.ToString("yyyy-MM-dd");
        public string Username => _settings?.Username;
        public string CoinsDisplay => _settings?.Coins.ToString("N0");
        #endregion

        #region Commands (커맨드)
        // UI의 버튼 등과 연결될 커맨드들
        public ICommand AddTaskCommand { get; }
        public ICommand EditTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand AddTodoCommand { get; }
        public ICommand PrevDayCommand { get; }
        public ICommand TodayCommand { get; }
        public ICommand NextDayCommand { get; }
        #endregion

        // ViewModel 생성자
        public DashboardViewModel()
        {
            // 데이터 컬렉션 초기화
            TaskItems = new ObservableCollection<TaskItem>();
            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            AllMemos = new ObservableCollection<MemoItem>();

            // ✨ Command 초기화 부분을 명확하게 수정
            AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
            EditTaskCommand = new RelayCommand(EditTask);
            DeleteTaskCommand = new RelayCommand(DeleteTask);
            AddTodoCommand = new RelayCommand(AddTodo, CanAddTodo);
            PrevDayCommand = new RelayCommand(p => ChangeDay(-1));
            TodayCommand = new RelayCommand(p => GoToToday());
            NextDayCommand = new RelayCommand(p => ChangeDay(1));

            // 비동기로 데이터 로딩 시작
            _ = LoadAllDataAsync();

            DataManager.SettingsUpdated += OnSettingsUpdated;
        }

        #region Data Logic (데이터 로직)
        public async Task LoadAllDataAsync()
        {
            LoadSettings();
            await LoadTasksAsync();
            await LoadTodosAsync();
            await LoadTimeLogsAsync();
            await LoadMemosAsync();

            UpdateUIAfterDataLoad();
        }

        public void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
            // 설정이 로드되면 관련된 UI 속성들을 모두 갱신하도록 알림
            OnPropertyChanged(nameof(Username));
            OnPropertyChanged(nameof(CoinsDisplay));
        }

        private async Task LoadTasksAsync()
        {
            if (!File.Exists(DataManager.TasksFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.TasksFilePath);
                var loadedTasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, DataManager.JsonOptions);
                if (loadedTasks == null) return;

                // UI 스레드에서 컬렉션을 변경하도록 Dispatcher 사용
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TaskItems.Clear();
                    foreach (var task in loadedTasks)
                    {
                        if (_settings.TaskColors.TryGetValue(task.Text, out var colorHex))
                        {
                            try { task.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex); }
                            catch { /* 색상 변환 실패 시 무시 */ }
                        }
                        TaskItems.Add(task);
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading tasks: {ex.Message}"); }
        }

        // LoadTodosAsync, LoadTimeLogsAsync, LoadMemosAsync (기존과 유사하게 구현)
        private async Task LoadTodosAsync()
        {
            if (!File.Exists(DataManager.TodosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.TodosFilePath);
                var loadedTodos = await JsonSerializer.DeserializeAsync<ObservableCollection<TodoItem>>(stream, DataManager.JsonOptions);
                if (loadedTodos == null) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TodoItems.Clear();
                    foreach (var todo in loadedTodos) TodoItems.Add(todo);
                    FilterTodos();
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading todos: {ex.Message}"); }
        }

        private async Task LoadTimeLogsAsync()
        {
            if (!File.Exists(DataManager.TimeLogFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.TimeLogFilePath);
                var loadedLogs = await JsonSerializer.DeserializeAsync<ObservableCollection<TimeLogEntry>>(stream, DataManager.JsonOptions);
                if (loadedLogs == null) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TimeLogEntries.Clear();
                    foreach (var log in loadedLogs) TimeLogEntries.Add(log);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading time logs: {ex.Message}"); }
        }

        private async Task LoadMemosAsync()
        {
            if (!File.Exists(DataManager.MemosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.MemosFilePath);
                var loadedMemos = await JsonSerializer.DeserializeAsync<ObservableCollection<MemoItem>>(stream, DataManager.JsonOptions);
                if (loadedMemos == null) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AllMemos.Clear();
                    foreach (var memo in loadedMemos) AllMemos.Add(memo);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading memos: {ex.Message}"); }
        }

        private void SaveTasks() => DataManager.SaveTasks(TaskItems);
        private void SaveTodos() => DataManager.SaveTodos(TodoItems);
        private void SaveTimeLogs() => DataManager.SaveTimeLogs(TimeLogEntries);
        private void SaveSettings() => DataManager.SaveSettingsAndNotify(_settings);
        #endregion

        #region Command Methods (커맨드 실행 메서드)
        private bool CanAddTask(object parameter) => !string.IsNullOrWhiteSpace(NewTaskText);
        private void AddTask(object parameter)
        {
            if (TaskItems.Any(t => t.Text.Equals(NewTaskText, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTask = new TaskItem { Text = NewTaskText };
            var colorPicker = new ColorPalette { Owner = Application.Current.MainWindow };
            if (colorPicker.ShowDialog() == true)
            {
                string colorHex = colorPicker.SelectedColor.ToString();
                _settings.TaskColors[newTask.Text] = colorHex;
                try { newTask.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex); } catch { }
                SaveSettings();
            }

            TaskItems.Add(newTask);
            NewTaskText = string.Empty;
            SaveTasks();
        }

        private void EditTask(object parameter)
        {
            if (parameter is not TaskItem selectedTask) return;

            var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text) { Owner = Application.Current.MainWindow };
            if (inputWindow.ShowDialog() != true) return;

            string newName = inputWindow.ResponseText.Trim();
            string oldName = selectedTask.Text;
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
            if (TaskItems.Any(t => t.Text.Equals(newName, StringComparison.OrdinalIgnoreCase) && t != selectedTask))
            {
                MessageBox.Show("이미 존재하는 과목 이름입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var log in TimeLogEntries.Where(l => l.TaskText == oldName)) log.TaskText = newName;
            if (_settings.TaskColors.ContainsKey(oldName))
            {
                var color = _settings.TaskColors[oldName];
                _settings.TaskColors.Remove(oldName);
                _settings.TaskColors[newName] = color;
            }

            selectedTask.Text = newName;
            SaveTasks();
            SaveTimeLogs();
            SaveSettings();
            // UI 갱신은 데이터 바인딩이 자동으로 처리!
        }

        private void DeleteTask(object parameter)
        {
            if (parameter is not TaskItem selectedTask) return;

            if (MessageBox.Show($"'{selectedTask.Text}' 과목을 삭제하시겠습니까?\n관련된 모든 학습 기록도 삭제됩니다.", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            string taskNameToDelete = selectedTask.Text;
            if (_settings.TaskColors.ContainsKey(taskNameToDelete))
            {
                _settings.TaskColors.Remove(taskNameToDelete);
                SaveSettings();
            }

            var logsToRemove = TimeLogEntries.Where(l => l.TaskText == taskNameToDelete).ToList();
            foreach (var log in logsToRemove) TimeLogEntries.Remove(log);

            TaskItems.Remove(selectedTask); // 마지막에 제거해야 참조 문제 없음

            SaveTasks();
            SaveTimeLogs();
        }

        private bool CanAddTodo(object parameter) => !string.IsNullOrWhiteSpace(NewTodoText);
        private void AddTodo(object parameter)
        {
            var newTodo = new TodoItem { Text = NewTodoText, Date = _currentDateForTimeline.Date };
            TodoItems.Add(newTodo);
            NewTodoText = string.Empty;
            SaveTodos();
            FilterTodos();
        }

        private void ChangeDay(int days)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(days);
            UpdateUIAfterDataLoad();
        }

        private void GoToToday()
        {
            _currentDateForTimeline = DateTime.Today;
            UpdateUIAfterDataLoad();
        }
        #endregion

        #region UI Update Logic (UI 갱신 로직)
        private void UpdateUIAfterDataLoad()
        {
            OnPropertyChanged(nameof(CurrentDateDisplay)); // 날짜 표시 갱신
            RecalculateAllTotals();
            FilterTodos();
            // ✨ RenderTimeTable() 호출은 View에서 담당하므로 여기서는 호출하지 않음
        }

        public void RecalculateAllTotals()
        {
            foreach (var task in TaskItems)
            {
                task.TotalTime = new TimeSpan(TimeLogEntries
                    .Where(log => log.TaskText == task.Text && log.StartTime.Date == _currentDateForTimeline.Date)
                    .Sum(log => log.Duration.Ticks));
            }
        }

        private void FilterTodos()
        {
            var filtered = TodoItems.Where(t => t.Date.Date == _currentDateForTimeline.Date).ToList();
            FilteredTodoItems.Clear();
            foreach (var item in filtered) FilteredTodoItems.Add(item);
        }

        private async void OnSettingsUpdated()
        {
            LoadSettings();
            await LoadTasksAsync(); // 설정이 바뀌면 과목 색상 등을 다시 로드
        }
        #endregion
    }
}