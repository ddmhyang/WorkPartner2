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
using System.Windows.Threading;
using WorkPartner.AI;
using System.ComponentModel; // PropertyChangedEventArgs 클래스를 사용하기 위해 필요

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

        private readonly Dictionary<string, SolidColorBrush> _taskBrushCache = new();
        private static readonly SolidColorBrush DefaultGrayBrush = new SolidColorBrush(Colors.Gray);
        private static readonly SolidColorBrush BlockBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        private static readonly SolidColorBrush BlockBorderBrush = Brushes.White;

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

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
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

        #region 데이터 저장 / 불러오기
        public void LoadSettings() { _settings = DataManager.LoadSettings(); }
        private void SaveSettings() { DataManager.SaveSettings(_settings); }

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
            DataManager.SaveTodos(TodoItems);
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
            DataManager.SaveTimeLogs(TimeLogEntries);
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

            // ✨ [진짜 수정] ColorPalette(UserControl)를 호스팅할 새 Window를 만듭니다.
            var palette = new ColorPalette();
            var window = new Window
            {
                Title = "과목 색상 선택",
                Content = palette,
                Width = 260, // 팔레트 크기에 맞게 조절
                Height = 180,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            // ✨ [진짜 수정] 팔레트에서 색을 선택하면(ColorChanged) 창을 닫고 저장합니다.
            palette.ColorChanged += (s, newColor) =>
            {
                _settings.TaskColors[newTask.Text] = newColor.ToString();
                SaveSettings();
                window.Close(); // 색상 선택 시 창 닫기
            };

            window.ShowDialog(); // ✨ Window가 ShowDialog()를 호출

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
                _taskBrushCache.Remove(oldName);
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
                _taskBrushCache.Remove(taskNameToDelete);
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

        /// <summary>
        /// ✨ [REVISED] 메인 타이머와 과목 이름, 하단 총 시간을 모두 업데이트합니다.
        /// </summary>
private void UpdateMainTimeDisplay()
        {
            TaskItem selectedTask = TaskListBox.SelectedItem as TaskItem;
            if (selectedTask == null && TaskItems.Any())
            {
                selectedTask = TaskItems.FirstOrDefault();
                // ViewModel을 사용하지 않으므로 직접 선택 항목을 설정합니다.
                if (TaskListBox.SelectedItem == null)
                {
                    TaskListBox.SelectedItem = selectedTask;
                }
            }

            TimeSpan timeToShow = TimeSpan.Zero;
            if (selectedTask != null)
            {
                // 오늘 날짜의 해당 과목 로그 시간을 합산합니다.
                var logsForSelectedDateAndTask = TimeLogEntries
                    .Where(log => log.StartTime.Date == _currentDateForTimeline.Date && log.TaskText == selectedTask.Text);
                timeToShow = new TimeSpan(logsForSelectedDateAndTask.Sum(log => log.Duration.Ticks));
            }
            
            // ✨ [추가] 미니 타이머로 보낼 문자열을 미리 준비합니다.
            string timeString = timeToShow.ToString(@"hh\:mm\:ss");
            string taskString = selectedTask != null ? selectedTask.Text : "과목을 선택하세요";

            // 메인 타이머 업데이트
            MainTimeDisplay.Text = timeString;

            // 현재 과목 이름 업데이트
            CurrentTaskDisplay.Text = taskString;

            // 하단 총 학습 시간 업데이트
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();
            var totalTimeToday = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));
            SelectedTaskTotalTimeDisplay.Text = $"이날의 총 학습 시간: {(int)totalTimeToday.TotalHours}시간 {totalTimeToday.Minutes}분";
            
            // ✨ [추가] 미니 타이머가 켜져있다면, 모든 정보를 새 UpdateData 메서드로 전달합니다.
            if (_miniTimer != null && _miniTimer.IsVisible)
            {
                // _settings 필드는 LoadAllDataAsync()에서 이미 로드되었습니다.
                _miniTimer.UpdateData(_settings, taskString, timeString);
            }
        }

        /// <summary>
        /// ✨ [REVISED] 과목별 시간만 계산하고, UI 업데이트는 UpdateMainTimeDisplay에 맡깁니다.
        /// </summary>
        private void RecalculateAllTotals()
        {
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            foreach (var task in TaskItems)
            {
                var taskLogs = todayLogs.Where(log => log.TaskText == task.Text);
                task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
            }

            UpdateMainTimeDisplay();

            if (TaskListBox != null)
            {
                TaskListBox.Items.Refresh();
            }
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMainTimeDisplay();
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

        private void RenderTimeTable()
        {
            // --- 이전 기록 블록 삭제 ---
            // 이전에 그린 '기록 블록'(Tag가 TimeLogEntry인 Border)만 찾아서 삭제합니다.
            var bordersToRemove = SelectionCanvas.Children.OfType<Border>()
                                             .Where(b => b.Tag is TimeLogEntry)
                                             .ToList();
            // ✨ 디버깅 로그: 삭제할 블록 수 확인
            Debug.WriteLine($"RenderTimeTable: Removing {bordersToRemove.Count} previous log blocks.");
            foreach (var border in bordersToRemove)
            {
                SelectionCanvas.Children.Remove(border);
            }

            // --- 배경 눈금 그리기 (StackPanel 방식, WorkPartner2-3 복원 버전) ---
            // 배경은 매번 새로 그립니다 (Clear 후 다시 그림).
            TimeTableContainer.Children.Clear();
            double blockWidth = 35, blockHeight = 17, hourLabelWidth = 30;
            double verticalMargin = 1; // 행 상하 마진
            double horizontalMargin = 1; // 블록 좌우 마진
            double rowHeight = blockHeight + (verticalMargin * 2); // 실제 행 높이 (19)
            double cellWidth = blockWidth + (horizontalMargin * 2); // 실제 셀 너비 (37)

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, verticalMargin, 0, verticalMargin) };
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
                    var blockContainer = new Grid { Width = blockWidth, Height = blockHeight, Background = BlockBackgroundBrush, Margin = new Thickness(horizontalMargin, 0, horizontalMargin, 0) };
                    // BorderThickness 수정: 이전 버전 실수 보완 (Bottom=1)
                    var blockWithBorder = new Border { BorderBrush = BlockBorderBrush, BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 1), Child = blockContainer };
                    hourRowPanel.Children.Add(blockWithBorder);
                }
                TimeTableContainer.Children.Add(hourRowPanel);
            }
            // ✨ 디버깅 로그: 배경 그리기 완료 확인
            Debug.WriteLine("RenderTimeTable: Background grid rendered.");


            // --- 시간 기록 블록 그리기 (✨ [수정됨] 줄바꿈 로직 적용) ---
            var logsForSelectedDate = TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date)
                .OrderBy(l => l.StartTime)
                .ToList();

            // ✨ 디버깅 로그: 해당 날짜의 로그 데이터 개수 확인
            Debug.WriteLine($"RenderTimeTable: Processing {logsForSelectedDate.Count} logs for date {_currentDateForTimeline.Date}.");

            if (!logsForSelectedDate.Any())
            {
                Debug.WriteLine("RenderTimeTable: No logs found for this date. Skipping block rendering.");
            }

            foreach (var logEntry in logsForSelectedDate)
            {
                // ✨ [수정 시작] 
                // 이 로직은 하나의 로그를 시간대별로 "조각(Chunk)"내어 그립니다.

                DateTime currentChunkStartTime = logEntry.StartTime;
                DateTime logEndTime = logEntry.EndTime;

                // 로그 종료 시간이 24:00를 넘어가면 23:59:59로 잘라냅니다.
                if (logEndTime.Date > currentChunkStartTime.Date)
                {
                    logEndTime = currentChunkStartTime.Date.AddDays(1).AddTicks(-1);
                }

                while (currentChunkStartTime < logEndTime)
                {
                    // 1. 현재 조각의 끝 시간을 계산합니다.
                    //    (다음 정시) 또는 (로그의 실제 종료 시간) 중 더 빠른 시간입니다.
                    DateTime endOfCurrentHour = currentChunkStartTime.Date.AddHours(currentChunkStartTime.Hour + 1);
                    DateTime currentChunkEndTime = (logEndTime < endOfCurrentHour) ? logEndTime : endOfCurrentHour;

                    TimeSpan chunkStart = currentChunkStartTime.TimeOfDay;
                    TimeSpan chunkEnd = currentChunkEndTime.TimeOfDay;
                    TimeSpan duration = chunkEnd - chunkStart;

                    // 2. 유효성 검사 1: Duration
                    if (duration.TotalSeconds <= 0)
                    {
                        // (예: 10:00에 시작하는 다음 조각이 10:00에 끝나면) 루프 종료
                        break;
                    }

                    // 3. 기존 계산 로직을 "조각"에 대해 그대로 적용합니다.
                    var topOffset = Math.Floor(chunkStart.TotalHours) * rowHeight;
                    var leftOffset = hourLabelWidth + (chunkStart.Minutes / 10.0) * cellWidth;

                    double calculatedWidth = (duration.TotalMinutes / 10.0) * cellWidth;
                    var barWidth = calculatedWidth - (horizontalMargin * 2);

                    // 4. 유효성 검사 (기존 코드 유지)
                    if (barWidth <= 0)
                    {
                        Debug.WriteLine($"--> Skipping log chunk (width <= 0): {logEntry.TaskText} at {currentChunkStartTime}, Duration={duration}, Calculated Width before margin: {calculatedWidth:F2}, Final Width: {barWidth:F2}");
                        currentChunkStartTime = currentChunkEndTime; // 다음 조각으로 이동
                        continue;
                    }
                    if (double.IsNaN(topOffset) || double.IsNaN(leftOffset) || double.IsInfinity(topOffset) || double.IsInfinity(leftOffset))
                    {
                        Debug.WriteLine($"--> Skipping log chunk (Invalid coordinates): {logEntry.TaskText} at {currentChunkStartTime}");
                        currentChunkStartTime = currentChunkEndTime; // 다음 조각으로 이동
                        continue;
                    }

                    // 5. 조각(Border) 생성
                    var coloredBar = new Border
                    {
                        Width = barWidth,
                        Height = blockHeight, // 높이는 17
                        Background = GetColorForTask(logEntry.TaskText),
                        CornerRadius = new CornerRadius(2),
                        ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}\n\n클릭하여 수정 또는 삭제" },
                        // ✨ [중요] Tag에는 원본 로그(logEntry)를 넣어야 수정/삭제가 정상 작동합니다.
                        Tag = logEntry,
                        Cursor = Cursors.Hand
                    };
                    coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;

                    // 6. Canvas에 배치
                    Canvas.SetLeft(coloredBar, leftOffset);
                    Canvas.SetTop(coloredBar, topOffset);
                    Panel.SetZIndex(coloredBar, 1); // Z-Index 설정
                    SelectionCanvas.Children.Add(coloredBar); // Canvas에 추가

                    // 7. 다음 조각의 시작 시간을 설정
                    currentChunkStartTime = currentChunkEndTime;
                }
                // ✨ [수정 종료] 
            }

            // ✨ [복원 및 필수] Canvas 높이 설정 코드를 다시 추가합니다.
            double requiredCanvasHeight = 24 * rowHeight;
            SelectionCanvas.Height = requiredCanvasHeight;

            // 선택 상자(_selectionBox) Z-Index 설정
            if (_selectionBox != null)
            {
                Panel.SetZIndex(_selectionBox, 100);
            }

            // ✨ 디버깅 로그: 최종 Canvas 자식 수 및 높이 확인
            Debug.WriteLine($"RenderTimeTable: Finished. Total elements in SelectionCanvas: {SelectionCanvas.Children.Count}. Canvas Height: {SelectionCanvas.Height}.");
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

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
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
                    var logRect = new Rect(child.Margin.Left, child.Margin.Top, child.ActualWidth, child.ActualHeight);
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

            // ✨ [진짜 수정] ColorPalette(UserControl)를 호스팅할 새 Window를 만듭니다.
            var palette = new ColorPalette();

            // (이미 저장된 색이 있으면 팔레트에 설정)
            if (_settings.TaskColors.TryGetValue(selectedTask.Text, out var hex))
            {
                try { palette.SelectedColor = (Color)ColorConverter.ConvertFromString(hex); }
                catch { /* ignore invalid hex */ }
            }

            var window = new Window
            {
                Title = "과목 색상 변경",
                Content = palette,
                Width = 260, // 팔레트 크기에 맞게 조절
                Height = 180,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            // ✨ [진짜 수정] 팔레트에서 색을 선택하면(ColorChanged) 창을 닫고 저장합니다.
            palette.ColorChanged += (s, newColor) =>
            {
                _settings.TaskColors[selectedTask.Text] = newColor.ToString();
                selectedTask.ColorBrush = new SolidColorBrush(newColor);
                DataManager.SaveSettings(_settings); // (DataManager.cs가 static이므로)

                RenderTimeTable();
                window.Close(); // 색상 선택 시 창 닫기
            };

            window.ShowDialog(); // ✨ Window가 ShowDialog()를 호출

            e.Handled = true;
        }

        private void DashboardPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.DashboardViewModel oldVm)
            {
                oldVm.TimeUpdated -= OnViewModelTimeUpdated;
            }
            if (e.NewValue is ViewModels.DashboardViewModel newVm)
            {
                newVm.TimeUpdated += OnViewModelTimeUpdated;
            }
        }

        private void OnViewModelTimeUpdated(string newTime)
        {
            // [추가] 오늘 날짜가 아니면 ViewModel의 실시간 업데이트를 무시합니다.
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            // 이 기능은 이제 ViewModel이 아닌 코드 비하인드에서 직접 관리하므로,
            // 실시간 타이머가 필요할 경우 MainTimeDisplay.Text를 여기서 업데이트 할 수도 있습니다.
            // 하지만 현재는 날짜별 기록 표시에 집중하므로 비워둡니다.
            // _miniTimer?.UpdateTime(newTime);

            // [수정] 위 주석을 무시하고, 여기서 UI를 직접 업데이트합니다.
            Dispatcher.Invoke(() =>
            {
                // 1. 메인 타이머 업데이트
                MainTimeDisplay.Text = newTime;

                // 2. 미니 타이머 업데이트
                if (_miniTimer != null && _miniTimer.IsVisible)
                {
                    if (_settings == null) LoadSettings();
                    // CurrentTaskDisplay.Text는 OnViewModelTaskChanged가 업데이트합니다.
                    _miniTimer.UpdateData(_settings, CurrentTaskDisplay.Text, newTime);
                }
            });
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

        // [추가] ViewModel에서 과목이 (AI 태그 등으로) 변경될 때
        private void OnViewModelTaskChanged(string newTaskName)
        {
            // 오늘 날짜가 아니면 VM 업데이트 무시
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            Dispatcher.Invoke(() =>
            {
                // 1. 상단 과목명(CurrentTaskDisplay) 업데이트
                CurrentTaskDisplay.Text = newTaskName;

                // 2. (중요) 과목 목록(TaskListBox)의 선택도 VM과 동기화
                var foundTask = TaskItems.FirstOrDefault(t => t.Text == newTaskName);
                if (foundTask != null && TaskListBox.SelectedItem != foundTask)
                {
                    TaskListBox.SelectedItem = foundTask;
                }
            });
        }
        #endregion
    }
}