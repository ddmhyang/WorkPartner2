using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WorkPartner.ViewModels;

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        private MainWindow _parentWindow;
        private readonly DashboardViewModel _viewModel;

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

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadAllDataAsync();
            RenderTimeTable();
            UpdatePinnedMemoView();
        }

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

        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not TimeLogEntry log) return;

            var win = new AddLogWindow(_viewModel.TaskItems, log) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            if (win.IsDeleted)
            {
                _viewModel.TimeLogEntries.Remove(log);
            }
            else
            {
                log.StartTime = win.NewLogEntry.StartTime;
                log.EndTime = win.NewLogEntry.EndTime;
                log.TaskText = win.NewLogEntry.TaskText;
                log.FocusScore = win.NewLogEntry.FocusScore;
            }

            DataManager.SaveTimeLogs(_viewModel.TimeLogEntries);
            _viewModel.RecalculateAllTotals();
            RenderTimeTable();
        }

        private void ClosePinnedMemo_Click(object sender, RoutedEventArgs e)
        {
            PinnedMemoPanel.Visibility = Visibility.Collapsed;
        }

        private void OnSettingsUpdated()
        {
            _taskBrushCache.Clear();
            RenderTimeTable();
            UpdateCharacterInfoPanel();
        }

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

        private void InitializeSoundPlayers()
        {
            // Placeholder
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_taskBrushCache.TryGetValue(taskName, out var cachedBrush))
            {
                return cachedBrush;
            }

            var taskItem = _viewModel.TaskItems.FirstOrDefault(t => t.Text == taskName);
            if (taskItem?.ColorBrush is SolidColorBrush solidColorBrush)
            {
                _taskBrushCache[taskName] = solidColorBrush;
                return solidColorBrush;
            }

            return DefaultGrayBrush;
        }

        private void RenderTimeTable()
        {
            SelectionCanvas.Children.Clear();
            SelectionCanvas.Children.Add(_selectionBox);
            TimeTableContainer.Children.Clear();

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

        private void UpdateCharacterInfoPanel(string status = null)
        {
            if (_viewModel == null) return;
            UsernameDisplay.Text = _viewModel.Username;
            CoinDisplay.Text = _viewModel.CoinsDisplay;
            CharacterPreview.UpdateCharacter();
        }

        public void UpdatePinnedMemoView()
        {
            var pinnedMemo = _viewModel.AllMemos.FirstOrDefault(m => m.IsPinned);
            if (pinnedMemo != null)
            {
                PinnedMemoPanel.Visibility = Visibility.Visible;
                PinnedMemoContent.Text = pinnedMemo.Content;
            }
            else
            {
                PinnedMemoPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void SetParentWindow(MainWindow window) => _parentWindow = window;
    }
}