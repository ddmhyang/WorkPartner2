using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private readonly SolidColorBrush _runningBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)) { Opacity = 0.6 };
        private readonly SolidColorBrush _stoppedBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.6 };

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

        public void UpdateData(AppSettings settings, string taskName, string time)
        {
            // 1. 텍스트 정보 업데이트
            TimeTextBlock.Text = time;
            TaskTextBlock.Text = taskName;
            UsernameLevelTextBlock.Text = $"{settings.Username} (Lv.{settings.Level})";
            CoinAmountTextBlock.Text = settings.Coins.ToString("N0");

            // 2. 설정에 따라 UI 요소 보이기/숨기기 적용
            ApplySettings(settings);

            // 3. 캐릭터가 보일 경우에만 업데이트 (성능 최적화)
            if (settings.MiniTimerShowCharacter)
            {
                MiniCharacterDisplay.UpdateCharacter();
            }
        }

        public void SetRunningStyle()
        {
            (this.Content as Border).Background = _runningBrush;
        }

        public void SetStoppedStyle()
        {
            (this.Content as Border).Background = _stoppedBrush;
        }

        public void LoadSettings()
        {
            var settings = DataManager.LoadSettings(); // ✨ [수정] 지역 변수로 Load
            ApplySettings(settings); // ✨ [수정] Load한 설정을 ApplySettings로 전달
        }

        // ✨ [수정] AppSettings를 파라미터로 받도록 변경하고, 새 UI 요소를 제어합니다.
        private void ApplySettings(AppSettings settings)
        {
            // 배경화면 설정
            if (settings.MiniTimerShowBackground)
            {
                // TODO: 실제 이미지 경로로 변경해야 합니다.
                // BackgroundImage.Source = new BitmapImage(new Uri("pack://application:,,,/images/mini_timer_bg.png"));
                TimerBorder.Background = Brushes.Transparent;
            }
            else
            {
                BackgroundImage.Source = null;
                SetStoppedStyle();
            }

            // 캐릭터 표시 설정
            MiniCharacterDisplay.Visibility = settings.MiniTimerShowCharacter ? Visibility.Visible : Visibility.Collapsed;

            // 추가 정보(과목, 닉네임, 레벨, 코인) 표시 설정
            var infoVisibility = settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;
            TaskTextBlock.Visibility = infoVisibility;
            UsernameLevelTextBlock.Visibility = infoVisibility;
            CoinStackPanel.Visibility = infoVisibility;
        }
    }
}