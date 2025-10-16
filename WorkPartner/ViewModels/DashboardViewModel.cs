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
    public class DashboardViewModel : BaseViewModel
    {
        #region Fields
        private AppSettings _settings;
        private DateTime _currentDateForTimeline = DateTime.Today;
        #endregion

        #region Properties
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
                OnPropertyChanged();
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

        public string CurrentDateDisplay => _currentDateForTimeline.ToString("yyyy-MM-dd");

        public string Username => _settings?.Username;
        public string CoinsDisplay => _settings?.Coins.ToString("N0");

        #endregion

        #region Commands
        public ICommand AddTaskCommand { get; }
        public ICommand EditTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand AddTodoCommand { get; }
        public ICommand PrevDayCommand { get; }
        public ICommand TodayCommand { get; }
        public ICommand NextDayCommand { get; }
        #endregion

        public DashboardViewModel()
        {
            // Initialize Collections
            TaskItems = new ObservableCollection<TaskItem>();
            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            AllMemos = new ObservableCollection<MemoItem>();

            // Initialize Commands
            AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
            EditTaskCommand = new RelayCommand(EditTask, CanEditOrDeleteTask);
            DeleteTaskCommand = new RelayCommand(DeleteTask, CanEditOrDeleteTask);
            AddTodoCommand = new RelayCommand(AddTodo, CanAddTodo);
            PrevDayCommand = new RelayCommand(p => ChangeDay(-1));
            TodayCommand = new RelayCommand(p => GoToToday());
            NextDayCommand = new RelayCommand(p => ChangeDay(1));

            // Load initial data
            _ = LoadAllDataAsync();

            DataManager.SettingsUpdated += OnSettingsUpdated;
        }

        #region Data Logic
        private async Task LoadAllDataAsync()
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
                var loadedTasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream);
                if (loadedTasks == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TaskItems.Clear();
                    foreach (var task in loadedTasks)
                    {
                        if (_settings.TaskColors.TryGetValue(task.Text, out var colorHex))
                        {
                            task.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
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
                var loadedTodos = await JsonSerializer.DeserializeAsync<ObservableCollection<TodoItem>>(stream);
                if (loadedTodos == null) return;
                TodoItems.Clear();
                foreach (var todo in loadedTodos) TodoItems.Add(todo);
                FilterTodos();
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading todos: {ex.Message}"); }
        }

        private async Task LoadTimeLogsAsync()
        {
            if (!File.Exists(DataManager.TimeLogFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.TimeLogFilePath);
                var loadedLogs = await JsonSerializer.DeserializeAsync<ObservableCollection<TimeLogEntry>>(stream);
                if (loadedLogs == null) return;
                TimeLogEntries.Clear();
                foreach (var log in loadedLogs) TimeLogEntries.Add(log);
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading time logs: {ex.Message}"); }
        }

        private async Task LoadMemosAsync()
        {
            if (!File.Exists(DataManager.MemosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(DataManager.MemosFilePath);
                var loadedMemos = await JsonSerializer.DeserializeAsync<ObservableCollection<MemoItem>>(stream);
                if (loadedMemos == null) return;
                AllMemos.Clear();
                foreach (var memo in loadedMemos) AllMemos.Add(memo);
                // Pinned memo view update will be handled by the View
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading memos: {ex.Message}"); }
        }

        private void SaveTasks() => DataManager.SaveTasks(TaskItems);
        private void SaveTodos() => DataManager.SaveTodos(TodoItems);
        private void SaveTimeLogs() => DataManager.SaveTimeLogs(TimeLogEntries);
        private void SaveSettings() => DataManager.SaveSettingsAndNotify(_settings);

        #endregion

        #region Command Methods
        private bool CanAddTask(object parameter) => !string.IsNullOrWhiteSpace(NewTaskText);
        private void AddTask(object parameter)
        {
            if (TaskItems.Any(t => t.Text.Equals(NewTaskText, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTask = new TaskItem { Text = NewTaskText };
            TaskItems.Add(newTask);

            var colorPicker = new ColorPalette { Owner = Application.Current.MainWindow };
            if (colorPicker.ShowDialog() == true)
            {
                _settings.TaskColors[newTask.Text] = colorPicker.SelectedColor.ToString();
                SaveSettings();
            }

            NewTaskText = string.Empty;
            SaveTasks();
        }

        private bool CanEditOrDeleteTask(object parameter) => parameter is TaskItem;

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

            foreach (var log in TimeLogEntries.Where(l => l.TaskText == oldName))
            {
                log.TaskText = newName;
            }
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

            if (MessageBox.Show($"'{selectedTask.Text}' 과목을 삭제하시겠습니까?\n관련된 모든 학습 기록도 삭제됩니다.", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            string taskNameToDelete = selectedTask.Text;
            TaskItems.Remove(selectedTask);

            if (_settings.TaskColors.ContainsKey(taskNameToDelete))
            {
                _settings.TaskColors.Remove(taskNameToDelete);
                SaveSettings();
            }

            var logsToRemove = TimeLogEntries.Where(l => l.TaskText == taskNameToDelete).ToList();
            foreach (var log in logsToRemove)
            {
                TimeLogEntries.Remove(log);
            }

            SaveTasks();
            SaveTimeLogs();
            // RecalculateAllTotals will be called from the View
        }

        private bool CanAddTodo(object parameter) => !string.IsNullOrWhiteSpace(NewTodoText);
        private void AddTodo(object parameter)
        {
            var newTodo = new TodoItem
            {
                Text = NewTodoText,
                Date = _currentDateForTimeline.Date
            };

            // Note: Handling parent-child relationship for Todos requires interaction with the View's SelectedItem.
            // This might need a slightly different approach or a message bus. For now, we add to the root.
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

        #region UI Update Logic
        private void UpdateUIAfterDataLoad()
        {
            OnPropertyChanged(nameof(CurrentDateDisplay));
            RecalculateAllTotals();
            FilterTodos();
            // The View will be responsible for re-rendering the timeline
        }

        public void RecalculateAllTotals()
        {
            foreach (var task in TaskItems)
            {
                var taskLogs = TimeLogEntries.Where(log => log.TaskText == task.Text && log.StartTime.Date == _currentDateForTimeline.Date);
                task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
            }
        }

        private void FilterTodos()
        {
            FilteredTodoItems.Clear();
            var filtered = TodoItems.Where(t => t.Date.Date == _currentDateForTimeline.Date);
            foreach (var item in filtered) FilteredTodoItems.Add(item);
        }

        private void OnSettingsUpdated()
        {
            LoadSettings();
            // The View will handle UI updates like brush caches.
        }
        #endregion
    }
}