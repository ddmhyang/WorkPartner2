using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System;
using WorkPartner.ViewModels; // ViewModel을 직접 참조하기 위해 추가

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private readonly SolidColorBrush _runningBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)) { Opacity = 0.6 };
        private readonly SolidColorBrush _stoppedBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.6 };
        private AppSettings _settings;

        public MiniTimerWindow()
        {
            InitializeComponent();
            (this.Content as Border).Background = _stoppedBrush;
            LoadSettings();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // ✨ 모든 정보를 한 번에 업데이트하는 통합 메서드
        public void UpdateDisplay(string time, string taskName, bool isRunning, DashboardViewModel viewModel)
        {
            // 시간 및 과목 정보 업데이트
            TimeTextBlock.Text = time;
            InfoTextBlock.Text = taskName ?? "선택된 과목 없음";

            // 사용자 정보 업데이트 (닉네임, 레벨, 재화)
            NicknameTextBlock.Text = viewModel.Nickname;
            LevelTextBlock.Text = $"Lv.{viewModel.Level}";
            MoneyTextBlock.Text = viewModel.Points.ToString("N0");

            // 실행 상태에 따른 스타일 변경
            if (isRunning)
            {
                SetRunningStyle();
            }
            else
            {
                SetStoppedStyle();
            }
        }

        public void SetRunningStyle()
        {
            if (!_settings.MiniTimerShowBackground)
                (this.Content as Border).Background = _runningBrush;
        }

        public void SetStoppedStyle()
        {
            if (!_settings.MiniTimerShowBackground)
                (this.Content as Border).Background = _stoppedBrush;
        }

        public void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (_settings.MiniTimerShowBackground)
            {
                // BackgroundImage.Source = new BitmapImage(new Uri("pack://application:,,,/images/mini_timer_bg.png"));
                TimerBorder.Background = Brushes.Transparent;
            }
            else
            {
                BackgroundImage.Source = null;
                SetStoppedStyle();
            }

            MiniCharacterDisplay.Visibility = _settings.MiniTimerShowCharacter ? Visibility.Visible : Visibility.Collapsed;
            if (_settings.MiniTimerShowCharacter) MiniCharacterDisplay.UpdateCharacter();

            InfoTextBlock.Visibility = _settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

