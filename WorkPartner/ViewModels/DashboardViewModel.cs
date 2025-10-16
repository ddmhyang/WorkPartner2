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
using System.Windows.Media;
using System.Collections.Generic;

namespace WorkPartner.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private AppSettings _settings;
        private DateTime _currentDateForTimeline = DateTime.Today;

        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; }
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; }
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<MemoItem> AllMemos { get; set; }

        private string _newTaskText;
        public string NewTaskText { get => _newTaskText; set { _newTaskText = value; OnPropertyChanged(); } }

        private string _newTodoText;
        public string NewTodoText { get => _newTodoText; set { _newTodoText = value; OnPropertyChanged(); } }

        public string CurrentDateDisplay => _currentDateForTimeline.ToString("yyyy-MM-dd");
        public string Username => _settings?.Username;
        public string CoinsDisplay => _settings?.Coins.ToString("N0");

        public ICommand AddTaskCommand { get; }
        public ICommand EditTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand AddTodoCommand { get; }
        public ICommand PrevDayCommand { get; }
        public ICommand TodayCommand { get; }
        public ICommand NextDayCommand { get; }

        // ✨✨✨ 오류의 원인! 이 생성자는 아무 인수도 받지 않아야 합니다. ✨✨✨
        public DashboardViewModel()
        {
            TaskItems = new ObservableCollection<TaskItem>();
            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            AllMemos = new ObservableCollection<MemoItem>();

            AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
            EditTaskCommand = new RelayCommand(EditTask);
            DeleteTaskCommand = new RelayCommand(DeleteTask);
            AddTodoCommand = new RelayCommand(AddTodo, CanAddTodo);
            PrevDayCommand = new RelayCommand(p => ChangeDay(-1));
            TodayCommand = new RelayCommand(p => GoToToday());
            NextDayCommand = new RelayCommand(p => ChangeDay(1));

            DataManager.SettingsUpdated += OnSettingsUpdated;
        }

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

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TaskItems.Clear();
                    foreach (var task in loadedTasks)
                    {
                        if (_settings.TaskColors.TryGetValue(task.Text, out var colorHex))
                        {
                            try { task.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex); } catch { }
                        }
                        TaskItems.Add(task);
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading tasks: {ex.Message}"); }
        }

        private async Task LoadTodosAsync()
        {
            if (!File.Exists(DataManager.TodosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.TodosFilePath);
                var loadedTodos = await JsonSerializer.DeserializeAsync<List<TodoItem>>(stream, DataManager.JsonOptions);
                if (loadedTodos == null) return;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TodoItems.Clear();
                    foreach (var todo in loadedTodos) TodoItems.Add(todo);
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
                await Application.Current.Dispatcher.InvokeAsync(() =>
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
                var loadedMemos = await JsonSerializer.DeserializeAsync<List<MemoItem>>(stream, DataManager.JsonOptions);
                if (loadedMemos == null) return;
                await Application.Current.Dispatcher.InvokeAsync(() =>
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
        }

        private void DeleteTask(object parameter)
        {
            if (parameter is not TaskItem selectedTask) return;
            if (MessageBox.Show($"'{selectedTask.Text}' 과목을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            string taskNameToDelete = selectedTask.Text;
            if (_settings.TaskColors.ContainsKey(taskNameToDelete))
            {
                _settings.TaskColors.Remove(taskNameToDelete);
                SaveSettings();
            }

            var logsToRemove = TimeLogEntries.Where(l => l.TaskText == taskNameToDelete).ToList();
            foreach (var log in logsToRemove) TimeLogEntries.Remove(log);

            TaskItems.Remove(selectedTask);
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

        private void UpdateUIAfterDataLoad()
        {
            OnPropertyChanged(nameof(CurrentDateDisplay));
            RecalculateAllTotals();
            FilterTodos();
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
            await LoadTasksAsync();
        }
    }
}