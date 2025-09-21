// 파일: AnalysisPage.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using WorkPartner.AI;

namespace WorkPartner
{
    public partial class AnalysisPage : UserControl
    {
        private readonly string _timeLogFilePath = DataManager.TimeLogFilePath;
        private readonly string _tasksFilePath = DataManager.TasksFilePath;
        private List<TimeLogEntry> _allTimeLogs;
        private PredictionService _predictionService;

        public SeriesCollection HourAnalysisSeries { get; set; }
        public string[] HourLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public AnalysisPage()
        {
            InitializeComponent();
            _allTimeLogs = new List<TimeLogEntry>();
            HourAnalysisSeries = new SeriesCollection();
            _predictionService = new PredictionService();
            DataContext = this;

            InitializePredictionUI();
        }

        private void InitializePredictionUI()
        {
            DayOfWeekPredictionComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => ToKoreanDayOfWeek(d));
            HourPredictionComboBox.ItemsSource = Enumerable.Range(0, 24).Select(h => $"{h:00}시");

            DayOfWeekPredictionComboBox.SelectedItem = ToKoreanDayOfWeek(DateTime.Today.DayOfWeek);
            HourPredictionComboBox.SelectedItem = $"{DateTime.Now.Hour:00}시";
        }

        // [NEW] Public method to be called from MainWindow, fixing CS1061
        public async Task LoadAndAnalyzeData()
        {
            await LoadDataAsync();
            UpdateAllAnalyses();
        }

