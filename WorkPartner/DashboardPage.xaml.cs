// 파일: DashboardPage.xaml.cs
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
using System.Windows.Shapes;
using System.Windows.Threading;
using WorkPartner;
using WorkPartner.AI;

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언
        private readonly string _tasksFilePath = DataManager.TasksFilePath;

        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; }
        //public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; } // ◀◀ [이 줄 삭제 또는 주석 처리]

        private MainWindow _parentWindow;
        private MemoWindow _memoWindow;
        private MiniTimerWindow _miniTimer;
        private ViewModels.DashboardViewModel ViewModel => DataContext as ViewModels.DashboardViewModel;

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

        private readonly ObservableCollection<TaskItem> _offlineTaskItems = new ObservableCollection<TaskItem>();
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


        // 파일: DashboardPage.xaml.cs (약 102줄)

        private void OnSettingsUpdated()
        {
            _taskBrushCache.Clear();
            Dispatcher.Invoke(() =>
            {
                if (ViewModel?.Settings == null) return;

                // ▼▼▼ [수정] '얼굴'의 TaskItems 대신 '두뇌'의 ViewModel.TaskItems를 사용합니다. ▼▼▼
                foreach (var taskItem in ViewModel.TaskItems) // 👈 [오류 수정]
                {
                    // '두뇌'의 설정값을 참조합니다.
                    if (ViewModel.Settings.TaskColors.TryGetValue(taskItem.Text, out string colorHex))
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
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            // TimeLogEntries = new ObservableCollection<TimeLogEntry>(); // ◀◀ [이 줄 삭제]
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


        private void SaveTodos()
        {
            ViewModel?.SaveTodos();
        }


        #region UI 이벤트 핸들러

        private async void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                //await LoadAllDataAsync();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentDateDisplay.Text = _currentDateForTimeline.ToString("yyyy-MM-dd");
            CurrentDayDisplay.Text = _currentDateForTimeline.ToString("ddd");
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string newTaskText = TaskInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(newTaskText)) return;

            // ▼▼▼ [수정] '얼굴'의 TaskItems 대신 '두뇌'의 ViewModel.TaskItems를 사용 ▼▼▼
            if (ViewModel.TaskItems.Any(t => t.Text.Equals(newTaskText, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var newTask = new TaskItem { Text = newTaskText };

            // ▼▼▼ [수정] '얼굴'의 TaskItems 대신 '두뇌'의 ViewModel.TaskItems에 추가 ▼▼▼
            ViewModel.TaskItems.Add(newTask);

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---

            // --- ▼▼▼ [수정된 부분 시작] ▼▼▼ ---
            var palette = new HslColorPicker();
            var confirmButton = new Button
            {
                Content = "확인",
                Margin = new Thickness(0, 10, 0, 0)
            };

            // 1. StackPanel 대신 DockPanel 사용
            var panel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = true // 👈 (중요) 마지막 자식이 남은 공간을 모두 채우도록 설정
            };

            // 2. '확인' 버튼을 DockPanel의 '아래(Bottom)'에 고정
            DockPanel.SetDock(confirmButton, Dock.Bottom);
            panel.Children.Add(confirmButton);

            // 3. 'palette'를 마지막에 추가하여 남은 공간을 모두 채우게 함
            panel.Children.Add(palette);
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            var window = new Window
            {
                Title = "과목 색상 선택",
                Content = panel,
                Width = 280,  // 👈 (수정) 사용자가 지정한 너비
                Height = 380, // 👈 (수정) 사용자가 지정한 높이
                              // SizeToContent = SizeToContent.WidthAndHeight, // 👈 (삭제) 고정 크기를 사용할 것이므로 삭제
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
                newTask.ColorBrush = new SolidColorBrush(newColor);

                // ▼▼▼ [수정] _settings -> ViewModel.Settings ▼▼▼
                ViewModel.Settings.TaskColors[newTask.Text] = newColor.ToString();
                // ▼▼▼ [수정] SaveSettings() -> ViewModel.SaveSettings() ▼▼▼
                ViewModel.SaveSettings();
            }
            // --- ▲▲▲ [수정된 부분 끝] ▲▲▲ ---

            TaskInput.Clear();
            ViewModel?.SaveTasks();
            RenderTimeTable();
        }

        // 파일: DashboardPage.xaml.cs

        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼
        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. '두뇌'(ViewModel)가 준비되었는지, 클릭한 대상이 무엇인지 확인
            if (ViewModel == null) return;

            TaskItem selectedTask = null;
            if (sender is FrameworkElement button && button.DataContext is TaskItem taskFromButton)
            {
                selectedTask = taskFromButton;
            }
            else if (TaskListBox.SelectedItem is TaskItem taskFromList) // (예외 처리)
            {
                selectedTask = taskFromList;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("수정할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. '얼굴'은 팝업창을 띄우고 입력받는 역할만 수행
            var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text) { Owner = Window.GetWindow(this) };
            if (inputWindow.ShowDialog() != true) return;

            string newName = inputWindow.ResponseText.Trim();
            string oldName = selectedTask.Text; // 👈 캐시를 비우기 위해 옛날 이름 저장

            // 3. [핵심] '두뇌'에게 이름 변경을 요청
            bool updateSuccess = ViewModel.UpdateTask(selectedTask, newName);

            // 4. '두뇌'가 성공적으로 변경했을 때만 '얼굴'의 UI를 갱신
            if (updateSuccess)
            {
                // '얼굴'이 가진 색상 캐시에서 옛날 이름 제거
                _taskBrushCache.Remove(oldName);

                TaskListBox.Items.Refresh();
                RenderTimeTable();
            }
        }

        // 파일: DashboardPage.xaml.cs

        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼
        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. '두뇌'(ViewModel)가 준비되었는지 확인
            if (ViewModel == null) return;

            // 2. 클릭된 버튼(sender)이 속한 과목(TaskItem) 가져오기
            TaskItem selectedTask = null;
            if (sender is FrameworkElement button && button.DataContext is TaskItem taskFromButton)
            {
                selectedTask = taskFromButton;
            }
            else if (TaskListBox.SelectedItem is TaskItem taskFromList) // (기존 방식 예외 처리)
            {
                selectedTask = taskFromList;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("삭제할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 3. 사용자에게 삭제 확인 (이 로직은 '얼굴'이 담당하는 것이 맞습니다)
            if (MessageBox.Show($"'{selectedTask.Text}' 과목을 삭제하시겠습니까?\n관련된 모든 학습 기록도 삭제됩니다.", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // 4. [핵심] '두뇌'에게 삭제를 요청
            ViewModel.DeleteTask(selectedTask);

            // 5. '얼굴'은 화면을 다시 그리기만 함
            RenderTimeTable();
        }

        private void TaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTaskButton_Click(sender, e);
        }

        // 파일: DashboardPage.xaml.cs

        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼
        private void AddTodoButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. '두뇌'(ViewModel)가 준비되었는지, 입력값이 있는지 확인
            if (ViewModel == null) return;

            string newTodoText = TodoInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(newTodoText)) return;

            // 2. '얼굴'에서 필요한 UI 정보(선택된 부모, 현재 날짜)를 가져옵니다.
            TodoItem parentTodo = TodoTreeView.SelectedItem as TodoItem;
            DateTime currentDate = _currentDateForTimeline; // '얼굴'이 알고 있는 현재 날짜

            // 3. [핵심] '두뇌'에게 추가를 요청 (모든 정보 전달)
            ViewModel.AddTodo(newTodoText, parentTodo, currentDate);

            // 4. '얼굴'은 UI 정리만 수행
            TodoInput.Clear();
            FilterTodos(); // '얼굴'의 필터링된 뷰 갱신
        }

        // 파일: DashboardPage.xaml.cs

        // 파일: DashboardPage.xaml.cs

        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼
        private void EditTodoButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. '두뇌'(ViewModel)가 준비되었는지, 클릭한 대상이 무엇인지 확인
            if (ViewModel == null) return;

            TodoItem selectedTodo = null;
            if (sender is FrameworkElement button && button.DataContext is TodoItem todoFromButton)
            {
                selectedTodo = todoFromButton;
            }
            else if (TodoTreeView.SelectedItem is TodoItem todoFromTree) // (예외 처리)
            {
                selectedTodo = todoFromTree;
            }

            if (selectedTodo == null)
            {
                MessageBox.Show("수정할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. '얼굴'은 팝업창을 띄우고 입력받는 역할만 수행
            var inputWindow = new InputWindow("할 일 수정", selectedTodo.Text) { Owner = Window.GetWindow(this) };
            if (inputWindow.ShowDialog() != true) return;

            // 3. [핵심] '두뇌'에게 수정을 요청
            ViewModel.UpdateTodo(selectedTodo, inputWindow.ResponseText);

            // (UI 갱신은 TodoItem의 INotifyPropertyChanged가 자동으로 처리하므로 FilterTodos() 불필요)
        }

        // 파일: DashboardPage.xaml.cs

        // ▼▼▼ 이 메서드 전체를 아래 코드로 교체하세요 ▼▼▼
        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. '두뇌'(ViewModel)가 준비되었는지, 클릭한 대상이 무엇인지 확인
            if (ViewModel == null) return;

            TodoItem selectedTodo = null;
            if (sender is FrameworkElement button && button.DataContext is TodoItem todoFromButton)
            {
                selectedTodo = todoFromButton;
            }
            else if (TodoTreeView.SelectedItem is TodoItem todoFromTree) // (예외 처리)
            {
                selectedTodo = todoFromTree;
            }

            if (selectedTodo == null)
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 사용자에게 삭제 확인 (이 로직은 '얼굴'이 담당)
            if (MessageBox.Show($"'{selectedTodo.Text}' 할 일을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // 3. [핵심] '두뇌'에게 삭제를 요청
            ViewModel.DeleteTodo(selectedTodo);

            // 4. '얼굴'은 필터링된 화면만 다시 그림
            FilterTodos();
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTodoButton_Click(sender, e);
        }

        // 파일: DashboardPage.xaml.cs (약 510줄)

        // 파일: DashboardPage.xaml.cs

        private void SaveTodos_Event(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: TodoItem todoItem })
            {
                if (ViewModel?.Settings == null) return;

                if (todoItem.IsCompleted && !todoItem.HasBeenRewarded)
                {
                    // ▼▼▼ [수정] _settings -> ViewModel.Settings ▼▼▼
                    ViewModel.Settings.Coins += 10;
                    todoItem.HasBeenRewarded = true;
                    UpdateCoinDisplay();
                    ViewModel.SaveSettings();
                    SoundPlayer.PlayCompleteSound();
                }
                else if (!todoItem.IsCompleted && todoItem.HasBeenRewarded)
                {
                    // ▼▼▼ [수정] _settings -> ViewModel.Settings ▼▼▼
                    ViewModel.Settings.Coins -= 10;
                    todoItem.HasBeenRewarded = false;
                    UpdateCoinDisplay();
                    ViewModel.SaveSettings();
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
            if (ViewModel == null) return; // '두뇌'가 없으면 중지

            if (_memoWindow == null || !_memoWindow.IsVisible)
            {
                // ▼▼▼ [수정] 생성자에 '두뇌'(ViewModel)를 전달 ▼▼▼
                _memoWindow = new MemoWindow(ViewModel) { Owner = Window.GetWindow(this) };
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
                TaskText = ViewModel.TaskItems.FirstOrDefault()?.Text ?? "과목 없음"
            };

            // 3. [수정] '편집' 생성자를 사용하여 팝업을 띄웁니다.
            var win = new AddLogWindow(ViewModel.TaskItems, templateLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            // 4. [신규] 사용자가 '삭제'를 눌러 템플릿 생성을 취소한 경우
            if (win.IsDeleted) return;

            // 5. [기존 로직] win.NewLogEntry에는 팝업에서 수정한 최종 결과가 담겨있음
            if (win.NewLogEntry != null)
            {
                vm.AddManualLog(win.NewLogEntry);

                var addedTaskName = win.NewLogEntry.TaskText;
                // [오류 527 수정]
                var taskToSelect = ViewModel.TaskItems.FirstOrDefault(t => t.Text == addedTaskName);
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

            var win = new AddLogWindow(ViewModel.TaskItems, originalLog) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted)
            {
                vm.DeleteLog(originalLog);
            }
            else
            {
                vm.UpdateLog(originalLog, win.NewLogEntry);

                var editedTaskName = win.NewLogEntry.TaskText;
                // [오류 571 수정]
                var taskToSelect = ViewModel.TaskItems.FirstOrDefault(t => t.Text == editedTaskName);
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

        private void UpdateMainTimeDisplay()
        {
            if (DataContext is not ViewModels.DashboardViewModel vm) return;

            TaskItem selectedTask = TaskListBox.SelectedItem as TaskItem;
            TimeSpan timeToShow = TimeSpan.Zero;

            if (selectedTask != null)
            {
                timeToShow = selectedTask.TotalTime;
            }

            // 1. [유지] 상단 메인 타이머 로직
            if (_currentDateForTimeline.Date != DateTime.Today.Date)
            {
                MainTimeDisplay.Text = timeToShow.ToString(@"hh\:mm\:ss");
            }
            else
            {
                if (vm.IsTimerRunning == false)
                {
                    MainTimeDisplay.Text = timeToShow.ToString(@"hh\:mm\:ss");
                }
            }

            // 2. [수정] 하단 총 학습 시간 로직
            if (_currentDateForTimeline.Date != DateTime.Today.Date)
            {
                // '다른 날짜'면, '얼굴'(Page)이 수동으로 계산해서 텍스트 설정
                var otherDateLogs = vm.TimeLogEntries
                    .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();
                var totalTimeOtherDate = new TimeSpan(otherDateLogs.Sum(log => log.Duration.Ticks));
                SelectedTaskTotalTimeDisplay.Text = $"오늘의 작업 시간 | {totalTimeOtherDate:hh\\:mm\\:ss}";
            }
            // '오늘' 날짜면, 아무것도 하지 않습니다.
            // (아래 2, 3단계에서 추가할 이벤트 핸들러가 '두뇌'의 값을 받아 처리)
        }

        // [복원 후 ✅] (맨 처음 버그 수정했던 그 코드입니다)
        private void RecalculateAllTotals()
        {
            if (ViewModel == null) return;

            var selectedDateLogs = ViewModel.TimeLogEntries
                .Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            if (_currentDateForTimeline.Date == DateTime.Today.Date)
            {
                // "오늘"이면 '두뇌'(ViewModel)의 '실시간' 목록을 업데이트
                foreach (var task in ViewModel.TaskItems)
                {
                    var taskLogs = selectedDateLogs.Where(log => log.TaskText == task.Text);
                    task.TotalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
                }
            }
            else
            {
                // "다른 날짜"면 '오프라인' 목록을 새로 채웁니다.
                _offlineTaskItems.Clear();
                foreach (var vmTask in ViewModel.TaskItems)
                {
                    var taskLogs = selectedDateLogs.Where(log => log.TaskText == vmTask.Text);
                    TimeSpan totalTime = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));

                    var offlineTask = new TaskItem
                    {
                        Text = vmTask.Text,
                        TotalTime = totalTime,
                        ColorBrush = GetColorForTask(vmTask.Text)
                    };
                    _offlineTaskItems.Add(offlineTask);
                }
            }

            //UpdateMainTimeDisplay(); // 👈 이게 꼭 마지막에 호출되어야 함
        }

        // [복원 후 ✅]
        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // [복원] '얼굴'이 UI를 직접 업데이트하도록 합니다.
            UpdateMainTimeDisplay();

            // [유지] '두뇌'에게 선택 항목이 바뀐 것을 '알려는' 줍니다.
            if (ViewModel != null)
            {
                ViewModel.SelectedTaskItem = TaskListBox.SelectedItem as TaskItem;
            }
        }
        // 파일: DashboardPage.xaml.cs (약 700줄)

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            // ▼▼▼ [수정] _settings 대신 ViewModel.Settings 를 사용합니다. ▼▼▼
            if (ViewModel?.Settings != null && ViewModel.Settings.TaskColors.TryGetValue(taskName, out string colorHex))
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


        // 파일: DashboardPage.xaml.cs (약 796줄)

        private void UpdateCharacterInfoPanel(string status = null)
        {
            // ▼▼▼ [수정] _settings 대신 ViewModel.Settings 를 사용합니다. ▼▼▼
            if (ViewModel?.Settings == null) return;
            UsernameTextBlock.Text = ViewModel.Settings.Username;
            LevelTextBlock.Text = $"Lv.{ViewModel.Settings.Level}";
            ExperienceBar.Value = ViewModel.Settings.Experience;
            UpdateCoinDisplay();
            CharacterPreview.UpdateCharacter();
        }

        private void UpdateCoinDisplay()
        {
            // ▼▼▼ [수정] _settings 대신 ViewModel.Settings 를 사용합니다. ▼▼▼
            if (ViewModel?.Settings != null) CoinTextBlock.Text = ViewModel.Settings.Coins.ToString("N0");
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

            if (ViewModel == null) return;

            // ▼▼▼ [버그 수정 2] ▼▼▼

            // 1. (목록 교체 전) 현재 선택된 과목 이름을 기억합니다.
            string selectedTaskName = (TaskListBox.SelectedItem as TaskItem)?.Text;

            // 2. (기존 로직) 날짜에 따라 목록을 교체합니다.
            if (_currentDateForTimeline.Date == DateTime.Today.Date)
            {
                TaskListBox.ItemsSource = ViewModel.TaskItems;
            }
            else
            {
                TaskListBox.ItemsSource = _offlineTaskItems;
            }

            RenderTimeTable();
            RecalculateAllTotals(); // 👈 (이제 UI 업데이트를 호출하지 않음)
            FilterTodos();

            // 3. (목록 교체 후) 기억해둔 이름으로 과목을 다시 찾습니다.
            if (selectedTaskName != null)
            {
                // 현재 연결된 목록(오늘용 또는 다른 날짜용)을 가져옵니다.
                var currentList = TaskListBox.ItemsSource as IEnumerable<TaskItem>;
                if (currentList != null)
                {
                    var taskToReselect = currentList.FirstOrDefault(t => t.Text == selectedTaskName);
                    if (taskToReselect != null)
                    {
                        // 4. 과목을 다시 선택합니다.
                        TaskListBox.SelectedItem = taskToReselect;
                        // (이 코드가 TaskListBox_SelectionChanged 이벤트를 발생시켜
                        //  UpdateMainTimeDisplay()를 호출하므로 시간이 올바르게 표시됩니다.)
                    }
                }
            }

            // 5. 만약 선택된 항목이 없다면 (이전에 아무것도 선택 안 했거나,
            //    새 목록에 해당 과목이 없는 경우) 00:00:00으로 표시하기 위해
            //    UpdateMainTimeDisplay를 수동으로 한 번 호출합니다.
            if (TaskListBox.SelectedItem == null)
            {
                UpdateMainTimeDisplay();
            }
            // ▲▲▲ [수정 완료] ▲▲▲
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
            if (ViewModel == null) return; var filtered = ViewModel.TodoItems.Where(t => t.Date.Date == _currentDateForTimeline.Date);
            foreach (var item in filtered) FilteredTodoItems.Add(item);
        }


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

            var bulkEditWindow = new BulkEditLogsWindow(distinctLogs, ViewModel.TaskItems) { Owner = Window.GetWindow(this) };
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
            if (ViewModel == null) return; var pinnedMemo = ViewModel.AllMemos.FirstOrDefault(m => m.IsPinned);
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
            if (ViewModel == null) return; // '두뇌'가 없으면 중지

            // ▼▼▼ [수정] 생성자에 '두뇌'(ViewModel)를 전달 ▼▼▼
            var memoWindow = new MemoWindow(ViewModel) { Owner = Window.GetWindow(this) };

            memoWindow.Closed += (s, args) => UpdatePinnedMemoView();
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

            // ▼▼▼ [수정] '두뇌'가 준비되지 않았으면 중단합니다. ▼▼▼
            if (ViewModel?.Settings == null) return;

            TaskListBox.SelectedItem = selectedTask;

            var palette = new HslColorPicker();

            // ▼▼▼ [수정] _settings -> ViewModel.Settings ▼▼▼
            if (ViewModel.Settings.TaskColors.TryGetValue(selectedTask.Text, out var hex))
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

                // ▼▼▼ [수정] _settings -> ViewModel.Settings ▼▼▼
                ViewModel.Settings.TaskColors[selectedTask.Text] = newColor.ToString();
                selectedTask.ColorBrush = new SolidColorBrush(newColor);

                // ▼▼▼ [수정] DataManager.SaveSettings(_settings) -> ViewModel.SaveSettings() ▼▼▼
                ViewModel.SaveSettings();

                RenderTimeTable();
            }

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

                // ▼▼▼ [추가] '할 일' 목록 이벤트 구독 해제 ▼▼▼
                if (oldVm.TodoItems != null)
                {
                    oldVm.TodoItems.CollectionChanged -= TodoItems_CollectionChanged;
                }

                // ▼▼▼ [추가] '시간 기록' 목록 이벤트 구독 해제 ▼▼▼
                if (oldVm.TimeLogEntries != null)
                {
                    oldVm.TimeLogEntries.CollectionChanged -= TimeLogEntries_CollectionChanged;
                }

                if (oldVm.AllMemos != null)
                {
                    oldVm.AllMemos.CollectionChanged -= Memos_CollectionChanged;
                }
            }
            if (e.NewValue is ViewModels.DashboardViewModel newVm)
            {
                newVm.TimeUpdated += OnViewModelTimeUpdated;
                newVm.TimerStoppedAndSaved += OnViewModelTimerStopped;
                newVm.CurrentTaskChanged += OnViewModelTaskChanged;
                newVm.PropertyChanged += OnViewModelPropertyChanged; // ◀◀ [이 줄 추가]

                if (newVm.TodoItems != null)
                {
                    newVm.TodoItems.CollectionChanged += TodoItems_CollectionChanged;
                }

                if (newVm.TimeLogEntries != null)
                {
                    newVm.TimeLogEntries.CollectionChanged += TimeLogEntries_CollectionChanged;
                }
                // ▲▲▲ [추가 완료] ▲▲▲

                if (newVm.AllMemos != null)
                {
                    newVm.AllMemos.CollectionChanged += Memos_CollectionChanged;
                }
                TaskListBox.ItemsSource = newVm.TaskItems;
                // ▲▲▲ [여기까지 추가] ▲▲▲
                RecalculateAllTotals();
                RenderTimeTable();
                UpdateCharacterInfoPanel();
                FilterTodos(); // '두뇌'가 연결되었으니, '할 일' 목록을 즉시 필터링합니다.
            }
        }

        private void Memos_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // '두뇌'의 메모 목록이 바뀌었으니, '얼굴'의 고정된 메모 뷰도 갱신합니다.
            Dispatcher.Invoke(() =>
            {
                UpdatePinnedMemoView();
            });
        }


        private void TimeLogEntries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // '두뇌'의 시간 기록이 바뀌었으니, '얼굴'의 타임라인과 계산을 모두 새로고침합니다.
            // (UI 스레드에서 실행되도록 보장합니다.)
            Dispatcher.Invoke(() =>
            {
                RecalculateAllTotals();
                RenderTimeTable();
            });

        }

        private void TodoItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FilterTodos();
            });
        }

        private void OnViewModelTimerStopped(object sender, EventArgs e)
        {
            // '두뇌'가 이미 데이터를 변경했으므로, '얼굴'은 화면을 다시 그리기만 하면 됩니다.
            // 비동기(async)로 데이터를 로드할 필요가 없으므로 Dispatcher.Invoke만 사용합니다.
            Dispatcher.Invoke(() =>
            {
                // await LoadTimeLogsAsync(); // 👈 [삭제!]
                RecalculateAllTotals();
                RenderTimeTable();
            });
        }
        // 파일: DashboardPage.xaml.cs
        // 메서드: OnViewModelTimeUpdated (약 1157줄 근처)

        // [복원 후 ✅]
        private void OnViewModelTimeUpdated(string newTime)
        {
            if (_miniTimer != null && _miniTimer.IsVisible)
            {
                if (ViewModel?.Settings == null) return;
                _miniTimer.UpdateData(ViewModel.Settings, ViewModel.CurrentTaskDisplayText, newTime);
            }

            // [복원] '오늘' 날짜가 아니면 실시간 업데이트를 무시
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            // [복원] '오늘' 날짜일 때만 '얼굴'의 텍스트를 직접 업데이트
            Dispatcher.Invoke(() =>
            {
                MainTimeDisplay.Text = newTime;
            });
        }
        // 파일: DashboardPage.xaml.cs (약 1251줄 뒤)

        // ▼▼▼ [이 메서드를 새로 추가하세요] ▼▼▼
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // '오늘' 날짜가 아니면 '두뇌'의 업데이트를 무시
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;

            // '두뇌'의 TotalTimeTodayDisplayText 속성이 변경될 때만
            if (e.PropertyName == nameof(ViewModels.DashboardViewModel.TotalTimeTodayDisplayText))
            {
                if (sender is ViewModels.DashboardViewModel vm)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // '얼굴'의 하단 텍스트를 '두뇌'의 값으로 설정
                        SelectedTaskTotalTimeDisplay.Text = vm.TotalTimeTodayDisplayText;
                    });
                }
            }
        }
        // ▲▲▲

        private void OnViewModelTaskChanged(string newTaskName)
        {
            // ▼▼▼ [수정] '오늘' 날짜가 아니면, '두뇌'의 변경 사항을 
            // '얼굴'의 메인 디스플레이에 반영하지 않습니다.
            if (_currentDateForTimeline.Date != DateTime.Today.Date) return;
            // ▲▲▲

            // 1. 메인 과목 텍스트(CurrentTaskDisplay) 업데이트
            Dispatcher.Invoke(() =>
            {
                CurrentTaskDisplay.Text = newTaskName;
            });

            // 2. 메인 대시보드 UI(TaskListBox) 동기화
            Dispatcher.Invoke(() =>
            {
                var foundTask = ViewModel.TaskItems.FirstOrDefault(t => t.Text == newTaskName);

                if (foundTask != null && TaskListBox.SelectedItem != foundTask)
                {
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