using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging; // BitmapImage를 사용하기 위해 추가
using System; // Uri를 사용하기 위해 추가

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

        public void UpdateTime(string time)
        {
            TimeTextBlock.Text = time;
        }

        // ✨ 과목 정보를 업데이트하는 메서드
        public void UpdateTaskInfo(string taskName)
        {
            InfoTextBlock.Text = taskName;
        }

        public void SetRunningStyle()
        {
            (this.Content as Border).Background = _runningBrush;
        }

        public void SetStoppedStyle()
        {
            (this.Content as Border).Background = _stoppedBrush;
        }

        // ✨ 설정을 불러와 UI에 적용하는 메서드
        public void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
            ApplySettings();
        }

        // ✨ 현재 설정에 맞게 UI를 변경하는 메서드
        private void ApplySettings()
        {
            // 배경화면 설정
            if (_settings.MiniTimerShowBackground)
            {
                // TODO: 실제 이미지 경로로 변경해야 합니다.
                // BackgroundImage.Source = new BitmapImage(new Uri("pack://application:,,,/images/mini_timer_bg.png"));
                TimerBorder.Background = Brushes.Transparent; // 이미지를 보여주기 위해 배경 투명 처리
            }
            else
            {
                BackgroundImage.Source = null;
                SetStoppedStyle(); // 기본 배경색으로 복원
            }

            // 캐릭터 표시 설정
            MiniCharacterDisplay.Visibility = _settings.MiniTimerShowCharacter ? Visibility.Visible : Visibility.Collapsed;
            if (_settings.MiniTimerShowCharacter) MiniCharacterDisplay.UpdateCharacter();

            // 추가 정보(과목) 표시 설정
            InfoTextBlock.Visibility = _settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}