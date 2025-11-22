using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class StatisticsPage : UserControl
    {
        private DateTime _currentMonth;
        private List<TimeLogEntry> _allLogs = new List<TimeLogEntry>();
        private AppSettings _settings;

        public StatisticsPage()
        {
            InitializeComponent();
            _currentMonth = DateTime.Today;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = DataManager.LoadSettings();
            await LoadLogsAsync();
            RenderPage();
        }

        private async Task LoadLogsAsync()
        {
            string path = DataManager.TimeLogFilePath;
            if (File.Exists(path))
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    _allLogs = await JsonSerializer.DeserializeAsync<List<TimeLogEntry>>(stream) ?? new List<TimeLogEntry>();
                }
                catch { _allLogs = new List<TimeLogEntry>(); }
            }
        }

        private void RenderPage()
        {
            CurrentMonthText.Text = _currentMonth.ToString("yyyy년 MM월");

            var monthLogs = _allLogs
                .Where(l => l.StartTime.Year == _currentMonth.Year && l.StartTime.Month == _currentMonth.Month)
                .ToList();

            RenderCalendar(monthLogs);
            RenderSubjectStats(monthLogs);
        }

        // ▼▼▼ [수정] 포맷을 "00:00:00" 형태로 변경 (두 자리수 맞춤) ▼▼▼
        private string FormatTotalHours(TimeSpan ts)
        {
            // 예: 9시간 -> "09:30:00", 117시간 -> "117:30:00"
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private void RenderCalendar(List<TimeLogEntry> monthLogs)
        {
            var daysList = new List<CalendarDayViewModel>();

            DateTime firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            for (int i = 0; i < offset; i++)
            {
                daysList.Add(new CalendarDayViewModel { Day = "", Opacity = 0 });
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);

                double totalMinutes = monthLogs
                    .Where(l => l.StartTime.Date == date)
                    .Sum(l => l.Duration.TotalMinutes);

                string timeText = "";
                if (totalMinutes > 0)
                {
                    var ts = TimeSpan.FromMinutes(totalMinutes);
                    // ▼▼▼ [수정] 달력 시간도 "00:00" 형태로 두 자리 맞춤 ▼▼▼
                    timeText = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}";
                }

                daysList.Add(new CalendarDayViewModel
                {
                    Day = day.ToString(),
                    StudyTimeText = timeText,
                    HasStudyTime = totalMinutes > 0,
                    Opacity = 1.0
                });
            }

            while (daysList.Count < 42)
            {
                daysList.Add(new CalendarDayViewModel { Day = "", Opacity = 0 });
            }

            CalendarGrid.ItemsSource = daysList;
        }

        private void RenderSubjectStats(List<TimeLogEntry> monthLogs)
        {
            var stats = monthLogs
                .GroupBy(l => l.TaskText)
                .Select(g => new
                {
                    Subject = g.Key,
                    TotalMinutes = g.Sum(l => l.Duration.TotalMinutes)
                })
                .OrderByDescending(x => x.TotalMinutes)
                .ToList();

            var viewModels = new List<SubjectStatViewModel>();
            double totalMonthMinutes = 0;

            foreach (var stat in stats)
            {
                totalMonthMinutes += stat.TotalMinutes;

                SolidColorBrush brush = Brushes.Gray;
                if (_settings.TaskColors.TryGetValue(stat.Subject, out string hex))
                {
                    try { brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); } catch { }
                }

                viewModels.Add(new SubjectStatViewModel
                {
                    SubjectName = stat.Subject,
                    TimeText = FormatTotalHours(TimeSpan.FromMinutes(stat.TotalMinutes)), // 여기서 수정된 포맷 적용됨
                    ColorBrush = brush
                });
            }

            SubjectStatsList.ItemsSource = viewModels;
            MonthTotalText.Text = FormatTotalHours(TimeSpan.FromMinutes(totalMonthMinutes)); // 여기서 수정된 포맷 적용됨
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            RenderPage();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            RenderPage();
        }
    }

    public class CalendarDayViewModel
    {
        public string Day { get; set; }
        public string StudyTimeText { get; set; }
        public bool HasStudyTime { get; set; }
        public double Opacity { get; set; }
    }

    public class SubjectStatViewModel
    {
        public string SubjectName { get; set; }
        public string TimeText { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
    }
}