        private async void AnalysisPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                await LoadAndAnalyzeData();
            }
        }

        private async Task LoadDataAsync()
        {
            if (File.Exists(_timeLogFilePath))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(_timeLogFilePath))
                    {
                        _allTimeLogs = await JsonSerializer.DeserializeAsync<List<TimeLogEntry>>(stream) ?? new List<TimeLogEntry>();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading time logs: {ex.Message}");
                    _allTimeLogs = new List<TimeLogEntry>();
                }
            }
            if (File.Exists(_tasksFilePath))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(_tasksFilePath))
                    {
                        var tasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream) ?? new List<TaskItem>();
                        TaskPredictionComboBox.ItemsSource = tasks.Select(t => t.Text);
                        if (tasks.Any()) TaskPredictionComboBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading tasks: {ex.Message}");
                }
            }
        }

        private void UpdateAllAnalyses()
        {
            UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue);
            UpdateHourlyAnalysis();
            UpdateTaskFocusAnalysis();
            GenerateWorkRestPatternSuggestion();
        }

        private void UpdateTaskAnalysis(DateTime start, DateTime end)
        {
            var filteredLogs = _allTimeLogs.Where(log => log.StartTime >= start && log.StartTime <= end.AddDays(1).AddTicks(-1));
            var analysis = filteredLogs
                .GroupBy(log => log.TaskText)
                .Select(group => new TaskAnalysisResult
                {
                    TaskName = group.Key,
                    TotalTime = TimeSpan.FromSeconds(group.Sum(log => log.Duration.TotalSeconds))
                })
                .OrderByDescending(item => item.TotalTime)
                .ToList();

            TaskAnalysisGrid.ItemsSource = analysis;
        }

        private void UpdateHourlyAnalysis()
        {
            HourAnalysisSeries.Clear();
            var hourlyFocus = _allTimeLogs
                .Where(log => log.FocusScore > 0)
                .GroupBy(log => log.StartTime.Hour)
                .Select(g => new { Hour = g.Key, AvgFocus = g.Average(l => l.FocusScore) })
                .OrderBy(x => x.Hour)
                .ToList();

            var chartValues = new ChartValues<double>();
            var labels = new List<string>();

            for (int i = 0; i < 24; i++)
            {
                var data = hourlyFocus.FirstOrDefault(h => h.Hour == i);
                chartValues.Add(data?.AvgFocus ?? 0);
                labels.Add($"{i}시");
            }

            HourAnalysisSeries.Add(new LineSeries
            {
                Title = "평균 집중도",
                Values = chartValues,
                PointGeometry = null
            });

            HourLabels = labels.ToArray();
            YFormatter = value => value.ToString("N1");

            DataContext = null;
            DataContext = this;
        }

        private void UpdateTaskFocusAnalysis()
        {
            var analysis = _allTimeLogs
                .Where(log => log.FocusScore > 0)
                .GroupBy(log => log.TaskText)
                .Select(group => new TaskFocusAnalysisResult
                {
                    TaskName = group.Key,
                    AverageFocusScore = group.Average(log => log.FocusScore),
                    TotalTime = TimeSpan.FromSeconds(group.Sum(l => l.Duration.TotalSeconds))
                })
                .OrderByDescending(item => item.AverageFocusScore)
                .ToList();

            TaskFocusGrid.ItemsSource = analysis;
        }

        private void GenerateWorkRestPatternSuggestion()
        {
            if (_allTimeLogs.Count < 10)
            {
                WorkRestPatternSuggestionTextBlock.Text = "데이터가 더 필요합니다. 최소 10개 이상의 학습 기록이 쌓이면 분석을 제공합니다.";
                return;
            }

            var sessions = new List<WorkRestPattern>();
            var sortedLogs = _allTimeLogs.OrderBy(l => l.StartTime).ToList();

            for (int i = 0; i < sortedLogs.Count - 1; i++)
            {
                var currentLog = sortedLogs[i];
                var nextLog = sortedLogs[i + 1];

                if (currentLog.FocusScore > 0 && nextLog.FocusScore > 0)
                {
                    var restTime = nextLog.StartTime - currentLog.EndTime;
                    if (restTime.TotalMinutes > 1 && restTime.TotalHours < 2)
                    {
                        sessions.Add(new WorkRestPattern
                        {
                            WorkDurationMinutes = (int)currentLog.Duration.TotalMinutes,
                            RestDurationMinutes = (int)restTime.TotalMinutes,
                            NextSessionFocusScore = nextLog.FocusScore
                        });
                    }
                }
            }

            if (!sessions.Any())
            {
                WorkRestPatternSuggestionTextBlock.Text = "패턴을 분석할 충분한 휴식 데이터가 없습니다.";
                return;
            }

            var bestPattern = sessions
                .GroupBy(p => new { Work = RoundToNearest(p.WorkDurationMinutes, 10), Rest = RoundToNearest(p.RestDurationMinutes, 5) })
                .Select(g => new
                {
                    Pattern = g.Key,
                    AvgFocus = g.Average(p => p.NextSessionFocusScore),
                    Count = g.Count()
                })
                .Where(p => p.Count > 2)
                .OrderByDescending(p => p.AvgFocus)
                .FirstOrDefault();

            if (bestPattern != null)
            {
                WorkRestPatternSuggestionTextBlock.Text = $"가장 효과적인 패턴은 약 {bestPattern.Pattern.Work}분 학습 후 {bestPattern.Pattern.Rest}분 휴식하는 것입니다. (평균 집중도: {bestPattern.AvgFocus:F1}점)";
            }
            else
            {
                WorkRestPatternSuggestionTextBlock.Text = "뚜렷한 최적의 패턴을 찾지 못했습니다. 꾸준히 기록을 추가해주세요.";
            }
        }

        private int RoundToNearest(int number, int nearest)
        {
            return (int)(Math.Round(number / (double)nearest) * nearest);
        }

        private string ToKoreanDayOfWeek(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Sunday: return "일요일";
                case DayOfWeek.Monday: return "월요일";
                case DayOfWeek.Tuesday: return "화요일";
                case DayOfWeek.Wednesday: return "수요일";
                case DayOfWeek.Thursday: return "목요일";
                case DayOfWeek.Friday: return "금요일";
                case DayOfWeek.Saturday: return "토요일";
                default: return "";
            }
        }

        private DayOfWeek FromKoreanDayOfWeek(string day)
        {
            switch (day)
            {
                case "일요일": return DayOfWeek.Sunday;
                case "월요일": return DayOfWeek.Monday;
                case "화요일": return DayOfWeek.Tuesday;
                case "수요일": return DayOfWeek.Wednesday;
                case "목요일": return DayOfWeek.Thursday;
                case "금요일": return DayOfWeek.Friday;
                case "토요일": return DayOfWeek.Saturday;
                default: throw new ArgumentException("Invalid day of week");
            }
        }

        private void PredictButton_Click(object sender, RoutedEventArgs e)
        {
            var input = new ModelInput
            {
                TaskName = TaskPredictionComboBox.SelectedItem as string ?? "",
                DayOfWeek = (float)FromKoreanDayOfWeek(DayOfWeekPredictionComboBox.SelectedItem as string),
                Hour = (float)HourPredictionComboBox.SelectedIndex,
                Duration = 60
            };

            float prediction = _predictionService.Predict(input);
            PredictionResultTextBlock.Text = $"예측 집중도 점수: {prediction:F2} / 5.0";
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = (UIElement)sender;
                parent.RaiseEvent(eventArg);
            }
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; UpdateTaskAnalysis(today, today); }
        private void ThisWeekButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; int dayOfWeek = (int)today.DayOfWeek - (int)DayOfWeek.Monday; var startDate = today.AddDays(-dayOfWeek); var endDate = startDate.AddDays(6); UpdateTaskAnalysis(startDate, endDate); }
        private void ThisMonthButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; var startDate = new DateTime(today.Year, today.Month, 1); var endDate = startDate.AddMonths(1).AddDays(-1); UpdateTaskAnalysis(startDate, endDate); }
        private void TotalButton_Click(object sender, RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue); }
        private void CustomDateButton_Click(object sender, RoutedEventArgs e) { if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue) { UpdateTaskAnalysis(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value); } else { MessageBox.Show("시작 날짜와 종료 날짜를 모두 선택해주세요."); } }
    }

    public class TaskAnalysisResult { public string TaskName { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours} 시간 {TotalTime.Minutes} 분"; }
    public class WorkRestPattern { public int WorkDurationMinutes { get; set; } public int RestDurationMinutes { get; set; } public int NextSessionFocusScore { get; set; } }
    public class TaskFocusAnalysisResult { public string TaskName { get; set; } public double AverageFocusScore { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours}시간 {TotalTime.Minutes}분"; }
}

