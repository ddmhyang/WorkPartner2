using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json; // System.Text.Json 사용
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
            // 로그 파일 로드
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

            // 1. 이번 달 로그 필터링
            var monthLogs = _allLogs
                .Where(l => l.StartTime.Year == _currentMonth.Year && l.StartTime.Month == _currentMonth.Month)
                .ToList();

            // 2. 달력 그리기
            RenderCalendar(monthLogs);

            // 3. 과목별 통계 그리기
            RenderSubjectStats(monthLogs);
        }

        private void RenderCalendar(List<TimeLogEntry> monthLogs)
        {
            var daysList = new List<CalendarDayViewModel>();

            // 달력 시작일 계산 (해당 월 1일의 요일 맞추기)
            DateTime firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (int)firstDay.DayOfWeek; // 일=0, 월=1 ...
            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            // 빈 칸 채우기 (지난달)
            for (int i = 0; i < offset; i++)
            {
                daysList.Add(new CalendarDayViewModel { Day = "", Opacity = 0 });
            }

            // 날짜 채우기
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);

                // 그 날의 공부 시간 합산
                double totalMinutes = monthLogs
                    .Where(l => l.StartTime.Date == date)
                    .Sum(l => l.Duration.TotalMinutes);

                string timeText = totalMinutes > 0
                    ? TimeSpan.FromMinutes(totalMinutes).ToString(@"hh\:mm")
                    : "";

                daysList.Add(new CalendarDayViewModel
                {
                    Day = day.ToString(),
                    StudyTimeText = timeText,
                    HasStudyTime = totalMinutes > 0,
                    Opacity = 1.0
                });
            }

            // 남은 칸 채우기 (42칸 - 6주 기준)
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

                // 색상 가져오기
                SolidColorBrush brush = Brushes.Gray;
                if (_settings.TaskColors.TryGetValue(stat.Subject, out string hex))
                {
                    try { brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); } catch { }
                }

                viewModels.Add(new SubjectStatViewModel
                {
                    SubjectName = stat.Subject,
                    TimeText = TimeSpan.FromMinutes(stat.TotalMinutes).ToString(@"hh\:mm\:ss"),
                    ColorBrush = brush
                });
            }

            SubjectStatsList.ItemsSource = viewModels;
            MonthTotalText.Text = TimeSpan.FromMinutes(totalMonthMinutes).ToString(@"hh\:mm\:ss");
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

    // 뷰모델 클래스들
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