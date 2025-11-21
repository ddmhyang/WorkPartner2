// 파일: DashboardPage.xaml.cs
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
                    if (_settings.TaskColors.TryGetValue(taskItem.Text, out string colorHex))
                    {
                        taskItem.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    }
                }
                RenderTimeTable();
                UpdateCharacterInfoPanel();
            });
        }

        private void InitializeData()
        {
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
            // (혹시 모르니 여기서도 한 번 더 로드)
            if (_settings == null) _settings = DataManager.LoadSettings();

            // 2. 이미지 로드 (여기가 중요!)
            LoadUserImage();

            // 3. 현재 작업
            if (CurrentTaskTextBlock != null)
                CurrentTaskTextBlock.Text = $"현재 작업 : {_settings.CurrentTask}";
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
                await using var stream = new FileStream(_memosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); // ✨ [수정]
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
            SaveTodos(); // 저장 기능만 남김
        }

        private void GoToClosetButton_Click(object sender, RoutedEventArgs e)
        {
            // _parentWindow?.NavigateToPage("Avatar"); // 🗑️ [삭제] 이동 기능 막음
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

        private void UpdateMainTimeDisplay() // 👈 파라미터 삭제
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

        private void RecalculateAllTotals(TaskItem selectedTask)
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            var todayLogs = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            foreach (var task in TaskItems)
            {
                var taskLogs = todayLogs.Where(log => log.TaskText == task.Text);
                task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
            }
            UpdateMainTimeDisplay();
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. 이벤트(e)에서 '진짜' 새로 선택된 항목을 가져옵니다.
            TaskItem newSelectedItem = null;
            if (e.AddedItems.Count > 0)
            {
                newSelectedItem = e.AddedItems[0] as TaskItem;
            }

            // 2. '진짜' 항목을 RecalculateAllTotals에 전달합니다.
            RecalculateAllTotals(newSelectedItem);

            RenderTimeTable();
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            if (_settings != null && _settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try
                {
                    var newBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    newBrush.Freeze();
                    _taskBrushCache[taskName] = newBrush;
                    return newBrush;
                }
                catch { /* ignore */ }
            }

            return DefaultGrayBrush;
        }

        // 621번째 줄의 RenderTimeTable 메서드 전체를 아래 코드로 대체해주세요.

        // 👈 [ 2단계 오류 수정: 이 메서드 전체를 교체 ]
        private void RenderTimeTable()
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // 이전 블록 삭제
            var bordersToRemove = SelectionCanvas.Children.OfType<Border>()
                                         .Where(b => b.Tag is TimeLogEntry)
                                         .ToList();
            foreach (var border in bordersToRemove) SelectionCanvas.Children.Remove(border);

            // [!!!] 배경 그리기가 삭제된 상태 (정상)

            // 로그 블록 그리기
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

                        // --- [수정된 좌표 계산 로직] ---
                        // ❗️ [수정] 모든 변수가 클래스 필드(_(언더스코어))를 사용하도록 변경

                        // 1. 10분당 픽셀 수 (실제 그리기 영역 '_blockWidth' 기준)
                        double pixelsPerMinuteInBlock = _blockWidth / 10.0; // 👈 _(언더스코어) 사용

                        // 2. 현재 블록이 속한 10분 단위 셀 인덱스 (0~5)
                        int cellIndex = (int)Math.Floor(blockStart.Minute / 10.0);

                        // 3. 해당 셀 안에서의 분 (0.0 ~ 9.99...)
                        double minuteInCell = blockStart.TimeOfDay.TotalMinutes % 10.0;

                        // 4. 해당 셀의 '그리기 영역(blockContainer)'이 시작되는 X좌표
                        double cellDrawableAreaStart = _hourLabelWidth                // 👈 _(언더스코어) 사용
                                                     + (cellIndex * _cellWidth)     // 👈 _(언더스코어) 사용
                                                     + _borderLeftThickness         // 👈 _(언더스코어) 사용
                                                     + _horizontalMargin;           // 👈 _(언더스코어) 사용

                        // 5. 셀 내부 '그리기 영역' 안에서의 픽셀 오프셋
                        double offsetInCell = minuteInCell * pixelsPerMinuteInBlock;

                        // 6. 최종 Left 좌표
                        double leftOffset = cellDrawableAreaStart + offsetInCell;

                        // 7. 최종 Width (지속 시간(분) * 분당 픽셀)
                        double barWidth = blockDuration.TotalMinutes * pixelsPerMinuteInBlock;

                        // 8. Top 좌표 (기존 로직 유지)
                        double topOffset = Math.Floor(blockStart.TimeOfDay.TotalHours) * _rowHeight + _verticalMargin; // 👈 _(언더스코어) 사용
                        // --- [계산 로직 종료] ---


                        if (barWidth <= 0 || double.IsNaN(topOffset) || double.IsNaN(leftOffset))
                        {
                            Debug.WriteLine($"Skipping invalid chunk: {logEntry.TaskText} at {blockStart}");
                            blockIterator = blockEnd;
                            continue;
                        }

                        var coloredBar = new Border
                        {
                            Width = barWidth,
                            Height = _blockHeight, // 👈 _(언더스코어) 사용
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

                        blockIterator = blockEnd;
                    }

                    currentChunkStartTime = currentChunkEndTime;
                }
            }

            // Canvas 높이 보정
            SelectionCanvas.Height = (24 * _rowHeight);

            if (_selectionBox != null) Panel.SetZIndex(_selectionBox, 100);

            Debug.WriteLine($"RenderTimeTable: Done. SelectionCanvas.Children={SelectionCanvas.Children.Count}, Height={SelectionCanvas.Height}");
        }

        // 👈 [ 2단계 오류 수정: 이 메서드 전체를 교체 ]
        /// <summary>
        /// 앱 시작 시 '최초 1회'만 호출되어 타임라인의 배경 눈금을 그립니다.
        /// </summary>
        private void InitializeTimeTableBackground()
        {
            // 배경 그리기
            TimeTableContainer.Children.Clear();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, _verticalMargin, 0, _verticalMargin) // 👈 _(언더스코어) 사용
                };

                // ▼▼▼ [DPI 수정] 측정할 수 있도록 0시와 1시 행에 Name을 부여합니다. ▼▼▼
                if (hour == 0) hourRowPanel.Name = "HourRow_0";
                if (hour == 1) hourRowPanel.Name = "HourRow_1";
                // ▲▲▲ [DPI 수정 완료] ▲▲▲

                var hourLabel = new TextBlock
                {
                    // ...
                    Text = $"{hour:00}",
                    Width = _hourLabelWidth,   // 👈 _(언더스코어) 사용
                    Height = _blockHeight,     // 👈 _(언더스코어) 사용
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
                        Width = _blockWidth,   // 👈 _(언더스코어) 사용
                        Height = _blockHeight, // 👈 _(언더스코어) 사용
                        Background = (Brush)FindResource("SecondaryBackgroundBrush"),
                        Margin = new Thickness(_horizontalMargin, 0, _horizontalMargin, 0) // 👈 _(언더스코어) 사용
                    };

                    var blockWithBorder = new Border
                    {
                        BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 1),
                        Child = blockContainer
                    };

                    hourRowPanel.Children.Add(blockWithBorder);
                }

                TimeTableContainer.Children.Add(hourRowPanel);
            }

            // (디버그) 한셀 계산값 출력
            Debug.WriteLine($"InitializeTimeTableBackground: blockWidth={_blockWidth}, cellWidth={_cellWidth}, rowHeight={_rowHeight}, hourLabelWidth={_hourLabelWidth}");
        }


        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_settings == null) return;
        }

        private void UpdateCoinDisplay()
        {
        }

        private async void GoToAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            // await _parentWindow.NavigateToPage("Avatar"); // 🗑️ [삭제] 이동 기능 막음
        }

        private void UpdateDateAndUI()
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd");

            CurrentDayDisplay.Text = _currentDateForTimeline.ToString("ddd");

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

        // (약 924줄 근처)
        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // ▼▼▼ [수정] VM을 먼저 가져옵니다. ▼▼▼
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
                    // ▼▼▼ [수정] Margin 대신 Canvas.GetLeft/Top 사용 ▼▼▼
                    var logRect = new Rect(Canvas.GetLeft(child), Canvas.GetTop(child), child.ActualWidth, child.ActualHeight);
                    // ▲▲▲
                    if (selectionRect.IntersectsWith(logRect))
                    {
                        selectedLogs.Add(logEntry);
                    }
                }
            }

            if (!selectedLogs.Any()) return;

            var distinctLogs = selectedLogs.Distinct().OrderBy(l => l.StartTime).ToList();

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---
            // [롤백] '하나씩' 수정하는 팝업 대신,
            // [롤백] '한 번에' 수정하는 'BulkEditLogsWindow' 팝업을 띄웁니다.
            // (이 로직은 원본 DashboardPage.xaml.cs 파일에 있던 로직입니다.)

            var bulkEditWindow = new BulkEditLogsWindow(distinctLogs, TaskItems) { Owner = Window.GetWindow(this) };

            if (bulkEditWindow.ShowDialog() != true) return;

            // ▼▼▼ [핵심 수정] VM의 메서드를 호출하도록 변경 ▼▼▼
            if (bulkEditWindow.Result == BulkEditResult.ChangeTask)
            {
                string newText = bulkEditWindow.SelectedTask.Text;
                foreach (var log in distinctLogs)
                {
                    // 수정된 새 객체 생성 (기존 객체 복사)
                    var updatedLog = new TimeLogEntry
                    {
                        StartTime = log.StartTime,
                        EndTime = log.EndTime,
                        TaskText = newText, // 과목만 변경
                        FocusScore = log.FocusScore,
                        BreakActivities = log.BreakActivities
                    };
                    vm.UpdateLog(log, updatedLog); // VM에 수정 요청 (오류 983 수정)
                }
            }
            else if (bulkEditWindow.Result == BulkEditResult.Delete)
            {
                foreach (var log in distinctLogs)
                {
                    vm.DeleteLog(log); // VM에 삭제 요청 (오류 983 수정)
                }
            }
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            // [삭제] VM이 직접 저장하므로 이 줄 삭제
            // SaveTimeLogs(); 

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

        // 🎯 WorkPartner/DashboardPage.xaml.cs 파일의 맨 끝 (클래스 닫는 괄호 '}' 바로 전)에 추가하세요.

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
                h = s = 0; // 회색조
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
                else // max == b
                {
                    h = (r - g) / delta + 4.0;
                }

                h /= 6.0; // 0-1 범위로 정규화
            }

            return (h * 360.0, s, l); // H(0-360), S(0-1), L(0-1)
        }

        // 파일: ddmhyang/workpartner2/WorkPartner2-6/WorkPartner/DashboardPage.xaml.cs

        // [수정 후 ✅]
        private void ChangeTaskColor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: TaskItem selectedTask }) return;

            TaskListBox.SelectedItem = selectedTask;

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---

            var palette = new HslColorPicker();

            // (기존 색상 로드 로직...)
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

            // 1. StackPanel 대신 DockPanel 사용
            var panel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = true // 👈 (중요)
            };

            // 2. '확인' 버튼을 '아래(Bottom)'에 고정
            DockPanel.SetDock(confirmButton, Dock.Bottom);
            panel.Children.Add(confirmButton);

            // 3. 'palette'를 마지막에 추가
            panel.Children.Add(palette);
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            var window = new Window
            {
                Title = "과목 색상 변경",
                Content = panel,
                Width = 280,  // 👈 (수정) 사용자가 지정한 너비
                Height = 380, // 👈 (수정) 사용자가 지정한 높이
                              // SizeToContent = SizeToContent.WidthAndHeight, // 👈 (삭제)
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

            // 5. 'ShowDialog()'를 호출하고, 그 결과가 true일 때만 (확인 버튼 클릭 시) 저장
            if (window.ShowDialog() == true)
            {
                var newColor = palette.SelectedColor;
                _settings.TaskColors[selectedTask.Text] = newColor.ToString();
                selectedTask.ColorBrush = new SolidColorBrush(newColor);
                DataManager.SaveSettings(_settings); // (DataManager.cs가 static이므로)

                RenderTimeTable();
            }
            // --- ▲▲▲ [수정된 부분 끝] ---

            e.Handled = true;
        }

        // (약 1137줄 근처)
        private void DashboardPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.DashboardViewModel oldVm)
            {
                oldVm.TimeUpdated -= OnViewModelTimeUpdated;
                oldVm.TimerStoppedAndSaved -= OnViewModelTimerStopped;
                oldVm.CurrentTaskChanged -= OnViewModelTaskChanged;
                oldVm.PropertyChanged -= OnViewModelPropertyChanged; // ◀◀ [이 줄 추가]
            }
            if (e.NewValue is ViewModels.DashboardViewModel newVm)
            {
                newVm.TimeUpdated += OnViewModelTimeUpdated;
                newVm.TimerStoppedAndSaved += OnViewModelTimerStopped;
                newVm.CurrentTaskChanged += OnViewModelTaskChanged;
                newVm.PropertyChanged += OnViewModelPropertyChanged; // ◀◀ [이 줄 추가]

            // ▼▼▼ [이 두 줄을 여기에 추가!] ▼▼▼
            // 1. '화면'의 리스트가 '두뇌'의 리스트를 가리키게 합니다.
            this.TaskItems = newVm.TaskItems;
            // 2. 'TaskListBox'가 '두뇌'의 리스트를 직접 바라보게 합니다.
            TaskListBox.ItemsSource = this.TaskItems;
            // ▲▲▲ [여기까지 추가] ▲▲▲
            }
        }
        private void OnViewModelTimerStopped(object sender, EventArgs e)
        {
            // ViewModel이 방금 새 로그를 저장했으므로 (VM.List가 변경됨)
            // Page는 VM의 총계를 다시 계산하고 타임라인을 다시 그리기만 하면 됨.

            // [중요] LoadTimeLogsAsync()를 호출하면 안 됨! (객체 참조가 꼬임)

            Dispatcher.Invoke(() =>
            {
                // await LoadTimeLogsAsync();    // 1. ◀◀ [이 줄 삭제 또는 주석 처리]
                RecalculateAllTotals(); // 2. 총 시간 다시 계산 (VM 리스트 사용)
                RenderTimeTable();      // 3. 타임라인 다시 그리기 (VM 리스트 사용)
            });
        }

        // 파일: DashboardPage.xaml.cs (약 1157줄)
        //
        // ▼▼▼ OnViewModelTimeUpdated 메서드 전체를 아래 코드로 교체하세요 ▼▼▼

        private void OnViewModelTimeUpdated(string newTime)
        {
            // 1. 미니 타이머는 "항상" 오늘의 실시간 데이터로 업데이트합니다.
            if (_miniTimer != null && _miniTimer.IsVisible)
            {
                LoadSettings();
                _miniTimer.UpdateData(_settings, CurrentTaskDisplay.Text, newTime);
            }

            // ▼▼▼ [!!! 여기가 핵심 수정입니다 !!!] ▼▼▼

            // 3. '두뇌'(VM)의 실시간 데이터를 '화면'(Page)의 TotalTime 속성에 동기화합니다.
            // [수정] 이 작업은 '오늘 날짜'를 보고 있을 때만 수행해야 합니다.
            // (다른 날짜를 볼 때는 RecalculateAllTotals가 TotalTime을 책임져야 함)
            if (_currentDateForTimeline.Date != DateTime.Today.Date)
            {
                return; // 👈 오늘 날짜가 아니면, 아래 실시간 동기화 로직(데이터 오염)을 실행하지 않음
            }

            // 3-1. (오늘 날짜일 때만 실행됨)
            // (중요) '두뇌'(ViewModel)의 리스트와 '화면'(Page)의 리스트를 동기화합니다.
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                // '두뇌'의 실시간 업데이트된 과목 리스트를 순회합니다.
                foreach (var vmTask in vm.TaskItems)
                {
                    // '화면'의 리스트(TaskItems)에서 일치하는 과목을 찾습니다.
                    var pageTask = this.TaskItems.FirstOrDefault(t => t.Text == vmTask.Text);

                    // '화면'에 과목이 존재하고, '두뇌'의 시간과 다르다면
                    if (pageTask != null && pageTask.TotalTime != vmTask.TotalTime)
                    {
                        // '화면'의 시간을 '두뇌'의 시간으로 덮어씁니다.
                        // (TaskItem.cs가 이 변경을 감지하고 UI를 갱신합니다)
                        pageTask.TotalTime = vmTask.TotalTime;
                    }
                }
            }
            // ▲▲▲ [!!! 여기까지 수정 !!!] ▲▲▲
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 오늘 날짜가 아니면 VM 업데이트 무시
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            // TotalTimeTodayDisplayText 속성이 변경될 때만 하단 텍스트 업데이트
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
            // 1. 메인 과목 텍스트(CurrentTaskDisplay)는 "항상" ViewModel의 값을 따릅니다.
            //    (이 값은 OnViewModelTimeUpdated가 참조하여 미니 타이머로 전달됩니다)
            Dispatcher.Invoke(() =>
            {
                CurrentTaskDisplay.Text = newTaskName;
            });

            // 2. 메인 대시보드 UI(TaskListBox)는 "오늘 날짜를 볼 때만" 동기화합니다.
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            Dispatcher.Invoke(() =>
            {
                // ✨ [추가] 
                // AI가 과목을 변경했을 때, TaskListBox의 UI 선택도 강제로 변경합니다.
                var foundTask = TaskItems.FirstOrDefault(t => t.Text == newTaskName);
                if (foundTask != null && TaskListBox.SelectedItem != foundTask)
                {
                    // 이 구문은 SelectionChanged 이벤트를 발생시키지만,
                    // 1단계에서 UpdateMainTimeDisplay의 충돌 코드를 제거했으므로
                    // 더 이상 문제를 일으키지 않습니다.
                    TaskListBox.SelectedItem = foundTask;
                }
            });
        }
        #endregion

        private void EvaluateDayButton_Click(object sender, RoutedEventArgs e)
        {
            // 🗑️ [삭제] 점수 평가 창 띄우는 로직 전체 제거
            MessageBox.Show("이 기능은 더 이상 사용되지 않습니다.", "알림");
        }

        private void LoadUserImage()
        {
            if (UserProfileImage == null) return;

            // 설정에서 경로 가져오기
            string imagePath = _settings.UserImagePath;

            // GifHelper에게 "이 이미지 컨트롤에, 이 파일을 재생해줘"라고 명령
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
                // 경로 저장
                _settings.UserImagePath = openFileDialog.FileName;

                // 파일에 즉시 쓰기 (가장 중요!)
                DataManager.SaveSettings(_settings);

                // 화면 갱신
                LoadUserImage();
            }
        }
    }
}