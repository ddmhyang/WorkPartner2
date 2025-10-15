using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WorkPartner.AI;
using WorkPartner.Services.Implementations;
using WorkPartner.ViewModels; // ★ 추가: ViewModel 네임스페이스 using

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언

        private readonly string _tasksFilePath = DataManager.TasksFilePath;
        private readonly string _todosFilePath = DataManager.TodosFilePath;
        private readonly string _timeLogFilePath = DataManager.TimeLogFilePath;
        private readonly string _memosFilePath = DataManager.MemosFilePath;

        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; }
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; }
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<MemoItem> AllMemos { get; set; }

        private MainWindow _parentWindow;
        private AppSettings _settings;
        private MemoWindow _memoWindow;
        private MiniTimerWindow _miniTimer;

        private DateTime _currentDateForTimeline = DateTime.Today;

        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;

        private readonly Dictionary<string, BackgroundSoundPlayer> _soundPlayers = new();

        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            InitializeSoundPlayers();

            waveSlider.ValueChanged += SoundSlider_ValueChanged;
            forestSlider.ValueChanged += SoundSlider_ValueChanged;
            rainSlider.ValueChanged += SoundSlider_ValueChanged;
            campfireSlider.ValueChanged += SoundSlider_ValueChanged;
            this.DataContextChanged += DashboardPage_DataContextChanged;

            _selectionBox = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255)),
                Visibility = Visibility.Collapsed
            };
            if (!SelectionCanvas.Children.Contains(_selectionBox))
            {
                SelectionCanvas.Children.Add(_selectionBox);
            }

            DataManager.SettingsUpdated += OnSettingsUpdated;
            this.Unloaded += (s, e) => DataManager.SettingsUpdated -= OnSettingsUpdated;
        }

        private void OnSettingsUpdated()
        {
            LoadSettings();
            Dispatcher.Invoke(() =>
            {
                foreach (var taskItem in TaskItems)
                {
                    if (_settings.TaskColors.TryGetValue(taskItem.Text, out string colorHex))
                    {
                        taskItem.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    }
                }
                RenderTimeTable();
            });
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>();
            TaskListBox.ItemsSource = TaskItems;

            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            AllMemos = new ObservableCollection<MemoItem>();
        }

        private void InitializeSoundPlayers()
        {
            var soundControls = new Dictionary<string, Slider>
            {
                { "wave", waveSlider },
                { "forest", forestSlider },
                { "rain", rainSlider },
                { "campfire", campfireSlider }
            };

            foreach (var sound in soundControls)
            {
                var player = new BackgroundSoundPlayer($"Sounds/{sound.Key}.mp3");
                _soundPlayers[sound.Key] = player;
                sound.Value.Tag = sound.Key;
            }
        }

        #region 데이터 저장 / 불러오기 (점진적으로 ViewModel과 Service로 이전 필요)

        public void LoadSettings() { _settings = DataManager.LoadSettings(); }
        private void SaveSettings() { DataManager.SaveSettingsAndNotify(_settings); }

        private async Task LoadTasksAsync()
        {
            if (!File.Exists(_tasksFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(_tasksFilePath);
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

        private void SaveTasks()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TaskItems, options);
            File.WriteAllText(_tasksFilePath, json);
        }

        private async Task LoadTodosAsync()
        {
            if (!File.Exists(_todosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(_todosFilePath);
                var loadedTodos = await JsonSerializer.DeserializeAsync<ObservableCollection<TodoItem>>(stream);
                if (loadedTodos == null) return;
                TodoItems.Clear();
                foreach (var todo in loadedTodos) TodoItems.Add(todo);
                FilterTodos();
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading todos: {ex.Message}"); }
        }

        private void SaveTodos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TodoItems, options);
            File.WriteAllText(_todosFilePath, json);
        }

        private async Task LoadTimeLogsAsync()
        {
            if (!File.Exists(_timeLogFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(_timeLogFilePath);
                var loadedLogs = await JsonSerializer.DeserializeAsync<ObservableCollection<TimeLogEntry>>(stream);
                if (loadedLogs == null) return;
                TimeLogEntries.Clear();
                foreach (var log in loadedLogs) TimeLogEntries.Add(log);
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading time logs: {ex.Message}"); }
        }

        private void SaveTimeLogs()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TimeLogEntries, options);
            File.WriteAllText(_timeLogFilePath, json);
        }

        private async Task LoadMemosAsync()
        {
            if (!File.Exists(_memosFilePath)) return;
            try
            {
                await using var stream = File.OpenRead(_memosFilePath);
                var loadedMemos = await JsonSerializer.DeserializeAsync<ObservableCollection<MemoItem>>(stream);
                if (loadedMemos == null) return;
                AllMemos.Clear();
                foreach (var memo in loadedMemos) AllMemos.Add(memo);
                UpdatePinnedMemoView();
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading memos: {ex.Message}"); }
        }

        public async Task LoadAllDataAsync()
        {
            LoadSettings();
            await LoadTasksAsync();
            await LoadTodosAsync();
            await LoadTimeLogsAsync();
            await LoadMemosAsync();
            UpdateCharacterInfoPanel();
            RecalculateAllTotals();
            RenderTimeTable();
        }

        #endregion

        #region UI 이벤트 핸들러

        private async void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                await LoadAllDataAsync();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd");
            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string newTaskText = TaskInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(newTaskText)) return;

            if (TaskItems.Any(t => t.Text.Equals(newTaskText, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var newTask = new TaskItem { Text = newTaskText };
            TaskItems.Add(newTask);

            var colorPicker = new ColorPalette { Owner = Window.GetWindow(this) };
            if (colorPicker.ShowDialog() == true)
            {
                _settings.TaskColors[newTask.Text] = colorPicker.SelectedColor.ToString();
                SaveSettings();
            }

            TaskInput.Clear();
            SaveTasks();
            RenderTimeTable();
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is not TaskItem selectedTask)
            {
                MessageBox.Show("수정할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text) { Owner = Window.GetWindow(this) };
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

            TaskListBox.Items.Refresh();
            RenderTimeTable();
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.StopTimerCommand.Execute(null);
            }

            if (TaskListBox.SelectedItem is not TaskItem selectedTask)
            {
                MessageBox.Show("삭제할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
            RenderTimeTable();
            RecalculateAllTotals();
        }

        private void TaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTaskButton_Click(sender, e);
        }

        // ✨ 오류 해결을 위해 빠진 메서드 추가
        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void AddTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TodoInput.Text)) return;
            var newTodo = new TodoItem
            {
                Text = TodoInput.Text,
                Date = _currentDateForTimeline.Date
            };

            if (TodoTreeView.SelectedItem is TodoItem parentTodo)
            {
                parentTodo.SubTasks.Add(newTodo);
            }
            else
            {
                TodoItems.Add(newTodo);
            }

            TodoInput.Clear();
            SaveTodos();
            FilterTodos();
        }

        private void EditTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is not TodoItem selectedTodo)
            {
                MessageBox.Show("수정할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var inputWindow = new InputWindow("할 일 수정", selectedTodo.Text) { Owner = Window.GetWindow(this) };
            if (inputWindow.ShowDialog() == true)
            {
                selectedTodo.Text = inputWindow.ResponseText;
                SaveTodos();
            }
        }

        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is not TodoItem selectedTodo)
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"'{selectedTodo.Text}' 할 일을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                RemoveTodoItem(TodoItems, selectedTodo);
                SaveTodos();
                FilterTodos();
            }
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTodoButton_Click(sender, e);
        }

        private void SaveTodos_Event(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: TodoItem todoItem })
            {
                if (todoItem.IsCompleted && !todoItem.HasBeenRewarded)
                {
                    _settings.Coins += 10;
                    todoItem.HasBeenRewarded = true;
                    UpdateCoinDisplay();
                    SaveSettings();
                    SoundPlayer.PlayCompleteSound();
                }
            }
            SaveTodos();
        }

        private void GoToClosetButton_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow?.NavigateToPage("Avatar");
        }

        private void MemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_memoWindow == null || !_memoWindow.IsVisible)
            {
                _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) };
                _memoWindow.Show();
            }
            else
            {
                _memoWindow.Activate();
            }
        }

        private void AddManualLogButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;
            if (win.NewLogEntry != null)
            {
                TimeLogEntries.Add(win.NewLogEntry);
            }
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not TimeLogEntry log) return;

            var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted)
            {
                TimeLogEntries.Remove(log);
            }
            else
            {
                log.StartTime = win.NewLogEntry.StartTime;
                log.EndTime = win.NewLogEntry.EndTime;
                log.TaskText = win.NewLogEntry.TaskText;
                log.FocusScore = win.NewLogEntry.FocusScore;
            }
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
        }

        #endregion

        #region 화면 렌더링 및 UI 업데이트

        private void RecalculateAllTotals()
        {
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date);
            var totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));

            foreach (var task in TaskItems)
            {
                var taskLogs = TimeLogEntries.Where(log => log.TaskText == task.Text && log.StartTime.Date == _currentDateForTimeline.Date);
                task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
            }
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_settings != null && _settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try { return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex); }
                catch { /* ignore */ }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        private void RenderTimeTable()
        {
            TimeTableContainer.Children.Clear();
            var logsForSelectedDate = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            double blockWidth = 35, blockHeight = 17, hourLabelWidth = 30;

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                var hourLabel = new TextBlock
                {
                    Text = $"{hour:00}",
                    Width = hourLabelWidth,
                    Height = blockHeight,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray,
                    FontSize = 8
                };
                hourRowPanel.Children.Add(hourLabel);

                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockStartTime = new TimeSpan(hour, minuteBlock * 10, 0);
                    var blockEndTime = blockStartTime.Add(TimeSpan.FromMinutes(10));
                    var blockContainer = new Grid
                    {
                        Width = blockWidth,
                        Height = blockHeight,
                        Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                        Margin = new Thickness(1, 0, 1, 0)
                    };
                    var overlappingLogs = logsForSelectedDate.Where(log => log.StartTime.TimeOfDay < blockEndTime && log.EndTime.TimeOfDay > blockStartTime).ToList();

                    foreach (var logEntry in overlappingLogs)
                    {
                        var segmentStart = logEntry.StartTime.TimeOfDay > blockStartTime ? logEntry.StartTime.TimeOfDay : blockStartTime;
                        var segmentEnd = logEntry.EndTime.TimeOfDay < blockEndTime ? logEntry.EndTime.TimeOfDay : blockEndTime;
                        var segmentDuration = segmentEnd - segmentStart;
                        if (segmentDuration.TotalSeconds <= 0) continue;

                        double barWidth = (segmentDuration.TotalMinutes / 10.0) * blockContainer.Width;
                        double leftOffset = ((segmentStart - blockStartTime).TotalMinutes / 10.0) * blockContainer.Width;
                        if (barWidth < 1) continue;

                        var coloredBar = new Border
                        {
                            Width = barWidth,
                            Height = blockContainer.Height,
                            Background = GetColorForTask(logEntry.TaskText),
                            CornerRadius = new CornerRadius(2),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(leftOffset, 0, 0, 0),
                            ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}\n\n클릭하여 수정 또는 삭제" },
                            Tag = logEntry,
                            Cursor = Cursors.Hand
                        };
                        coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;
                        blockContainer.Children.Add(coloredBar);
                    }
                    var blockWithBorder = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 0), Child = blockContainer };
                    hourRowPanel.Children.Add(blockWithBorder);
                }
                TimeTableContainer.Children.Add(hourRowPanel);
            }
        }

        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_settings == null) return;
            UsernameDisplay.Text = _settings.Username;
            UpdateCoinDisplay();
            CharacterPreview.UpdateCharacter();
        }

        private void UpdateCoinDisplay()
        {
            if (_settings != null) CoinDisplay.Text = _settings.Coins.ToString("N0");
        }

        private void UpdateDateAndUI()
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd");
            RenderTimeTable();
            RecalculateAllTotals();
            FilterTodos();
        }

        private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(-1);
            UpdateDateAndUI();
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = DateTime.Today;
            UpdateDateAndUI();
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(1);
            UpdateDateAndUI();
        }

        private void FilterTodos()
        {
            FilteredTodoItems.Clear();
            var filtered = TodoItems.Where(t => t.Date.Date == _currentDateForTimeline.Date);
            foreach (var item in filtered) FilteredTodoItems.Add(item);
        }

        private bool RemoveTodoItem(ObservableCollection<TodoItem> collection, TodoItem itemToRemove)
        {
            if (collection.Remove(itemToRemove)) return true;
            foreach (var item in collection)
            {
                if (item.SubTasks != null && RemoveTodoItem(item.SubTasks, itemToRemove)) return true;
            }
            return false;
        }

        #endregion

        #region 타임라인 드래그 및 일괄 수정

        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(SelectionCanvas);
            _selectionBox.SetValue(Canvas.LeftProperty, _dragStartPoint.X);
            _selectionBox.SetValue(Canvas.TopProperty, _dragStartPoint.Y);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            _selectionBox.Visibility = Visibility.Visible;
            SelectionCanvas.CaptureMouse();
        }

        private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(SelectionCanvas);
            var x = Math.Min(currentPoint.X, _dragStartPoint.X);
            var y = Math.Min(currentPoint.Y, _dragStartPoint.Y);
            var w = Math.Abs(currentPoint.X - _dragStartPoint.X);
            var h = Math.Abs(currentPoint.Y - _dragStartPoint.Y);

            _selectionBox.SetValue(Canvas.LeftProperty, x);
            _selectionBox.SetValue(Canvas.TopProperty, y);
            _selectionBox.Width = w;
            _selectionBox.Height = h;
        }

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            SelectionCanvas.ReleaseMouseCapture();
            _selectionBox.Visibility = Visibility.Collapsed;

            var selectionRect = new Rect(
                Math.Min(_dragStartPoint.X, e.GetPosition(SelectionCanvas).X),
                Math.Min(_dragStartPoint.Y, e.GetPosition(SelectionCanvas).Y),
                Math.Abs(_dragStartPoint.X - e.GetPosition(SelectionCanvas).X),
                Math.Abs(_dragStartPoint.Y - e.GetPosition(SelectionCanvas).Y)
            );

            if (selectionRect.Width < 10 && selectionRect.Height < 10) return;

            var selectedLogs = new List<TimeLogEntry>();
            foreach (var child in TimeTableContainer.Children.OfType<StackPanel>())
            {
                foreach (var grandChild in child.Children.OfType<Border>())
                {
                    if (grandChild.Child is Grid grid)
                    {
                        foreach (var logBar in grid.Children.OfType<Border>())
                        {
                            if (logBar.Tag is TimeLogEntry logEntry)
                            {
                                Point logBarPos = logBar.TranslatePoint(new Point(0, 0), SelectionCanvas);
                                Rect logRect = new Rect(logBarPos.X, logBarPos.Y, logBar.ActualWidth, logBar.ActualHeight);
                                if (selectionRect.IntersectsWith(logRect))
                                {
                                    selectedLogs.Add(logEntry);
                                }
                            }
                        }
                    }
                }
            }

            if (!selectedLogs.Any()) return;

            var distinctLogs = selectedLogs.Distinct().OrderBy(l => l.StartTime).ToList();
            var bulkEditWindow = new BulkEditLogsWindow(distinctLogs, TaskItems) { Owner = Window.GetWindow(this) };

            if (bulkEditWindow.ShowDialog() != true) return;

            if (bulkEditWindow.Result == BulkEditResult.ChangeTask)
            {
                string newText = bulkEditWindow.SelectedTask.Text;
                foreach (var log in distinctLogs) log.TaskText = newText;
            }
            else if (bulkEditWindow.Result == BulkEditResult.Delete)
            {
                foreach (var log in distinctLogs) TimeLogEntries.Remove(log);
            }
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("타임라인에서 수정하고 싶은 영역을 마우스로 드래그하세요.\n드래그가 끝나면 수정 창이 나타납니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region 기타 유틸리티 및 View 통신

        public void SetParentWindow(MainWindow window) => _parentWindow = window;
        public void SetMiniTimerReference(MiniTimerWindow timer) => _miniTimer = timer;

        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null && sender is Button button && button.Tag is string pageName)
            {
                await _parentWindow.NavigateToPage(pageName);
            }
        }

        private void UpdatePinnedMemoView()
        {
            var pinnedMemo = AllMemos.FirstOrDefault(m => m.IsPinned);
            if (pinnedMemo != null && !string.IsNullOrWhiteSpace(pinnedMemo.Content))
            {
                PinnedMemoView.Visibility = Visibility.Visible;
                NoPinnedMemoText.Visibility = Visibility.Collapsed;
                PinnedMemoContent.Text = pinnedMemo.Content;
            }
            else
            {
                PinnedMemoView.Visibility = Visibility.Collapsed;
                NoPinnedMemoText.Visibility = Visibility.Visible;
            }
        }

        private void PinnedMemo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var memoWindow = new MemoWindow { Owner = Window.GetWindow(this) };
            memoWindow.Closed += async (s, args) => await LoadMemosAsync();
            memoWindow.ShowDialog();
        }

        private void SoundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.Tag is string soundName)
            {
                if (_soundPlayers.TryGetValue(soundName, out var player))
                {
                    if (e.NewValue > 0.05 && !player.IsPlaying) player.Play();
                    else if (e.NewValue <= 0.05 && player.IsPlaying) player.Stop();
                    player.Volume = e.NewValue;
                }
            }
        }
        private void ChangeTaskColor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: TaskItem selectedTask }) return;

            TaskListBox.SelectedItem = selectedTask;
            var colorPicker = new ColorPalette { Owner = Window.GetWindow(this) };

            if (colorPicker.ShowDialog() != true) return;

            var newColor = colorPicker.SelectedColor;
            _settings.TaskColors[selectedTask.Text] = newColor.ToString();

            selectedTask.ColorBrush = new SolidColorBrush(newColor);
            DataManager.SaveSettingsAndNotify(_settings);

            e.Handled = true;
        }

        private void DashboardPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DashboardViewModel oldViewModel)
            {
                oldViewModel.TimeUpdated -= OnViewModelTimeUpdated;
            }
            if (e.NewValue is DashboardViewModel newViewModel)
            {
                newViewModel.TimeUpdated += OnViewModelTimeUpdated;
            }
        }

        private void OnViewModelTimeUpdated(string newTime)
        {
            _miniTimer?.UpdateTime(newTime);
        }

        #endregion
    }
}