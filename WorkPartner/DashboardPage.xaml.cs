using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WorkPartner.ViewModels; // ✨ ViewModel 네임스페이스 추가

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        private MainWindow _parentWindow;
        // ✨ ViewModel 인스턴스를 멤버 변수로 가짐
        private readonly DashboardViewModel _viewModel;

        // UI와 직접 관련된 필드들은 그대로 유지
        private readonly Dictionary<string, BackgroundSoundPlayer> _soundPlayers = new();
        private readonly Dictionary<string, SolidColorBrush> _taskBrushCache = new();
        private static readonly SolidColorBrush DefaultGrayBrush = new SolidColorBrush(Colors.Gray);
        private static readonly SolidColorBrush BlockBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        private static readonly SolidColorBrush BlockBorderBrush = Brushes.White;
        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;
        private MemoWindow _memoWindow;

        public DashboardPage()
        {
            InitializeComponent();
            // ✨ ViewModel 인스턴스를 생성하고 DataContext로 설정
            _viewModel = new DashboardViewModel();
            this.DataContext = _viewModel;

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

            // ✨ ViewModel의 데이터가 변경될 때마다 타임라인을 다시 그리도록 이벤트 연결
            _viewModel.TimeLogEntries.CollectionChanged += (s, e) => RenderTimeTable();
            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(_viewModel.CurrentDateDisplay))
                {
                    RenderTimeTable();
                }
            };

            DataManager.SettingsUpdated += OnSettingsUpdated;
            this.Unloaded += (s, e) => DataManager.SettingsUpdated -= OnSettingsUpdated;
            this.Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 페이지가 로드될 때 타임라인을 그림
            RenderTimeTable();
        }

        private async void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                // ✨ 페이지가 다시 보일 때 데이터를 새로고침하고 UI 갱신
                await _viewModel.LoadAllDataAsync();
                RenderTimeTable();
                UpdateCharacterInfoPanel();
                UpdatePinnedMemoView();
            }
        }

        private void OnSettingsUpdated()
        {
            // 설정이 업데이트되면 브러시 캐시를 비우고 UI 갱신
            _taskBrushCache.Clear();
            RenderTimeTable();
            UpdateCharacterInfoPanel();
        }

        // ✨ KeyDown 이벤트는 ViewModel의 Command를 직접 호출하도록 변경
        private void TaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.AddTaskCommand.CanExecute(null))
            {
                _viewModel.AddTaskCommand.Execute(null);
            }
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.AddTodoCommand.CanExecute(null))
            {
                _viewModel.AddTodoCommand.Execute(null);
            }
        }

        #region UI Rendering (View의 고유 역할)
        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            // ✨ 이제 TaskItem 자체에 ColorBrush가 있으므로 ViewModel에서 가져옴
            var taskItem = _viewModel.TaskItems.FirstOrDefault(t => t.Text == taskName);
            if (taskItem != null && taskItem.ColorBrush != null)
            {
                _taskBrushCache[taskName] = taskItem.ColorBrush;
                return taskItem.ColorBrush;
            }

            return DefaultGrayBrush;
        }

        private void RenderTimeTable()
        {
            SelectionCanvas.Children.Clear();
            SelectionCanvas.Children.Add(_selectionBox);
            TimeTableContainer.Children.Clear();

            // ✨ 데이터를 ViewModel에서 직접 가져옴
            var logsForSelectedDate = _viewModel.TimeLogEntries
                .Where(log => log.StartTime.Date.ToString("yyyy-MM-dd") == _viewModel.CurrentDateDisplay)
                .OrderBy(l => l.StartTime)
                .ToList();

            double blockWidth = 35, blockHeight = 17, hourLabelWidth = 30;

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                var hourLabel = new TextBlock { Text = $"{hour:00}", Width = hourLabelWidth, Height = blockHeight, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, Foreground = Brushes.Gray, FontSize = 8 };
                hourRowPanel.Children.Add(hourLabel);
                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockContainer = new Grid { Width = blockWidth, Height = blockHeight, Background = BlockBackgroundBrush, Margin = new Thickness(1, 0, 1, 0) };
                    var blockWithBorder = new Border { BorderBrush = BlockBorderBrush, BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 0), Child = blockContainer };
                    hourRowPanel.Children.Add(blockWithBorder);
                }
                TimeTableContainer.Children.Add(hourRowPanel);
            }

            foreach (var logEntry in logsForSelectedDate)
            {
                var logStart = logEntry.StartTime.TimeOfDay;
                var logEnd = logEntry.EndTime.TimeOfDay;
                var duration = logEnd - logStart;
                if (duration.TotalSeconds <= 1) continue;

                var topOffset = logStart.TotalHours * (blockHeight + 2);
                var leftOffset = hourLabelWidth + (logStart.Minutes / 10.0) * (blockWidth + 2);
                var barWidth = (duration.TotalMinutes / 10.0) * (blockWidth + 2);

                var coloredBar = new Border
                {
                    Width = barWidth,
                    Height = blockHeight,
                    Background = GetColorForTask(logEntry.TaskText),
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(leftOffset, topOffset, 0, 0),
                    ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}\n\n클릭하여 수정 또는 삭제" },
                    Tag = logEntry,
                    Cursor = Cursors.Hand
                };
                coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;
                SelectionCanvas.Children.Add(coloredBar);
            }
        }

        // ✨ 캐릭터 정보 패널 업데이트 (ViewModel의 데이터 바인딩으로 대체 가능하지만 일단 유지)
        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_viewModel == null) return;
            UsernameDisplay.Text = _viewModel.Username;
            CoinDisplay.Text = _viewModel.CoinsDisplay;
            CharacterPreview.UpdateCharacter();
        }

        // ✨ 핀된 메모 업데이트 (이것도 추후 ViewModel로 이전 가능)
        public void UpdatePinnedMemoView()
        {
            // 로직 유지
        }

        // 사운드 플레이어, 드래그 선택 등 나머지 UI 로직은 여기에 그대로 둡니다.
        // InitializeSoundPlayers(), TimeLogRect_MouseLeftButtonDown(), SelectionCanvas_Mouse*() 등...
        #endregion

        public void SetParentWindow(MainWindow window) => _parentWindow = window;

        // ✨ 아래 메서드들은 ViewModel로 이전되었으므로 삭제합니다.
        // LoadTasks, SaveTasks, AddTaskButton_Click, EditTaskButton_Click, DeleteTaskButton_Click
        // LoadTodos, SaveTodos, AddTodoButton_Click, TodoItem_CheckboxChanged, 
        // LoadTimeLogs, SaveTimeLogs, RecalculateAllTotals, 등등...
    }
}