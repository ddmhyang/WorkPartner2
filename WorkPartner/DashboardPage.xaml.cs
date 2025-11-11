// 파일: DashboardPage.xaml.cs
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
using System.ComponentModel;

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
        //public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; } // ◀◀ [이 줄 삭제 또는 주석 처리]
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

        // 계산된 값 (생성자에서 초기화)
        private readonly double _rowHeight;
        private readonly double _cellWidth;

        private DateTime _currentDateForTimeline = DateTime.Today;

        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;

        private readonly Dictionary<string, BackgroundSoundPlayer> _soundPlayers = new();

        private readonly Dictionary<string, SolidColorBrush> _taskBrushCache = new();
        private static readonly SolidColorBrush DefaultGrayBrush = new SolidColorBrush(Colors.Gray);

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

            _rowHeight = _blockHeight + (_verticalMargin * 2.65) + _borderBottomThickness;
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
                UpdateCharacterInfoPanel(); // 👈 [이 줄을 추가하세요]
            });
        }

        private void InitializeData()
        {
            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            // TimeLogEntries = new ObservableCollection<TimeLogEntry>(); // ◀◀ [이 줄 삭제]
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

        // ▼▼▼ [오류 327] 이 메서드는 OnViewModelTimerStopped에서 사용되므로, VM 리스트를 채우도록 수정 ▼▼▼
        private async Task LoadTimeLogsAsync()
        {
            if (!File.Exists(_timeLogFilePath)) return;
            try
            {
                await using var stream = new FileStream(_timeLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var loadedLogs = await JsonSerializer.DeserializeAsync<ObservableCollection<TimeLogEntry>>(stream);
                if (loadedLogs == null) return;

                if (DataContext is ViewModels.DashboardViewModel vm)
                {
                    vm.TimeLogEntries.Clear(); // ◀ (오류 327 수정)
                    foreach (var log in loadedLogs) vm.TimeLogEntries.Add(log);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading time logs: {ex.Message}"); }
        }

        private void SaveTimeLogs()
        {
            // DataManager.SaveTimeLogsImmediately(TimeLogEntries); // ◀ (오류 342 수정)
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

        public async Task LoadAllDataAsync()
        {
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

            // 🎯 수정 후
            var palette = new HslColorPicker(); // 
            var window = new Window
            {
                Title = "과목 색상 선택",
                Content = palette,
                Width = 280, // HSL 피커에 맞게 너비 조절
                Height = 350, // HSL 피커에 맞게 높이 조절
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            // ✨ [수정] HslColorPicker의 ColorChanged 이벤트를 사용합니다. (로직 동일)
            palette.ColorChanged += (s, newColor) =>
            {
                _settings.TaskColors[newTask.Text] = newColor.ToString();
                SaveSettings();
                window.Close(); // 색상 선택 시 창 닫기
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
            // ▼▼▼ [V6 수정] VM에서 TimeLogEntries를 가져와야 함 ▼▼▼
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

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

            // ▼▼▼ [V6 수정] vm.TimeLogEntries 사용 ▼▼▼
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
            // ▼▼▼ [V6 수정] VM의 리스트를 지연 저장 ▼▼▼
            DataManager.SaveTimeLogs(vm.TimeLogEntries); // 👈 'Immediately'를 뺐습니다.
            SaveSettings();

            TaskListBox.Items.Refresh();
            RenderTimeTable();
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---
            TaskItem selectedTask = null;

            // 1. 클릭된 버튼(sender)에서 DataContext(TaskItem)를 가져옵니다.
            if (sender is FrameworkElement button && button.DataContext is TaskItem taskFromButton)
            {
                selectedTask = taskFromButton;
            }
            // 2. (예외 처리) 만약 DataContext가 없으면, 기존 방식(선택된 항목)을 사용합니다.
            else if (TaskListBox.SelectedItem is TaskItem taskFromList)
            {
                selectedTask = taskFromList;
            }

            // 3. 그래도 없으면 삭제할 대상이 없는 것입니다.
            if (selectedTask == null)
            {
                MessageBox.Show("삭제할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            // ▼▼▼ (기존 로직 동일) ▼▼▼
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
            DataManager.SaveTimeLogs(vm.TimeLogEntries); // 👈 (이전 단계에서 수정했어야 함) vm.DeleteLog(log)를 사용하거나, 이 라인을 _timeLogService.SaveTimeLogsAsync(vm.TimeLogEntries)로 바꿔야 하지만, 지금은 시연이 우선이니 그대로 둡니다.
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
            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---
            TodoItem selectedTodo = null;

            // 1. 클릭된 버튼(sender)에서 DataContext(TodoItem)를 가져옵니다.
            if (sender is FrameworkElement button && button.DataContext is TodoItem todoFromButton)
            {
                selectedTodo = todoFromButton;
            }
            // 2. (예외 처리) 만약 DataContext가 없으면, 기존 방식(선택된 항목)을 사용합니다.
            else if (TodoTreeView.SelectedItem is TodoItem todoFromTree)
            {
                selectedTodo = todoFromTree;
            }

            // 3. 그래도 없으면 삭제할 대상이 없는 것입니다.
            if (selectedTodo == null)
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---


            // ▼▼▼ (기존 로직 동일) ▼▼▼
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
                // ▼▼▼ [이 블록 전체 추가] ▼▼▼
                else if (!todoItem.IsCompleted && todoItem.HasBeenRewarded)
                {
                    // 완료를 취소했고, 이전에 보상을 받았다면
                    _settings.Coins -= 10; // 코인 회수 (마이너스 가능)
                    todoItem.HasBeenRewarded = false; // 보상 상태 리셋
                    UpdateCoinDisplay();
                    SaveSettings();
                }
                // ▲▲▲ [여기까지 추가] ▲▲▲
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
            // 1. VM 가져오기
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---

            // 2. [신규] 현재 타임라인 날짜를 기준으로 '임시 로그' 생성
            // (시간은 현재 시간의 '시'를 가져오고, 분/초는 0으로)
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
                EndTime = defaultStartTime.AddHours(1), // 기본 1시간
                TaskText = TaskItems.FirstOrDefault()?.Text ?? "과목 없음" // 첫 번째 과목 선택
            };

            // 3. [수정] '편집' 생성자를 사용하여 팝업을 띄웁니다.
            var win = new AddLogWindow(TaskItems, templateLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            // 4. [신규] 사용자가 '삭제'를 눌러 템플릿 생성을 취소한 경우
            if (win.IsDeleted) return;

            // 5. [기존 로직] win.NewLogEntry에는 팝업에서 수정한 최종 결과가 담겨있음
            if (win.NewLogEntry != null)
            {
                // 6. [핵심] Page 리스트가 아닌 VM의 public 메서드 호출
                vm.AddManualLog(win.NewLogEntry); // ◀ (오류 370 수정) - [기존 코드 재사용]

                // (과목 선택 로직은 그대로 둠)
                var addedTaskName = win.NewLogEntry.TaskText;
                var taskToSelect = TaskItems.FirstOrDefault(t => t.Text == addedTaskName);
                if (taskToSelect != null)
                {
                    TaskListBox.SelectedItem = taskToSelect;
                }
            }

            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            // (기존 코드 재사용)
            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not TimeLogEntry log) return;
            // 1. VM 가져오기
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // [중요] 수정 시 VM에 있는 '원본' 객체를 전달해야 함
            var originalLog = vm.TimeLogEntries.FirstOrDefault(l =>
                l.StartTime == log.StartTime &&
                l.TaskText == log.TaskText &&
                l.EndTime == log.EndTime
            );
            // (만약 못찾으면 Page의 log 객체라도 사용)
            if (originalLog == null) originalLog = log;

            var win = new AddLogWindow(TaskItems, originalLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted)
            {
                // 2. [핵심] Page 리스트가 아닌 VM의 public 메서드 호출
                vm.DeleteLog(originalLog);
            }
            else
            {
                // 2. [핵심] Page 리스트가 아닌 VM의 public 메서드 호출
                vm.UpdateLog(originalLog, win.NewLogEntry);

                // (과목 선택 로직은 그대로 둠)
                var editedTaskName = win.NewLogEntry.TaskText;
                var taskToSelect = TaskItems.FirstOrDefault(t => t.Text == editedTaskName);
                if (taskToSelect != null)
                {
                    TaskListBox.SelectedItem = taskToSelect;
                }
            }

            // 3. [삭제] Page가 직접 저장/계산하지 않음
            // DataManager.SaveTimeLogsImmediately(TimeLogEntries);

            // 4. [수정] VM이 계산했으니, Page는 VM 리스트를 사용해 그리기만 함
            RecalculateAllTotals();
            RenderTimeTable();

            // 5. [삭제] VM은 이미 스스로 갱신했으므로 이 로직 필요 없음
            // if (DataContext is ViewModels.DashboardViewModel vm) ...
        }
        #endregion

        #region 화면 렌더링 및 UI 업데이트

        // (약 510줄 근처)
        // 
        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼

        private void UpdateMainTimeDisplay()
        {
            // ▼▼▼ [수정] VM을 먼저 가져옵니다. ▼▼▼
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // 1. [수정] 현재 선택된 과목을 단순하게 가져옵니다.
            TaskItem selectedTask = TaskListBox.SelectedItem as TaskItem;

            // 2. [!!! BUG FIX !!!]
            // 선택이 null일 때 첫 번째 항목을 강제로 다시 선택하는
            // "로직 폭탄" 코드를 완전히 제거합니다.
            /*
            if (selectedTask == null && TaskItems.Any())
            {
                selectedTask = TaskItems.FirstOrDefault(); 
                if (TaskListBox.SelectedItem == null)
                {
                    TaskListBox.SelectedItem = selectedTask; 
                }
            }
            */
            // [!!! BUG FIX 완료 !!!]


            // 3. [수정] selectedTask가 null일 수 있으므로, null일 때는 0초를 표시
            TimeSpan timeToShow = TimeSpan.Zero;
            if (selectedTask != null)
            {
                // (우리가 이전에 수정한 코드로 인해 TotalTime은 실시간으로 업데이트됨)
                timeToShow = selectedTask.TotalTime;
            }

            // 1. 메인 타이머 업데이트
            MainTimeDisplay.Text = timeToShow.ToString(@"hh\:mm\:ss");

            // 2. 하단 총 학습 시간 업데이트
            // ▼▼▼ [핵심 수정] Page의 리스트가 아닌 VM의 리스트(vm.TimeLogEntries)를 사용
            var todayLogs = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();
            // ▲▲▲
            var totalTimeToday = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));
            SelectedTaskTotalTimeDisplay.Text = $"이날의 총 학습 시간: {(int)totalTimeToday.TotalHours}시간 {totalTimeToday.Minutes}분";
        }

        // ▼▼▼ [V6 수정] (오류 CS0103) VM 리스트 사용 ▼▼▼
        private void RecalculateAllTotals()
        {
            // ▼▼▼ [수정] VM을 먼저 가져옵니다. ▼▼▼
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            // ▼▼▼ [핵심 수정] Page의 리스트가 아닌 VM의 리스트(vm.TimeLogEntries)를 사용
            var todayLogs = vm.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();
            // ▲▲▲

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
            SelectionCanvas.Height = (24 * _rowHeight) + _verticalMargin; // 👈 _(언더스코어) 사용

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

            // ❗️ [수정] 모든 로컬 변수 선언을 삭제하고,
            // ❗️ 클래스 필드(_blockWidth, _rowHeight 등)를 사용합니다.

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, _verticalMargin, 0, _verticalMargin) // 👈 _(언더스코어) 사용
                };

                var hourLabel = new TextBlock
                {
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
                        BorderBrush = (Brush)FindResource("BorderBrush"),
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
            UsernameTextBlock.Text = _settings.Username; // [!] 수정됨
            LevelTextBlock.Text = $"Lv.{_settings.Level}"; // 👈 [추가]
            ExperienceBar.Value = _settings.Experience; // 👈 [추가]
            UpdateCoinDisplay();
            CharacterPreview.UpdateCharacter();
        }

        private void UpdateCoinDisplay()
        {
            if (_settings != null) CoinTextBlock.Text = _settings.Coins.ToString("N0"); // [!] 수정됨
        }

        // [!] 아래 새 메서드를 클래스 내부에 추가하세요.
        private async void GoToAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                // "Avatar" 페이지로 이동
                await _parentWindow.NavigateToPage("Avatar");
            }
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

        // 🎯 수정 후
        private void ChangeTaskColor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: TaskItem selectedTask }) return;

            TaskListBox.SelectedItem = selectedTask;

            // ✨ [수정] HslColorPicker(UserControl)를 호스팅할 새 Window를 만듭니다.
            var palette = new HslColorPicker();

            // (이미 저장된 색이 있으면 팔레트에 설정)
            if (_settings.TaskColors.TryGetValue(selectedTask.Text, out var hex))
            {
                try
                {
                    // ✨ [수정] HSL 피커는 SetHsl() 메서드를 사용해 초기 색상을 설정해야 합니다.
                    var currentColor = (Color)ColorConverter.ConvertFromString(hex);
                    (double H, double S, double L) hsl = WpfColorToHsl(currentColor);
                    palette.SetHsl(hsl.H, hsl.S, hsl.L);
                }
                catch { /* ignore invalid hex */ }
            }

            var window = new Window
            {
                Title = "과목 색상 변경",
                Content = palette,
                Width = 280, // HSL 피커에 맞게 너비 조절
                Height = 350, // HSL 피커에 맞게 높이 조절
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            // ✨ [수정] 팔레트에서 색을 선택하면(ColorChanged) 창을 닫고 저장합니다. (이 로직은 동일)
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

        // (약 1157줄 근처)
        private void OnViewModelTimeUpdated(string newTime)
        {
            // 1. 미니 타이머는 "항상" 오늘의 실시간 데이터로 업데이트합니다.
            if (_miniTimer != null && _miniTimer.IsVisible)
            {
                if (_settings == null) LoadSettings();

                // (위 1번 수정으로 CurrentTaskDisplay.Text는 항상 최신 상태가 보장됨)
                _miniTimer.UpdateData(_settings, CurrentTaskDisplay.Text, newTime);
            }

            // 2. 메인 대시보드 UI(메인 타이머)는 "오늘 날짜를 볼 때만" 업데이트합니다.
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            Dispatcher.Invoke(() =>
            {
                // 1. 메인 타이머 업데이트
                MainTimeDisplay.Text = newTime;
            });

            // ▼▼▼ [이 코드 블록을 여기에 추가하세요!] ▼▼▼
            // 3. (중요) '두뇌'(ViewModel)의 리스트와 '화면'(Page)의 리스트를 동기화합니다.
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
            // ▲▲▲ [여기까지 추가] ▲▲▲
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

        // 파일: DashboardPage.xaml.cs
        // (약 1238줄 근처)
private void EvaluateDayButton_Click(object sender, RoutedEventArgs e)
        {
            // ▼▼▼ [수정] VM을 먼저 가져옵니다. ▼▼▼
            if (DataContext is not ViewModels.DashboardViewModel vm) return;
            
            DateTime targetDate = _currentDateForTimeline.Date;

            // 1. 이 날짜의 현재 저장된 점수를 찾습니다.
            // ▼▼▼ [핵심 수정] VM 리스트(vm.TimeLogEntries) 사용
            var firstRatedLog = vm.TimeLogEntries.FirstOrDefault(log => // (오류 1245 수정)
                log.StartTime.Date == targetDate && log.FocusScore > 0);
            // ▲▲▲

            int currentScore = firstRatedLog?.FocusScore ?? 0; // 없으면 0점

            // 2. 팝업 창을 띄웁니다.
            var ratingWindow = new DailyFocusRatingWindow(currentScore)
            {
                Owner = Window.GetWindow(this)
            };

            // 3. 팝업 창에서 "저장" 버튼을 누른 경우
            if (ratingWindow.ShowDialog() == true)
            {
                int newScore = ratingWindow.SelectedScore;

                // 5. 이 날짜의 모든 로그를 찾습니다.
                // ▼▼▼ [핵심 수정] VM 리스트(vm.TimeLogEntries) 사용
                var logsForDay = vm.TimeLogEntries.Where(log => log.StartTime.Date == targetDate).ToList(); // (오류 1264 수정)
                // ▲▲▲

                if (!logsForDay.Any())
                {
                    MessageBox.Show("이날에는 적용할 학습 기록이 없습니다.", "알림");
                    return;
                }

                bool isChanged = false;

                // 6. 모든 로그의 FocusScore를 새 점수로 덮어씁니다.
                foreach (var log in logsForDay)
                {
                    if (log.FocusScore != newScore)
                    {
                        log.FocusScore = newScore;
                        isChanged = true;
                    }
                }

                // 7. 변경된 경우에만 파일에 "즉시" 저장합니다.
                if (isChanged)
                {
                    // ▼▼▼ [핵심 수정] VM 리스트(vm.TimeLogEntries)를 저장
                    DataManager.SaveTimeLogs(vm.TimeLogEntries); // 👈 'Immediately'를 뺐습니다.
                    // ▲▲▲
                    MessageBox.Show($"'{targetDate:yyyy-MM-dd}'의 모든 기록에 집중도 {newScore}점을 적용했습니다.", "저장 완료");
                }
            }
        }
    }
}