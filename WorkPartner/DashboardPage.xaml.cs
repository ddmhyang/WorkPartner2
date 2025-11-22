using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WorkPartner;

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
        public ObservableCollection<MemoItem> AllMemos { get; set; }

        private MainWindow _parentWindow;
        private AppSettings _settings;
        private MemoWindow _memoWindow;
        private MiniTimerWindow _miniTimer;

        private readonly double _blockWidth = 35, _blockHeight = 17;
        private readonly double _hourLabelWidth = 30;
        private readonly double _verticalMargin = 1, _horizontalMargin = 1;
        private readonly double _borderLeftThickness = 1;
        private readonly double _borderBottomThickness = 1;

        private readonly double _rowHeight;
        private readonly double _cellWidth;

        private DateTime _currentDateForTimeline = DateTime.Today;

        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;

        private readonly Dictionary<string, BackgroundSoundPlayer> _soundPlayers = new();

        private readonly Dictionary<string, SolidColorBrush> _taskBrushCache = new();
        private static readonly SolidColorBrush DefaultGrayBrush = new SolidColorBrush(Colors.Gray);

        private bool _layoutMeasured = false;
        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            InitializeTimeTableBackground();
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

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            _rowHeight = _blockHeight + (_verticalMargin * 2) + _borderBottomThickness;
            _cellWidth = _blockWidth + (_horizontalMargin * 2) + _borderLeftThickness;
        }


        private void OnSettingsUpdated()
        {
            LoadSettings();
            _taskBrushCache.Clear();
            Dispatcher.Invoke(() =>
            {
                foreach (var taskItem in TaskItems)
                {
                    taskItem.ColorBrush = GetColorForTask(taskItem.Text);
                }

                if (DataContext is ViewModels.DashboardViewModel vm)
                {
                    foreach (var vmTask in vm.TaskItems)
                    {
                        vmTask.ColorBrush = GetColorForTask(vmTask.Text);
                    }
                }

                RenderTimeTable();
                UpdateCharacterInfoPanel();
            });
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>();

            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

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

        #region 데이터 저장 / 불러오기

        private void LoadSettings()
        {
            if (_settings == null) _settings = DataManager.LoadSettings();

            LoadUserImage();

            _taskBrushCache.Clear();

            Dispatcher.Invoke(() =>
            {
                if (TaskItems != null)
                {
                    foreach (var taskItem in TaskItems)
                    {
                        taskItem.ColorBrush = GetColorForTask(taskItem.Text);
                    }
                }

                if (DataContext is ViewModels.DashboardViewModel vm && vm.TaskItems != null)
                {
                    foreach (var vmTask in vm.TaskItems)
                    {
                        vmTask.ColorBrush = GetColorForTask(vmTask.Text);
                    }
                }

                RenderTimeTable();
                UpdateCharacterInfoPanel();
            });
        }
        private void SaveSettings() { DataManager.SaveSettings(_settings); }

        private async Task LoadTasksAsync()
        {
            if (!File.Exists(_tasksFilePath)) return;
            try
            {
                await using var stream = new FileStream(_tasksFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var loadedTasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream);
                if (loadedTasks == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TaskItems.Clear();
                    _taskBrushCache.Clear();

                    if (_settings == null) _settings = DataManager.LoadSettings();
                    if (_settings.TaskColors == null) _settings.TaskColors = new Dictionary<string, string>();

                    foreach (var task in loadedTasks)
                    {
                        if (_settings.TaskColors != null && _settings.TaskColors.TryGetValue(task.Text, out var colorHex))
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
            DataManager.SaveTasks(TaskItems);
        }

        private async Task LoadTodosAsync()
        {
            if (!File.Exists(_todosFilePath)) return;
            try
            {
                await using var stream = new FileStream(_todosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
            DataManager.SaveTodos(TodoItems);
        }

        private async Task LoadMemosAsync()
        {
            if (!File.Exists(_memosFilePath)) return;
            try
            {
                await using var stream = new FileStream(_memosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var loadedMemos = await JsonSerializer.DeserializeAsync<ObservableCollection<MemoItem>>(stream);
                if (loadedMemos == null) return;
                AllMemos.Clear();
                foreach (var memo in loadedMemos) AllMemos.Add(memo);
                UpdatePinnedMemoView();
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading memos: {ex.Message}"); }
        }

        public async System.Threading.Tasks.Task LoadAllDataAsync()
        {
            _settings = DataManager.LoadSettings();
            LoadSettings();
            await LoadTasksAsync();
            await LoadTodosAsync();
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
            CurrentDayDisplay.Text = _currentDateForTimeline.ToString("ddd");
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

            var palette = new HslColorPicker();
            var confirmButton = new Button
            {
                Content = "확인",
                Margin = new Thickness(0, 10, 0, 0)
            };

            var panel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = true
            };

            DockPanel.SetDock(confirmButton, Dock.Bottom);
            panel.Children.Add(confirmButton);

            panel.Children.Add(palette);

            var window = new Window
            {
                Title = "과목 색상 선택",
                Content = panel,
                Width = 280,
                Height = 380,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            confirmButton.Click += (s, args) =>
            {
                window.DialogResult = true;
                window.Close();
            };

            if (window.ShowDialog() == true)
            {
                var newColor = palette.SelectedColor;

                newTask.ColorBrush = new SolidColorBrush(newColor);

                _settings.TaskColors[newTask.Text] = newColor.ToString();
                SaveSettings();
            }

            TaskInput.Clear();
            SaveTasks();
            RenderTimeTable();
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            TaskItem selectedTask = null;

            if (sender is FrameworkElement button && button.DataContext is TaskItem taskFromButton)
            {
                selectedTask = taskFromButton;
            }
            else if (TaskListBox.SelectedItem is TaskItem taskFromList)
            {
                selectedTask = taskFromList;
            }

            if (selectedTask == null)
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

            foreach (var log in vm.TimeLogEntries.Where(l => l.TaskText == oldName))
            {
                log.TaskText = newName;
            }
            if (_settings.TaskColors.ContainsKey(oldName))
            {
                var color = _settings.TaskColors[oldName];
                _settings.TaskColors.Remove(oldName);
                _settings.TaskColors[newName] = color;
                _taskBrushCache.Remove(oldName);
            }

            selectedTask.Text = newName;

            SaveTasks();
            DataManager.SaveTimeLogs(vm.TimeLogEntries);
            SaveSettings();

            TaskListBox.Items.Refresh();
            RenderTimeTable();
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            TaskItem selectedTask = null;

            if (sender is FrameworkElement button && button.DataContext is TaskItem taskFromButton)
            {
                selectedTask = taskFromButton;
            }
            else if (TaskListBox.SelectedItem is TaskItem taskFromList)
            {
                selectedTask = taskFromList;
            }

            if (selectedTask == null)
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
                _taskBrushCache.Remove(taskNameToDelete);
                SaveSettings();
            }

            var logsToRemove = vm.TimeLogEntries.Where(l => l.TaskText == taskNameToDelete).ToList();
            foreach (var log in logsToRemove)
            {
                vm.TimeLogEntries.Remove(log);
            }

            SaveTasks();
            DataManager.SaveTimeLogs(vm.TimeLogEntries);
            RenderTimeTable();
        }

        private void TaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTaskButton_Click(sender, e);
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
            TodoItem selectedTodo = null;

            if (sender is FrameworkElement button && button.DataContext is TodoItem todoFromButton)
            {
                selectedTodo = todoFromButton;
            }
            else if (TodoTreeView.SelectedItem is TodoItem todoFromTree)
            {
                selectedTodo = todoFromTree;
            }
            if (selectedTodo == null)
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
            TodoItem selectedTodo = null;

            if (sender is FrameworkElement button && button.DataContext is TodoItem todoFromButton)
            {
                selectedTodo = todoFromButton;
            }
            else if (TodoTreeView.SelectedItem is TodoItem todoFromTree)
            {
                selectedTodo = todoFromTree;
            }

            if (selectedTodo == null)
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
            SaveTodos();
        }

        private void GoToClosetButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("추후 이미지 뷰어 기능으로 업데이트될 예정입니다.", "알림");
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
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            var now = DateTime.Now;
            var defaultStartTime = new DateTime(
                _currentDateForTimeline.Year,
                _currentDateForTimeline.Month,
                _currentDateForTimeline.Day,
                now.Hour,
                now.Minute,
                0
            );

            var templateLog = new TimeLogEntry
            {
                StartTime = defaultStartTime,
                EndTime = defaultStartTime.AddHours(1),
                TaskText = TaskItems.FirstOrDefault()?.Text ?? "과목 없음"
            };

            var win = new AddLogWindow(TaskItems, templateLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted) return;

            if (win.NewLogEntry != null)
            {
                vm.AddManualLog(win.NewLogEntry); 

                var addedTaskName = win.NewLogEntry.TaskText;
                var taskToSelect = TaskItems.FirstOrDefault(t => t.Text == addedTaskName);
                if (taskToSelect != null)
                {
                    TaskListBox.SelectedItem = taskToSelect;
                }
            }

            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not TimeLogEntry log) return;
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            var originalLog = vm.TimeLogEntries.FirstOrDefault(l =>
                l.StartTime == log.StartTime &&
                l.TaskText == log.TaskText &&
                l.EndTime == log.EndTime
            );
            if (originalLog == null) originalLog = log;

            var win = new AddLogWindow(TaskItems, originalLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted)
            {
                vm.DeleteLog(originalLog);
            }
            else
            {
                vm.UpdateLog(originalLog, win.NewLogEntry);

                var editedTaskName = win.NewLogEntry.TaskText;
                var taskToSelect = TaskItems.FirstOrDefault(t => t.Text == editedTaskName);
                if (taskToSelect != null)
                {
                    TaskListBox.SelectedItem = taskToSelect;
                }
            }
            RecalculateAllTotals();
            RenderTimeTable();
        }
        #endregion

        #region 화면 렌더링 및 UI 업데이트

        private void UpdateMainTimeDisplay() 
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;
            var todayLogs = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();
            var totalTimeToday = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));
            SelectedTaskTotalTimeDisplay.Text = $"총 작업 시간 | {totalTimeToday:hh\\:mm\\:ss}";
        }

        private void RecalculateAllTotals()
        {
            RecalculateAllTotals(TaskListBox.SelectedItem as TaskItem);
        }

        private void RecalculateAllTotals(TaskItem selectedTask = null) 
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            var todayLogs = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            var targetList = TaskListBox.ItemsSource as IEnumerable<TaskItem>;
            if (targetList == null) targetList = this.TaskItems;

            foreach (var task in targetList)
            {
                var taskLogs = todayLogs.Where(log => log.TaskText == task.Text);
                task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
            }
            UpdateMainTimeDisplay();
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TaskItem newSelectedItem = null;
            if (e.AddedItems.Count > 0)
            {
                newSelectedItem = e.AddedItems[0] as TaskItem;
            }

            RecalculateAllTotals(newSelectedItem);

            RenderTimeTable();
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_settings == null)
            {
                _settings = DataManager.LoadSettings();
            }

            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            if (_settings != null && _settings.TaskColors != null && _settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try
                {
                    var newBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    newBrush.Freeze();
                    _taskBrushCache[taskName] = newBrush;
                    return newBrush;
                }
                catch { }
            }

            return DefaultGrayBrush;
        }

        private void RenderTimeTable()
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            double dpiScale = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                dpiScale = source.CompositionTarget.TransformToDevice.M11;
            }

            double basePixelsPerMin = _blockWidth / 10.0;
            double baseCellWidth = _blockWidth + (_horizontalMargin * 2) + _borderLeftThickness;

            double drawingRowHeight = _rowHeight;

            if (Math.Abs(dpiScale - 1.5) < 0.01)
            {
                drawingRowHeight = _blockHeight + (_verticalMargin * 2.65) + _borderBottomThickness;
            }

            var bordersToRemove = SelectionCanvas.Children.OfType<Border>()
                                            .Where(b => b.Tag is TimeLogEntry)
                                            .ToList();
            foreach (var border in bordersToRemove) SelectionCanvas.Children.Remove(border);

            var logsForSelectedDate = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date)
                .OrderBy(l => l.StartTime)
                .ToList();

            foreach (var logEntry in logsForSelectedDate)
            {
                DateTime currentChunkStartTime = logEntry.StartTime;
                DateTime logEndTime = logEntry.EndTime;
                if (logEndTime.Date > currentChunkStartTime.Date)
                    logEndTime = currentChunkStartTime.Date.AddDays(1).AddTicks(-1);

                while (currentChunkStartTime < logEndTime)
                {
                    DateTime endOfCurrentHour = currentChunkStartTime.Date.AddHours(currentChunkStartTime.Hour + 1);
                    DateTime currentChunkEndTime = (logEndTime < endOfCurrentHour) ? logEndTime : endOfCurrentHour;

                    DateTime blockIterator = currentChunkStartTime;
                    while (blockIterator < currentChunkEndTime)
                    {
                        DateTime blockStart = blockIterator;
                        DateTime nextTenMinMark = blockStart.Date
                            .AddHours(blockStart.Hour)
                            .AddMinutes(Math.Floor(blockStart.Minute / 10.0) * 10)
                            .AddMinutes(10);
                        DateTime blockEnd = (nextTenMinMark < currentChunkEndTime) ? nextTenMinMark : currentChunkEndTime;

                        TimeSpan blockDuration = blockEnd - blockStart;
                        if (blockDuration.TotalSeconds <= 0) break;

                        int cellIndex = (int)Math.Floor(blockStart.Minute / 10.0);

                        double startX = _hourLabelWidth + _horizontalMargin + _borderLeftThickness;
                        double minuteOffset = (blockStart.TimeOfDay.TotalMinutes % 10.0) * basePixelsPerMin;
                        double leftOffset = startX + (cellIndex * baseCellWidth) + minuteOffset;

                        double topOffset = (blockStart.Hour * drawingRowHeight) + _verticalMargin;

                        double barWidth = blockDuration.TotalMinutes * basePixelsPerMin;

                        if (barWidth > 0)
                        {
                            var coloredBar = new Border
                            {
                                Width = barWidth,
                                Height = _blockHeight,
                                Background = GetColorForTask(logEntry.TaskText),
                                CornerRadius = new CornerRadius(2),
                                Tag = logEntry,
                                Cursor = Cursors.Hand,
                                ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}" }
                            };
                            coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;

                            Canvas.SetLeft(coloredBar, leftOffset);
                            Canvas.SetTop(coloredBar, topOffset);
                            Panel.SetZIndex(coloredBar, 1);
                            SelectionCanvas.Children.Add(coloredBar);
                        }
                        blockIterator = blockEnd;
                    }
                    currentChunkStartTime = currentChunkEndTime;
                }
            }

            SelectionCanvas.Height = 24 * drawingRowHeight;
            if (_selectionBox != null) Panel.SetZIndex(_selectionBox, 100);
        }

        private void InitializeTimeTableBackground()
        {
            TimeTableContainer.Children.Clear();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, _verticalMargin, 0, _verticalMargin)
                };

                if (hour == 0) hourRowPanel.Name = "HourRow_0";
                if (hour == 1) hourRowPanel.Name = "HourRow_1";

                var hourLabel = new TextBlock
                {
                    Text = $"{hour:00}",
                    Width = _hourLabelWidth,
                    Height = _blockHeight, 
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray,
                    FontSize = 8
                };
                hourRowPanel.Children.Add(hourLabel);

                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockContainer = new Grid
                    {
                        Width = _blockWidth, 
                        Height = _blockHeight,
                        Background = (Brush)FindResource("SecondaryBackgroundBrush"),
                        Margin = new Thickness(_horizontalMargin, 0, _horizontalMargin, 0) 
                    };

                    blockContainer.SetResourceReference(Grid.BackgroundProperty, "SecondaryBackgroundBrush");

                    var blockWithBorder = new Border
                    {
                        BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 1),
                        Child = blockContainer
                    };

                    hourRowPanel.Children.Add(blockWithBorder);
                }

                TimeTableContainer.Children.Add(hourRowPanel);
            }

            Debug.WriteLine($"InitializeTimeTableBackground: blockWidth={_blockWidth}, cellWidth={_cellWidth}, rowHeight={_rowHeight}, hourLabelWidth={_hourLabelWidth}");
        }


        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_settings == null) return;
        }


        private async void GoToAvatarButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void UpdateDateAndUI()
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd");
            CurrentDayDisplay.Text = _currentDateForTimeline.ToString("ddd");

            UpdateTaskListBoxSource();

            RenderTimeTable();
            RecalculateAllTotals();
            FilterTodos();
        }

        private void UpdateTaskListBoxSource()
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            if (_currentDateForTimeline.Date == DateTime.Today)
            {
                if (TaskListBox.ItemsSource != vm.TaskItems)
                {
                    TaskListBox.ItemsSource = vm.TaskItems;
                }
            }
            else
            {
                if (TaskListBox.ItemsSource != this.TaskItems)
                {
                    TaskListBox.ItemsSource = this.TaskItems;
                }
            }
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
            if (e.OriginalSource is Border) return;

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
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            if (!_isDragging) return;
            _isDragging = false;
            SelectionCanvas.ReleaseMouseCapture();
            _selectionBox.Visibility = Visibility.Collapsed;

            var selectionRect = new Rect(Canvas.GetLeft(_selectionBox), Canvas.GetTop(_selectionBox), _selectionBox.Width, _selectionBox.Height);

            if (selectionRect.Width < 10 && selectionRect.Height < 10) return;

            var selectedLogs = new List<TimeLogEntry>();
            foreach (var child in SelectionCanvas.Children.OfType<Border>())
            {
                if (child.Tag is TimeLogEntry logEntry)
                {
                    var logRect = new Rect(Canvas.GetLeft(child), Canvas.GetTop(child), child.ActualWidth, child.ActualHeight);
                    if (selectionRect.IntersectsWith(logRect))
                    {
                        selectedLogs.Add(logEntry);
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
                foreach (var log in distinctLogs)
                {
                    var updatedLog = new TimeLogEntry
                    {
                        StartTime = log.StartTime,
                        EndTime = log.EndTime,
                        TaskText = newText, // 과목만 변경
                        FocusScore = log.FocusScore,
                        BreakActivities = log.BreakActivities
                    };
                    vm.UpdateLog(log, updatedLog);
                }
            }
            else if (bulkEditWindow.Result == BulkEditResult.Delete)
            {
                foreach (var log in distinctLogs)
                {
                    vm.DeleteLog(log); 
                }
            }

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


        private (double H, double S, double L) WpfColorToHsl(Color wpfColor)
        {
            double r = wpfColor.R / 255.0;
            double g = wpfColor.G / 255.0;
            double b = wpfColor.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            double h = 0, s = 0, l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; 
            }
            else
            {
                double delta = max - min;
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

                if (max == r)
                {
                    h = (g - b) / delta + (g < b ? 6.0 : 0.0);
                }
                else if (max == g)
                {
                    h = (b - r) / delta + 2.0;
                }
                else 
                {
                    h = (r - g) / delta + 4.0;
                }

                h /= 6.0; 
            }

            return (h * 360.0, s, l);
        }

        private void ChangeTaskColor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: TaskItem selectedTask }) return;

            TaskListBox.SelectedItem = selectedTask;

            var palette = new HslColorPicker();

            if (_settings.TaskColors.TryGetValue(selectedTask.Text, out var hex))
            {
                try
                {
                    var currentColor = (Color)ColorConverter.ConvertFromString(hex);
                    (double H, double S, double L) hsl = WpfColorToHsl(currentColor);
                    palette.SetHsl(hsl.H, hsl.S, hsl.L);
                }
                catch { /* ignore invalid hex */ }
            }

            var confirmButton = new Button
            {
                Content = "확인",
                Margin = new Thickness(0, 10, 0, 0)
            };

            var panel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = true // 👈 (중요)
            };

            DockPanel.SetDock(confirmButton, Dock.Bottom);
            panel.Children.Add(confirmButton);

            panel.Children.Add(palette);

            var window = new Window
            {
                Title = "과목 색상 변경",
                Content = panel,
                Width = 280, 
                Height = 380, 
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            confirmButton.Click += (s, args) =>
            {
                window.DialogResult = true;
                window.Close();
            };

            if (window.ShowDialog() == true)
            {
                var newColor = palette.SelectedColor;
                _settings.TaskColors[selectedTask.Text] = newColor.ToString();
                selectedTask.ColorBrush = new SolidColorBrush(newColor);
                DataManager.SaveSettings(_settings);

                RenderTimeTable();
            }

            e.Handled = true;
        }

        private void DashboardPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            LoadSettings();
            if (e.OldValue is ViewModels.DashboardViewModel oldVm)
            {
                oldVm.TimeUpdated -= OnViewModelTimeUpdated;
                oldVm.TimerStoppedAndSaved -= OnViewModelTimerStopped;
                oldVm.CurrentTaskChanged -= OnViewModelTaskChanged;
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

                if (oldVm.TaskItems != null)
                    oldVm.TaskItems.CollectionChanged -= TaskItems_CollectionChanged;
            }

            if (e.NewValue is ViewModels.DashboardViewModel newVm)
            {
                newVm.TimeUpdated += OnViewModelTimeUpdated;
                newVm.TimerStoppedAndSaved += OnViewModelTimerStopped;
                newVm.CurrentTaskChanged += OnViewModelTaskChanged;
                newVm.PropertyChanged += OnViewModelPropertyChanged;

                foreach (var task in newVm.TaskItems)
                {
                    if (task.ColorBrush == null)
                        task.ColorBrush = GetColorForTask(task.Text);
                }

                newVm.TaskItems.CollectionChanged += TaskItems_CollectionChanged;

                CurrentTaskDisplay.Text = newVm.CurrentTaskDisplayText;

                UpdateTaskListBoxSource();
            }
        }

        private void TaskItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TaskItem newTask in e.NewItems)
                {
                    if (newTask.ColorBrush == null)
                    {
                        newTask.ColorBrush = GetColorForTask(newTask.Text);
                    }
                }
            }
        }


        private void OnViewModelTimerStopped(object sender, EventArgs e)
        {

            Dispatcher.Invoke(() =>
            {
                RecalculateAllTotals();
                RenderTimeTable(); 
            });
        }

        private void OnViewModelTimeUpdated(string newTime)
        {
            if (_miniTimer != null && _miniTimer.IsVisible)
            {
                LoadSettings();
                _miniTimer.UpdateData(_settings, CurrentTaskDisplay.Text, newTime);
            }

            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;
            if (this.TaskItems == null) return; 

            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                foreach (var vmTask in vm.TaskItems)
                {
                    if (vmTask.ColorBrush == null)
                    {
                        vmTask.ColorBrush = GetColorForTask(vmTask.Text);
                    }

                    var pageTask = this.TaskItems.FirstOrDefault(t => t.Text == vmTask.Text);
                    if (pageTask != null && pageTask.TotalTime != vmTask.TotalTime)
                    {
                        pageTask.TotalTime = vmTask.TotalTime;
                    }
                }
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            if (e.PropertyName == nameof(ViewModels.DashboardViewModel.TotalTimeTodayDisplayText))
            {
                if (sender is ViewModels.DashboardViewModel vm)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SelectedTaskTotalTimeDisplay.Text = vm.TotalTimeTodayDisplayText;
                    });
                }
            }
        }

        private void OnViewModelTaskChanged(string newTaskName)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentTaskDisplay.Text = newTaskName;
            });

            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            Dispatcher.Invoke(() =>
            {
                var foundTask = TaskItems.FirstOrDefault(t => t.Text == newTaskName);
                if (foundTask != null && TaskListBox.SelectedItem != foundTask)
                {
                    TaskListBox.SelectedItem = foundTask;
                }
            });
        }
        #endregion

        private void EvaluateDayButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("이 기능은 더 이상 사용되지 않습니다.", "알림");
        }

        private void LoadUserImage()
        {
            if (UserProfileImage == null) return;

            GifHelper.StopGif(UserProfileImage);

            string imagePath = _settings.UserImagePath;

            if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
            {
                UserProfileImage.Source = null;
                return;
            }

            GifHelper.PlayGif(UserProfileImage, imagePath);
        }


        private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "이미지 선택",
                Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.gif;*.bmp|모든 파일|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _settings.UserImagePath = openFileDialog.FileName;

                DataManager.SaveSettings(_settings);

                LoadUserImage();
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_settings.UserImagePath))
            {
                MessageBox.Show("삭제할 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("현재 설정된 이미지를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _settings.UserImagePath = null;

                DataManager.SaveSettings(_settings);

                LoadUserImage();
            }
        }
    }
}