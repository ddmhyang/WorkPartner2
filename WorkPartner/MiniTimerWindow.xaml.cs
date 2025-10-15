using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private DispatcherTimer _timer;
        private string _currentTask;
        private bool _isWorkTime = true;

        public MiniTimerWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // DashboardViewModel에서 현재 작업 이름을 받아옵니다.
        public void SetCurrentTask(string task)
        {
            _currentTask = task;
        }

        // 1초마다 UI를 업데이트합니다.
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTask))
            {
                TimerDisplay.Text = "00:00:00";
                return;
            }

            var timeLogs = DataManager.LoadTimeLogs(DateTime.Now); // DataManager 직접 사용
            var currentTaskLog = timeLogs.FirstOrDefault(t => t.TaskName == _currentTask);

            if (currentTaskLog != null)
            {
                var totalSeconds = currentTaskLog.TimeSegments.Sum(s => s.Value);
                TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
                TimerDisplay.Text = $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            else
            {
                TimerDisplay.Text = "00:00:00";
            }
        }

        public void UpdateTimerState(bool isWorking, string task)
        {
            _isWorkTime = isWorking;
            _currentTask = task;

            if (!_isWorkTime)
            {
                TimerDisplay.Text = "00:00:00";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}