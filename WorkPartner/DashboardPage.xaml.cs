using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WorkPartner.AI;
using System.Threading.Tasks;


namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언
        private MainWindow _mainWindow; // 부모 윈도우를 저장할 변수
        private readonly string _tasksFilePath = DataManager.TasksFilePath;
        private readonly string _todosFilePath = DataManager.TodosFilePath;
        private readonly string _timeLogFilePath = DataManager.TimeLogFilePath;
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; }
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; }
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<string> SuggestedTags { get; set; }
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch;
        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs;
        private TimeSpan _selectedTaskTotalTimeFromLogs;
        private MemoWindow _memoWindow;
        private AppSettings _settings;
        private bool _isFocusModeActive = false;
        private DateTime _lastNagTime;
        private TodoItem _lastAddedTodo;
        private TimeLogEntry _lastUnratedSession;
        private MiniTimerWindow _miniTimer;

        private readonly PredictionService _predictionService;
        private readonly Dictionary<string, MediaPlayer> _soundPlayers = new Dictionary<string, MediaPlayer>();
        private DateTime _currentDateForTimeline = DateTime.Today;
        private DateTime _lastSuggestionTime;

        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;

        // 1. MainWindow를 저장할 변수 선언
        private MainWindow _parentWindow;
        // DashboardPage.xaml.cs -> #region 변수 선언

        private readonly string _memosFilePath = DataManager.MemosFilePath;
        public ObservableCollection<MemoItem> AllMemos { get; set; }
        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            InitializeData();
            LoadAllData();

            _predictionService = new PredictionService();
            _lastSuggestionTime = DateTime.MinValue;
            DataManager.SettingsUpdated += OnSettingsUpdated;

            InitializeSoundPlayers();

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
        }



        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>();
            TaskListBox.ItemsSource = TaskItems;

            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            // SuggestedTagsItemsControl is not in the new XAML
            // SuggestedTagsItemsControl.ItemsSource = SuggestedTags;
            AllMemos = new ObservableCollection<MemoItem>();
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_settings != null && _settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                }
                catch { /* 잘못된 색상 코드 무시 */ }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        #region 데이터 저장 / 불러오기


        public void LoadSettings() { _settings = DataManager.LoadSettings(); }
        private void OnSettingsUpdated() { _settings = DataManager.LoadSettings(); }
        private void SaveSettings() { DataManager.SaveSettingsAndNotify(_settings); }

        private void LoadTasks()
        {
            if (!File.Exists(_tasksFilePath)) return;
            var json = File.ReadAllText(_tasksFilePath);
            var loadedTasks = JsonSerializer.Deserialize<ObservableCollection<TaskItem>>(json) ?? new ObservableCollection<TaskItem>();
            TaskItems.Clear();
            foreach (var task in loadedTasks)
            {
                TaskItems.Add(task);
            }
        }
        private void SaveTasks()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TaskItems, options);
            File.WriteAllText(_tasksFilePath, json);
        }

        private void LoadTodos()
        {
            if (!File.Exists(_todosFilePath)) return;
            var json = File.ReadAllText(_todosFilePath);
            var loadedTodos = JsonSerializer.Deserialize<ObservableCollection<TodoItem>>(json) ?? new ObservableCollection<TodoItem>();
            TodoItems.Clear();
            foreach (var todo in loadedTodos)
            {
                TodoItems.Add(todo);
            }
            FilterTodos();
        }
        private void SaveTodos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TodoItems, options);
            File.WriteAllText(_todosFilePath, json);
        }

        private void LoadTimeLogs()
        {
            if (!File.Exists(_timeLogFilePath)) return;
            var json = File.ReadAllText(_timeLogFilePath);
            var loadedLogs = JsonSerializer.Deserialize<ObservableCollection<TimeLogEntry>>(json) ?? new ObservableCollection<TimeLogEntry>();
            TimeLogEntries.Clear();
            foreach (var log in loadedLogs)
            {
                TimeLogEntries.Add(log);
            }
        }
        private void SaveTimeLogs()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TimeLogEntries, options);
            File.WriteAllText(_timeLogFilePath, json);
        }
        #endregion



        #region UI 이벤트 핸들러
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd ddd");

            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                LoadAllData();
            }
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = (UIElement)((Control)sender).Parent;
                parent.RaiseEvent(eventArg);
            }
        }

        private void TodoTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var treeViewItem = FindVisualParent<TreeViewItem>(source);
                treeViewItem?.Focus();
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            var parent = parentObject as T;
            return parent ?? FindVisualParent<T>(parentObject);
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string newTaskText = TaskInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newTaskText))
            {
                if (TaskItems.Any(t => t.Text.Equals(newTaskText, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var newTask = new TaskItem { Text = newTaskText };
                TaskItems.Add(newTask);

                var colorPicker = new ColorPickerWindow
                {
                    Owner = Window.GetWindow(this)
                };
                if (colorPicker.ShowDialog() == true)
                {
                    _settings.TaskColors[newTask.Text] = colorPicker.SelectedColor.ToString();
                    SaveSettings();
                }

                TaskInput.Clear();
                SaveTasks();
                RenderTimeTable();
            }
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text)
                {
                    Owner = Window.GetWindow(this)
                };

                if (inputWindow.ShowDialog() == true)
                {
                    string newName = inputWindow.ResponseText.Trim();
                    string oldName = selectedTask.Text;
                    if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
                    if (TaskItems.Any(t => t.Text.Equals(newName, StringComparison.OrdinalIgnoreCase) && t != selectedTask))
                    {
                        MessageBox.Show("이미 존재하는 과목 이름입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Update task name everywhere
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

                    selectedTask.Text = newName; // This should update the UI via binding

                    SaveTasks();
                    SaveTimeLogs();
                    SaveSettings();

                    // Explicitly refresh the ListBox to ensure the UI updates
                    TaskListBox.Items.Refresh();
                    RenderTimeTable();
                    UpdateSelectedTaskTotalTimeDisplay(); // Update total time display as well
                }
            }
            else
            {
                MessageBox.Show("수정할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                if (MessageBox.Show($"'{selectedTask.Text}' 과목을 삭제하시겠습니까?\n관련된 모든 학습 기록도 삭제됩니다.", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    string taskNameToDelete = selectedTask.Text;

                    TaskItems.Remove(selectedTask);

                    if (_settings.TaskColors.ContainsKey(taskNameToDelete))
                    {
                        _settings.TaskColors.Remove(taskNameToDelete);
                        SaveSettings();
                    }

                    // Remove related TimeLogEntries
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
            }
            else
            {
                MessageBox.Show("삭제할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void RateSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastUnratedSession != null && sender is Button button)
            {
                int score = int.Parse(button.Tag.ToString());
                _lastUnratedSession.FocusScore = score;
                SessionReviewPanel.Visibility = Visibility.Collapsed;
                var breakWin = new BreakActivityWindow { Owner = Window.GetWindow(this) };
                if (breakWin.ShowDialog() == true)
                {
                    _lastUnratedSession.BreakActivities = breakWin.SelectedActivities;
                }
                SaveTimeLogs();
                _lastUnratedSession = null;
            }
        }

        private void SaveTodos_Event(object sender, RoutedEventArgs e) { if (sender is CheckBox checkBox && checkBox.DataContext is TodoItem todoItem) { if (todoItem.IsCompleted && !todoItem.HasBeenRewarded) { _settings.Coins += 10; todoItem.HasBeenRewarded = true; UpdateCoinDisplay(); SaveSettings(); SoundPlayer.PlayCompleteSound(); } } SaveTodos(); }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTask = TaskListBox.SelectedItem as TaskItem;
            if (_currentWorkingTask != selectedTask)
            {
                SessionReviewPanel.Visibility = Visibility.Collapsed;
                if (_stopwatch.IsRunning)
                {
                    LogWorkSession(); _stopwatch.Reset();
                }
                _currentWorkingTask = selectedTask;
                UpdateSelectedTaskTotalTimeDisplay();
                if (_currentWorkingTask != null)
                    UpdateCharacterInfoPanel();
            }
        }

        private void TaskInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTaskButton_Click(sender, e); } }

        private void AddTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TodoInput.Text)) return;
            var newTodo = new TodoItem
            {
                Text = TodoInput.Text,
                // 현재 타임라인에 선택된 날짜를 할 일의 날짜로 지정합니다.
                Date = _currentDateForTimeline.Date
            };

            // Check if an item is selected in the TreeView to add a subtask
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
            // 할 일 목록을 다시 필터링하여 UI를 새로고침합니다.
            FilterTodos();
        }

        private void EditTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                var inputWindow = new InputWindow("할 일 수정", selectedTodo.Text)
                {
                    Owner = Window.GetWindow(this)
                };

                if (inputWindow.ShowDialog() == true)
                {
                    selectedTodo.Text = inputWindow.ResponseText;
                    SaveTodos();
                    // The TreeView should update automatically thanks to INotifyPropertyChanged on TodoItem.
                }
            }
            else
            {
                MessageBox.Show("수정할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                if (MessageBox.Show($"'{selectedTodo.Text}' 할 일을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    RemoveTodoItem(TodoItems, selectedTodo);
                    SaveTodos();
                    FilterTodos(); // Re-filter to update the view
                }
            }
            else
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTodoButton_Click(sender, e); }
        private void TodoTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SaveTodos(); }

        // '아바타 꾸미기' 버튼 클릭 이벤트 핸들러
        private void GoToClosetButton_Click(object sender, RoutedEventArgs e)
        {
            // 부모 윈도우의 페이지 이동 메서드 호출
            _mainWindow?.NavigateToPage("Avatar");
        }

        private void AddManualLogButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
            {
                if (win.NewLogEntry != null)
                {
                    TimeLogEntries.Add(win.NewLogEntry);
                }
                SaveTimeLogs();
                RecalculateAllTotals();
                RenderTimeTable();
            }
        }

        private void MemoButton_Click(object sender, RoutedEventArgs e) { if (_memoWindow == null || !_memoWindow.IsVisible) { _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) }; _memoWindow.Show(); } else { _memoWindow.Activate(); } }
        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if ((sender as FrameworkElement)?.Tag is TimeLogEntry log) { var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.IsDeleted) TimeLogEntries.Remove(log); else { log.StartTime = win.NewLogEntry.StartTime; log.EndTime = win.NewLogEntry.EndTime; log.TaskText = win.NewLogEntry.TaskText; log.FocusScore = win.NewLogEntry.FocusScore; } SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } } }
        #endregion

        #region 핵심 로직
        public void SetMiniTimerReference(MiniTimerWindow timer) { _miniTimer = timer; }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // 페이지가 현재 보이지 않으면 아무것도 하지 않고 돌아갑니다.
            if (!this.IsVisible)
            {
                return;
            }

            if (_stopwatch.IsRunning && _lastUnratedSession != null)
            {
                SessionReviewPanel.Visibility = Visibility.Collapsed;
                _lastUnratedSession = null;
            }
            HandleStopwatchMode();
            CheckFocusAndSuggest();
        }

        private void HandleStopwatchMode()
        {
            if (_settings == null)
            {
                return;
            }

            string activeProcess = ActiveWindowHelper.GetActiveProcessName();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();

            string windowTitle = ActiveWindowHelper.GetActiveWindowTitle();
            string activeTitle = string.IsNullOrEmpty(activeUrl) ? (windowTitle ?? "").ToLower() : activeUrl;

            string keywordToCheck = !string.IsNullOrEmpty(activeUrl) ? activeUrl : activeProcess;

            if (keywordToCheck == null)
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : (DateTime?)null);
                    _stopwatch.Reset();
                }
                _isPausedForIdle = false;
                UpdateCharacterInfoPanel("휴식 중");
                UpdateLiveTimeDisplays();
                return;
            }

            bool isDistraction = _settings.DistractionProcesses != null &&
                                 _settings.DistractionProcesses.Any(p => p != null && keywordToCheck.Contains(p));

            if (isDistraction)
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : (DateTime?)null);
                    _stopwatch.Reset();
                }
                _isPausedForIdle = false;
                UpdateCharacterInfoPanel("딴짓 중!");
                if (_isFocusModeActive && (DateTime.Now - _lastNagTime).TotalSeconds > _settings.FocusModeNagIntervalSeconds)
                {
                    new AlertWindow(_settings.FocusModeNagMessage).Show();
                    _lastNagTime = DateTime.Now;
                }
                UpdateLiveTimeDisplays();
                return;
            }

            bool isTrackable = _settings.WorkProcesses != null &&
                               _settings.WorkProcesses.Any(p => p != null && keywordToCheck.Contains(p));
            bool isPassive = _settings.PassiveProcesses != null &&
                             _settings.PassiveProcesses.Any(p => p != null && keywordToCheck.Contains(p));

            if (isTrackable || isPassive)
            {
                bool isCurrentlyIdle = _settings.IsIdleDetectionEnabled && !isPassive && ActiveWindowHelper.GetIdleTime().TotalSeconds > _settings.IdleTimeoutSeconds;

                if (isCurrentlyIdle)
                {
                    if (_stopwatch.IsRunning)
                    {
                        _stopwatch.Stop();
                        _isPausedForIdle = true;
                        _idleStartTime = DateTime.Now;
                    }
                    else if (_isPausedForIdle)
                    {
                        if ((DateTime.Now - _idleStartTime).TotalSeconds > IdleGraceSeconds)
                        {
                            LogWorkSession(_sessionStartTime.Add(_stopwatch.Elapsed));
                            _stopwatch.Reset();
                            _isPausedForIdle = false;
                        }
                    }
                }
                else
                {
                    if (_isPausedForIdle)
                    {
                        _isPausedForIdle = false;
                        _stopwatch.Start();
                    }
                    // ▼▼▼ 여기가 수정된 핵심 로직입니다! ▼▼▼
                    else if (!_stopwatch.IsRunning)
                    {
                        // 1. 현재 선택된 과목이 없는 경우, 목록의 첫 번째 과목을 선택합니다.
                        if (_currentWorkingTask == null && TaskItems.Any())
                        {
                            // TaskListBox의 아이템을 직접 설정하고, _currentWorkingTask 변수에도 즉시 할당합니다.
                            TaskListBox.SelectedItem = TaskItems.FirstOrDefault();
                            _currentWorkingTask = TaskListBox.SelectedItem as TaskItem;
                        }

                        // 2. 이제 _currentWorkingTask가 확실히 설정되었으므로, 스톱워치를 시작합니다.
                        if (_currentWorkingTask != null)
                        {
                            _sessionStartTime = DateTime.Now;
                            _stopwatch.Start();
                        }
                        // 만약 과목 목록이 비어있다면, _currentWorkingTask는 여전히 null이고 스톱워치는 시작되지 않습니다.
                    }
                }

                if (_isPausedForIdle) { UpdateCharacterInfoPanel("자리 비움"); }
                else if (_stopwatch.IsRunning) { UpdateCharacterInfoPanel(); }
                else { UpdateCharacterInfoPanel("휴식 중"); }
            }
            else
            {
                if (_stopwatch.IsRunning || _isPausedForIdle)
                {
                    LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : (DateTime?)null);
                    _stopwatch.Reset();
                }
                _isPausedForIdle = false;
                UpdateCharacterInfoPanel("휴식 중");
            }
            UpdateLiveTimeDisplays();
        }

        private void LogWorkSession(DateTime? endTime = null)
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
                TaskText = _currentWorkingTask.Text,
                FocusScore = 0
            };
            TimeLogEntries.Insert(0, entry);
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
            _lastUnratedSession = entry;
            SessionReviewPanel.Visibility = Visibility.Visible;
        }

        private void CheckFocusAndSuggest()
        {
            if ((DateTime.Now - _lastSuggestionTime).TotalSeconds < 60) return;
            if (!_stopwatch.IsRunning || _currentWorkingTask == null)
            {
                if (AiSuggestionTextBlock != null) AiSuggestionTextBlock.Text = "";
                return;
            }
            _lastSuggestionTime = DateTime.Now;
            var input = new ModelInput { TaskName = _currentWorkingTask.Text, DayOfWeek = (float)DateTime.Now.DayOfWeek, Hour = (float)DateTime.Now.Hour, Duration = 60 };
            float predictedScore = _predictionService.Predict(input);
            string suggestion = "";
            if (predictedScore > 0)
            {
                if (predictedScore >= 4.0)
                {
                    suggestion = "AI: 지금은 집중력이 최고조에 달할 시간입니다! 가장 어려운 과제를 처리해보세요.";
                    SoundPlayer.PlayNotificationSound();
                }
                else if (predictedScore < 2.5)
                {
                    suggestion = $"AI: 현재 '{_currentWorkingTask.Text}' 작업의 예상 집중도가 낮습니다. 5분간 휴식 후 다시 시작하는 건 어떠신가요?";
                    SoundPlayer.PlayNotificationSound();
                }
            }
            if (AiSuggestionTextBlock != null) AiSuggestionTextBlock.Text = suggestion;
        }

        private void UpdateCoinDisplay() { if (_settings != null) { CoinDisplay.Text = _settings.Coins.ToString("N0"); } }

        private void UpdateLiveTimeDisplays()
        {
            TimeSpan timeToDisplay = _totalTimeTodayFromLogs;

            // 스톱워치가 실행 중이고 '오늘' 날짜를 보고 있을 때만 현재 측정 시간을 더합니다.
            bool isViewingToday = _currentDateForTimeline.Date == DateTime.Today;

            if (_stopwatch.IsRunning && isViewingToday)
            {
                timeToDisplay += _stopwatch.Elapsed;
                _miniTimer?.SetRunningStyle();
            }
            else
            {
                _miniTimer?.SetStoppedStyle();
            }
            MainTimeDisplay.Text = timeToDisplay.ToString(@"hh\:mm\:ss");
            _miniTimer?.UpdateTime(MainTimeDisplay.Text);

            if (_currentWorkingTask != null)
            {
                TimeSpan selectedTaskTime = _selectedTaskTotalTimeFromLogs;
                // 선택된 과목 시간 표시에도 동일한 로직을 적용합니다.
                if (_stopwatch.IsRunning && isViewingToday)
                {
                    selectedTaskTime += _stopwatch.Elapsed;
                }
                SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {selectedTaskTime:hh\\:mm\\:ss}";
            }
        }

        private void UpdateSelectedTaskTotalTimeDisplay()
        {
            if (_currentWorkingTask != null)
            {
                var taskLogs = TimeLogEntries.Where(log => log.TaskText == _currentWorkingTask.Text && log.StartTime.Date == _currentDateForTimeline.Date);
                _selectedTaskTotalTimeFromLogs = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
                SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {_selectedTaskTotalTimeFromLogs:hh\\:mm\\:ss}";
            }
            else
            {
                _selectedTaskTotalTimeFromLogs = TimeSpan.Zero;
                SelectedTaskTotalTimeDisplay.Text = "선택 과목 총계: 00:00:00";
            }
        }

        private void RecalculateAllTotals()
        {
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date);
            _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));
            UpdateLiveTimeDisplays();
            UpdateSelectedTaskTotalTimeDisplay();
        }

        private void RenderTimeTable()
        {
            TimeTableContainer.Children.Clear();

            var logsForSelectedDate = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                var hourLabel = new TextBlock { Text = $"{hour:00}", Width = 30, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, Foreground = Brushes.Gray };
                hourRowPanel.Children.Add(hourLabel);

                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockStartTime = new TimeSpan(hour, minuteBlock * 10, 0);
                    var blockEndTime = blockStartTime.Add(TimeSpan.FromMinutes(10));
                    var blockContainer = new Grid { Width = 80, Height = 20, Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)), Margin = new Thickness(1, 0, 1, 0) };
                    var overlappingLogs = logsForSelectedDate.Where(log => log.StartTime.TimeOfDay < blockEndTime && log.EndTime.TimeOfDay > blockStartTime).ToList();
                    foreach (var logEntry in overlappingLogs)
                    {
                        var segmentStart = logEntry.StartTime.TimeOfDay > blockStartTime ? logEntry.StartTime.TimeOfDay : blockStartTime;
                        var segmentEnd = logEntry.EndTime.TimeOfDay < blockEndTime ? logEntry.EndTime.TimeOfDay : blockEndTime;
                        var segmentDuration = segmentEnd - segmentStart;
                        if (segmentDuration.TotalSeconds <= 0) continue;
                        double totalBlockWidth = blockContainer.Width;
                        double barWidth = (segmentDuration.TotalMinutes / 10.0) * totalBlockWidth;
                        double leftOffset = ((segmentStart - blockStartTime).TotalMinutes / 10.0) * totalBlockWidth;
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
            CoinDisplay.Text = _settings.Coins.ToString("N0");

            if (status != null)
            {
                CurrentTaskDisplay.Text = status;
            }
            else
            {
                CurrentTaskDisplay.Text = _currentWorkingTask?.Text ?? "휴식 중";
            }

            CharacterPreview.UpdateCharacter();
        }

        // [수정] 날짜 변수를 직접 변경하고 UI를 업데이트하는 새 메서드
        private void UpdateDateAndUI()
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd ddd");
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
            // 전체 할 일 목록(TodoItems)에서 현재 선택된 날짜와 일치하는 항목만 필터링합니다.
            var filtered = TodoItems.Where(t => t.Date.Date == _currentDateForTimeline.Date);
            foreach (var item in filtered)
            {
                FilteredTodoItems.Add(item);
            }
        }

        private bool RemoveTodoItem(ObservableCollection<TodoItem> collection, TodoItem itemToRemove)
        {
            if (collection.Remove(itemToRemove))
            {
                return true;
            }

            foreach (var item in collection)
            {
                if (item.SubTasks != null && RemoveTodoItem(item.SubTasks, itemToRemove))
                {
                    return true;
                }
            }
            return false;
        }

        //private void FocusModeButton_Click(object sender, RoutedEventArgs e)
        //{
        //    _isFocusModeActive = !_isFocusModeActive;
        //    if (_isFocusModeActive)
        //    {
        //        FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 255));
        //        FocusModeButton.Foreground = Brushes.White;
        //        MessageBox.Show("집중 모드가 활성화되었습니다. 방해 앱으로 등록된 프로그램을 실행하면 경고가 표시됩니다.", "집중 모드 ON");
        //    }
        //    else
        //    {
        //        FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF));
        //        FocusModeButton.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        //    }
        //}
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

            // 드래그 영역에 포함된 모든 TimeLogEntry를 찾습니다.
            var selectionRect = new Rect(
                Math.Min(_dragStartPoint.X, e.GetPosition(SelectionCanvas).X),
                Math.Min(_dragStartPoint.Y, e.GetPosition(SelectionCanvas).Y),
                Math.Abs(_dragStartPoint.X - e.GetPosition(SelectionCanvas).X),
                Math.Abs(_dragStartPoint.Y - e.GetPosition(SelectionCanvas).Y)
            );

            // 드래그 영역이 너무 작으면 무시합니다.
            if (selectionRect.Width < 10 && selectionRect.Height < 10) return;

            // 시각적 요소(Border)를 기반으로 해당 로그 엔트리를 찾습니다.
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
                                // 로그 바의 상대적인 위치를 캔버스 기준 좌표로 변환합니다.
                                Point logBarPos = logBar.TranslatePoint(new Point(0, 0), SelectionCanvas);
                                Rect logRect = new Rect(logBarPos.X, logBarPos.Y, logBar.ActualWidth, logBar.ActualHeight);

                                // 드래그 영역과 로그 바가 교차하는지 확인합니다.
                                if (selectionRect.IntersectsWith(logRect))
                                {
                                    selectedLogs.Add(logEntry);
                                }
                            }
                        }
                    }
                }
            }

            if (selectedLogs.Any())
            {
                // 중복 항목을 제거하고 정렬합니다.
                var distinctLogs = selectedLogs.Distinct().OrderBy(l => l.StartTime).ToList();
                var bulkEditWindow = new BulkEditLogsWindow(distinctLogs, TaskItems)
                {
                    Owner = Window.GetWindow(this)
                };

                if (bulkEditWindow.ShowDialog() == true)
                {
                    if (bulkEditWindow.Result == BulkEditResult.ChangeTask)
                    {
                        string newText = bulkEditWindow.SelectedTask.Text;
                        foreach (var log in distinctLogs)
                        {
                            log.TaskText = newText;
                        }
                    }
                    else if (bulkEditWindow.Result == BulkEditResult.Delete)
                    {
                        foreach (var log in distinctLogs)
                        {
                            TimeLogEntries.Remove(log);
                        }
                    }
                    SaveTimeLogs();
                    RecalculateAllTotals();
                    RenderTimeTable();
                }
            }
        }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("타임라인에서 수정하고 싶은 영역을 마우스로 드래그하세요.\n드래그가 끝나면 수정 창이 나타납니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region 사운드 믹서
        private void InitializeSoundPlayers()
        {
            var sounds = new[] { "wave", "forest", "rain", "campfire" };
            foreach (var sound in sounds)
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", $"{sound}.mp3");
                if (File.Exists(path))
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(path));
                    player.MediaEnded += (s, e) => { player.Position = TimeSpan.Zero; player.Play(); }; // Loop
                    player.Volume = 0;
                    player.Play();
                    _soundPlayers[sound] = player;
                }
            }
        }

        private void SoundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.Tag is string soundName)
            {
                if (_soundPlayers.TryGetValue(soundName, out var player))
                {
                    player.Volume = slider.Value;
                }
            }
        }

        public void SetParentWindow(MainWindow window)
        {
            _parentWindow = window;
        }

        // 3. 요청하신 NavButton_Click 메서드 추가
        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null && sender is Button button && button.Tag is string pageName)
            {
                // 페이지 이동 전에 타이머를 정지시킵니다.
                _timer.Stop();

                // 4. 저장된 _parentWindow의 페이지 이동 함수 호출
                await _parentWindow.NavigateToPage(pageName);
            }
        }

        // DashboardPage.xaml.cs (메서드 추가)

        private void LoadMemos()
        {
            if (!File.Exists(_memosFilePath)) return;
            var json = File.ReadAllText(_memosFilePath);
            var loadedMemos = JsonSerializer.Deserialize<ObservableCollection<MemoItem>>(json) ?? new ObservableCollection<MemoItem>();
            AllMemos.Clear();
            foreach (var memo in loadedMemos)
            {
                AllMemos.Add(memo);
            }
            UpdatePinnedMemoView();
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

        // DashboardPage.xaml.cs -> LoadAllData()

        public void LoadAllData()
        {
            LoadSettings();
            LoadTasks();
            LoadTodos();
            LoadTimeLogs();
            LoadMemos(); // ▼▼▼ 추가 ▼▼▼
            UpdateCharacterInfoPanel();
        }


        // DashboardPage.xaml.cs (메서드 추가)
        // DashboardPage.xaml.cs
        // NewMemoButton_Click, MemoPreviewItem_MouseDoubleClick 두 메서드를 지우고 아래 코드를 추가하세요.

        private void PinnedMemo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var memoWindow = new MemoWindow { Owner = Window.GetWindow(this) };
            memoWindow.Closed += (s, args) => LoadMemos(); // 창이 닫히면 고정 메모 새로고침
            memoWindow.ShowDialog();
        }

        #endregion
    }
}

