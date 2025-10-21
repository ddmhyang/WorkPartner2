﻿// 파일: AnalysisPage.xaml.cs
using System;
using System.Collections.Generic;
using System.ComponentModel; // INotifyPropertyChanged 사용 위해 필요
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices; // CallerMemberName 사용 위해 필요
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using WorkPartner.AI; // PredictionService, ModelInput 등이 정의된 네임스페이스

namespace WorkPartner
{
    // INotifyPropertyChanged 인터페이스 구현
    public partial class AnalysisPage : UserControl, INotifyPropertyChanged
    {
        // --- 멤버 변수 ---
        private readonly string _timeLogFilePath = DataManager.TimeLogFilePath;
        private readonly string _tasksFilePath = DataManager.TasksFilePath;
        private List<TimeLogEntry> _allTimeLogs;
        private bool _isDataLoaded = false;
        private PredictionService _predictionService;

        // --- 차트 바인딩 속성 ---

        // 시간대별 집중도 (라인)
        private SeriesCollection _hourAnalysisSeries;
        public SeriesCollection HourAnalysisSeries
        {
            get => _hourAnalysisSeries;
            set => SetProperty(ref _hourAnalysisSeries, value);
        }

        // 시간대별 X축 레이블 (공유)
        private string[] _hourLabels;
        public string[] HourLabels
        {
            get => _hourLabels;
            set => SetProperty(ref _hourLabels, value);
        }

        // 집중도 Y축 포맷터 (소수점)
        public Func<double, string> YFormatter { get; set; }

        // 집중도 분포 (세로 막대)
        private SeriesCollection _focusDistributionSeries;
        public SeriesCollection FocusDistributionSeries
        {
            get => _focusDistributionSeries;
            set => SetProperty(ref _focusDistributionSeries, value);
        }

        // 집중도 분포 X축 레이블
        private string[] _focusDistributionLabels;
        public string[] FocusDistributionLabels
        {
            get => _focusDistributionLabels;
            set => SetProperty(ref _focusDistributionLabels, value);
        }

        // 시간대별 학습 시간 (가로 막대)
        private SeriesCollection _hourlyTimeSeries;
        public SeriesCollection HourlyTimeSeries
        {
            get => _hourlyTimeSeries;
            set => SetProperty(ref _hourlyTimeSeries, value);
        }

        // 정수 Y축 포맷터
        public Func<double, string> YFormatterInt { get; set; }


        // --- 생성자 ---
        public AnalysisPage()
        {
            InitializeComponent();
            _allTimeLogs = new List<TimeLogEntry>();
            _predictionService = new PredictionService();

            // 속성 초기화
            HourAnalysisSeries = new SeriesCollection();
            FocusDistributionSeries = new SeriesCollection();
            HourlyTimeSeries = new SeriesCollection();

            YFormatter = value => value.ToString("N1"); // 소수점 1자리
            YFormatterInt = value => ((int)value).ToString(); // 정수

            DataContext = this; // 데이터 바인딩 컨텍스트 설정
            InitializePredictionUI();
        }

        // --- 초기화 메서드 ---
        private void InitializePredictionUI()
        {
            DayOfWeekPredictionComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => ToKoreanDayOfWeek(d));
            HourPredictionComboBox.ItemsSource = Enumerable.Range(0, 24).Select(h => $"{h:00}시");

            DayOfWeekPredictionComboBox.SelectedItem = ToKoreanDayOfWeek(DateTime.Today.DayOfWeek);
            HourPredictionComboBox.SelectedItem = $"{DateTime.Now.Hour:00}시";
        }

        // --- 데이터 로딩 및 분석 ---
        public async Task LoadAndAnalyzeData()
        {
            await LoadDataAsync();
            UpdateAllAnalyses();
        }

