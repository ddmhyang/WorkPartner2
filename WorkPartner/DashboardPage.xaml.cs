using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WorkPartner.ViewModels; // ViewModel 네임스페이스 추가

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        private MainWindow _parentWindow;
        private DashboardViewModel _viewModel; // ViewModel 인스턴스

        // ... (Other UI-related fields like _soundPlayers, _selectionBox etc. remain here)
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
            _viewModel = new DashboardViewModel();
            this.DataContext = _viewModel;

            InitializeSoundPlayers();
            // ... (Other initializations for UI elements)
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
            this.Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Re-render when the page is loaded
            await _viewModel.LoadAllDataAsync(); // ViewModel 데이터 로딩 호출
            RenderTimeTable();
        }

        // The IsVisibleChanged event can also trigger a refresh
        private async void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                await _viewModel.LoadAllDataAsync();
                RenderTimeTable();
                UpdateCharacterInfoPanel();
                UpdatePinnedMemoView();
            }
        }


        private void OnSettingsUpdated()
        {
            _viewModel.LoadSettings(); // ViewModel 설정 다시 로드
            _taskBrushCache.Clear();
            Dispatcher.Invoke(() =>
            {
                // TaskItems는 ViewModel에 있으므로, 브러시 업데이트 로직도 ViewModel이나 별도 서비스로 이동하는 것이 이상적
                foreach (var taskItem in _viewModel.TaskItems)
                {
                    if (_viewModel._settings.TaskColors.TryGetValue(taskItem.Text, out string colorHex))
                    {
                        taskItem.ColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    }
                }
                RenderTimeTable();
                UpdateCharacterInfoPanel();
            });
        }

        // AddTaskButton_Click, EditTaskButton_Click, etc. event handlers are now REMOVED
        // They are replaced by Commands in the ViewModel

        // KeyDown events can be handled using System.Windows.Interactivity or by calling the command
        private void TaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.AddTaskCommand.CanExecute(null))
                {
                    _viewModel.AddTaskCommand.Execute(null);
                }
            }
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.AddTodoCommand.CanExecute(null))
                {
                    _viewModel.AddTodoCommand.Execute(null);
                }
            }
        }

        #region UI Rendering (Stays in View)
        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            if (_viewModel._settings != null && _viewModel._settings.TaskColors.TryGetValue(taskName, out string colorHex))
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

        private void RenderTimeTable()
        {
            // This method now gets its data from _viewModel
            SelectionCanvas.Children.Clear(); // Clear old logs
            SelectionCanvas.Children.Add(_selectionBox); // Re-add selection box
            TimeTableContainer.Children.Clear();

            var logsForSelectedDate = _viewModel.TimeLogEntries
                .Where(log => log.StartTime.Date == _viewModel._currentDateForTimeline.Date)
                .OrderBy(l => l.StartTime)
                .ToList();

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

        // Other UI-specific methods like UpdateCharacterInfoPanel, UpdatePinnedMemoView, etc.
        // should also get their data from the _viewModel
        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_viewModel?._settings == null) return;
            UsernameDisplay.Text = _viewModel.Username;
            CoinDisplay.Text = _viewModel.CoinsDisplay;
            CharacterPreview.UpdateCharacter();
        }

        // ... The rest of the UI logic (drag selection, sound players, etc.) remains here ...
        // Make sure to call RenderTimeTable() and RecalculateAllTotals() after data changes.
        // For example, in the SelectionCanvas_MouseLeftButtonUp after a bulk edit.

        #endregion

        public void SetParentWindow(MainWindow window) => _parentWindow = window;
    }
}