        private async void AnalysisPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true && !_isDataLoaded)
            {
                await LoadAndAnalyzeData();
                _isDataLoaded = true;
            }
            else if (e.NewValue is true && _isDataLoaded)
            {
                UpdateAllAnalyses(); // 페이지가 다시 보일 때마다 업데이트
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
                    MessageBox.Show($"시간 기록 로딩 오류: {ex.Message}");
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
                    MessageBox.Show($"과목 목록 로딩 오류: {ex.Message}");
                }
            }
        }

        private void UpdateAllAnalyses()
        {
            UpdateTotalStudyTime();
            UpdateTaskAnalysis();      // 과목별 시간 (탭 1)
            UpdateTaskFocusAnalysis(); // 집중도 분석 (탭 2)
            UpdateHourlyAnalysis();    // 시간대별 분석 (탭 3)
            GenerateWorkRestPatternSuggestion(); // AI 제안
        }

        // --- 개별 분석 메서드 ---
        private void UpdateTotalStudyTime()
        {
            TimeSpan totalDuration = TimeSpan.FromSeconds(_allTimeLogs.Sum(log => log.Duration.TotalSeconds));
            int days = (int)totalDuration.TotalDays;
            int hours = totalDuration.Hours;
            int minutes = totalDuration.Minutes;
            int seconds = totalDuration.Seconds;
            TotalStudyTimeTextBlock.Text = $"총 학습 시간: {days}일 {hours}시간 {minutes}분 {seconds}초";
        }

        private void UpdateTaskAnalysis() // 탭 1: 과목별 학습 시간 표
        {
            var analysis = _allTimeLogs
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

        private void UpdateTaskFocusAnalysis() // 탭 2: 집중도 분석 (평균, 분포, 과목별 표)
        {
            var validLogs = _allTimeLogs.Where(log => log.FocusScore > 0).ToList();

            // 전체 평균 집중도
            double overallAvgFocus = validLogs.Any() ? validLogs.Average(log => log.FocusScore) : 0;
            AverageFocusScoreTextBlock.Text = overallAvgFocus.ToString("F1");

            // 집중도 점수 분포 (세로 막대 차트)
            var localFocusDistributionSeries = new SeriesCollection();
            var focusCounts = validLogs
                .GroupBy(log => log.FocusScore)
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .OrderBy(x => x.Score)
                .ToList();

            var distributionValues = new ChartValues<double>();
            var distributionLabels = new List<string>();

            for (int score = 1; score <= 5; score++)
            {
                var data = focusCounts.FirstOrDefault(c => c.Score == score);
                distributionValues.Add(data?.Count ?? 0);
                distributionLabels.Add($"{score}점");
            }

            localFocusDistributionSeries.Add(new ColumnSeries
            {
                Title = "횟수",
                Values = distributionValues,
                DataLabels = true
            });
            // 속성 업데이트 (SetProperty 호출 -> UI 갱신)
            FocusDistributionSeries = localFocusDistributionSeries;
            FocusDistributionLabels = distributionLabels.ToArray();

            // 과목별 평균 집중도 (표)
            var taskFocusAnalysis = validLogs
                .GroupBy(log => log.TaskText)
                .Select(group => new TaskFocusAnalysisResult
                {
                    TaskName = group.Key,
                    AverageFocusScore = group.Average(log => log.FocusScore),
                    TotalTime = TimeSpan.FromSeconds(group.Sum(l => l.Duration.TotalSeconds))
                })
                .OrderByDescending(item => item.AverageFocusScore)
                .ToList();
            TaskFocusGrid.ItemsSource = taskFocusAnalysis;
        }

        private void UpdateHourlyAnalysis() // 탭 3: 시간대별 분석 (집중도 라인, 학습 시간 막대)
        {
            // 시간대별 평균 집중도 (라인 차트)
            var localHourAnalysisSeries = new SeriesCollection();
            var hourlyFocus = _allTimeLogs
                .Where(log => log.FocusScore > 0)
                .GroupBy(log => log.StartTime.Hour)
                .Select(g => new { Hour = g.Key, AvgFocus = g.Average(l => l.FocusScore) })
                .OrderBy(x => x.Hour)
                .ToList();

            var focusChartValues = new ChartValues<double>();
            var labels = new List<string>(); // X축 레이블 공유

            for (int i = 0; i < 24; i++)
            {
                var focusData = hourlyFocus.FirstOrDefault(h => h.Hour == i);
                focusChartValues.Add(focusData?.AvgFocus ?? 0);
                labels.Add($"{i}시");
            }

            localHourAnalysisSeries.Add(new LineSeries
            {
                Title = "평균 집중도",
                Values = focusChartValues,
                PointGeometry = null
            });
            // 속성 업데이트 (SetProperty 호출 -> UI 갱신)
            HourAnalysisSeries = localHourAnalysisSeries;
            HourLabels = labels.ToArray(); // 시간대별 학습 시간 차트와 Y축 레이블 공유

            // 시간대별 총 학습 시간 (가로 막대 차트)
            var localHourlyTimeSeries = new SeriesCollection();
            var hourlyTime = _allTimeLogs
                .GroupBy(log => log.StartTime.Hour)
                .Select(g => new { Hour = g.Key, TotalMinutes = g.Sum(l => l.Duration.TotalMinutes) })
                .OrderBy(x => x.Hour)
                .ToList();

            var timeChartValues = new ChartValues<double>();

            for (int i = 0; i < 24; i++)
            {
                var timeData = hourlyTime.FirstOrDefault(h => h.Hour == i);
                timeChartValues.Add(timeData?.TotalMinutes ?? 0);
            }

            localHourlyTimeSeries.Add(new RowSeries
            {
                Title = "학습 시간(분)",
                Values = timeChartValues,
                DataLabels = true
            });
            // 속성 업데이트 (SetProperty 호출 -> UI 갱신)
            HourlyTimeSeries = localHourlyTimeSeries;
        }

        private void GenerateWorkRestPatternSuggestion() // AI 제안
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

                if (currentLog.FocusScore > 0 && nextLog.FocusScore > 0) // 집중도 점수가 있는 기록만 고려
                {
                    var restTime = nextLog.StartTime - currentLog.EndTime;
                    // 유효한 휴식 시간 범위 (1분 초과, 2시간 미만)
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

            // 작업 시간은 10분 단위, 휴식 시간은 5분 단위로 그룹화하여 평균 집중도 계산
            var bestPattern = sessions
                .GroupBy(p => new { Work = RoundToNearest(p.WorkDurationMinutes, 10), Rest = RoundToNearest(p.RestDurationMinutes, 5) })
                .Select(g => new
                {
                    Pattern = g.Key,
                    AvgFocus = g.Average(p => p.NextSessionFocusScore),
                    Count = g.Count()
                })
                .Where(p => p.Count > 2) // 최소 3번 이상 나타난 패턴만 고려
                .OrderByDescending(p => p.AvgFocus)
                .FirstOrDefault();

            if (bestPattern != null)
            {
                WorkRestPatternSuggestionTextBlock.Text = $"가장 효과적인 패턴은 약 {bestPattern.Pattern.Work}분 학습 후 {bestPattern.Pattern.Rest}분 휴식하는 것입니다. (평균 집중도: {bestPattern.AvgFocus:F1}점, 빈도: {bestPattern.Count}회)";
            }
            else
            {
                WorkRestPatternSuggestionTextBlock.Text = "뚜렷한 최적의 패턴을 찾지 못했습니다. 꾸준히 기록을 추가해주세요.";
            }
        }

        // --- 유틸리티 메서드 ---
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

        // --- 이벤트 핸들러 ---
        private void PredictButton_Click(object sender, RoutedEventArgs e) // AI 예측
        {
            if (TaskPredictionComboBox.SelectedItem == null ||
                DayOfWeekPredictionComboBox.SelectedItem == null ||
                HourPredictionComboBox.SelectedItem == null)
            {
                PredictionResultTextBlock.Text = "과목, 요일, 시간을 모두 선택해주세요.";
                return;
            }

            try
            {
                var input = new ModelInput
                {
                    TaskName = TaskPredictionComboBox.SelectedItem as string ?? "",
                    DayOfWeek = (float)FromKoreanDayOfWeek(DayOfWeekPredictionComboBox.SelectedItem as string),
                    Hour = (float)HourPredictionComboBox.SelectedIndex, // 0-23
                    Duration = 60 // 예측 기준 시간 (예: 60분)
                };

                float prediction = _predictionService.Predict(input);
                // 예측 결과 범위 보정 (0~5점)
                prediction = Math.Max(0, Math.Min(5, prediction));
                PredictionResultTextBlock.Text = $"예측 집중도 점수: {prediction:F2} / 5.0";
            }
            catch (Exception ex)
            {
                PredictionResultTextBlock.Text = $"예측 중 오류 발생: {ex.Message}";
            }
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e) // 스크롤 이벤트 처리
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                if (sender is UIElement parent) // 타입 캐스팅 추가
                {
                    parent.RaiseEvent(eventArg);
                }
            }
        }


        #region INotifyPropertyChanged 구현
        // 이 이벤트는 인터페이스 요구사항입니다.
        public event PropertyChangedEventHandler PropertyChanged;

        // 속성 값이 변경될 때 이 메서드를 호출하여 UI에 알립니다.
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 속성 set 접근자에서 사용되는 헬퍼 메서드입니다.
        // 값이 실제로 변경되었는지 확인하고 변경된 경우에만 NotifyPropertyChanged를 호출합니다.
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false; // 값이 같으면 아무것도 안 함
            field = value; // 값을 업데이트
            NotifyPropertyChanged(propertyName); // UI에 변경 알림
            return true;
        }
        #endregion

    } // <- AnalysisPage 클래스 닫는 괄호

    // --- 도우미 클래스 정의 ---
    public class TaskAnalysisResult { public string TaskName { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours} 시간 {TotalTime.Minutes} 분"; }
    public class WorkRestPattern { public int WorkDurationMinutes { get; set; } public int RestDurationMinutes { get; set; } public int NextSessionFocusScore { get; set; } }
    public class TaskFocusAnalysisResult { public string TaskName { get; set; } public double AverageFocusScore { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours}시간 {TotalTime.Minutes}분"; }

} // <- WorkPartner 네임스페이스 닫는 괄호