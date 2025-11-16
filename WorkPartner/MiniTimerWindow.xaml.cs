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

            // 2. 설정에 따라 UI 요소 보이기/숨기기 적용 (ApplySettings가 동적 레이아웃을 처리)
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
            var settings = DataManager.LoadSettings();
            ApplySettings(settings);
        }

        // [핵심 수정] AppSettings를 파라미터로 받도록 변경하고, 동적 레이아웃 로직을 추가합니다.
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
                // [수정] 타이머 상태에 따라 배경색이 결정되므로 LoadSettings에서 SetStoppedStyle() 호출을 제거합니다.
                // MainWindow의 _timerService.IsRunning 값에 따라 결정되어야 합니다.
                // 우선순위가 아니므로 지금은 그대로 둡니다.
            }

            // --- [핵심 수정] 동적 레이아웃 로직 ---

            // 1. 현재 설정값을 변수로 가져옵니다.
            bool showInfo = settings.MiniTimerShowInfo;
            bool showChar = settings.MiniTimerShowCharacter;

            // 2. 설정값에 따라 기본 Visibility를 적용합니다.
            var infoVisibility = showInfo ? Visibility.Visible : Visibility.Collapsed;
            TaskTextBlock.Visibility = infoVisibility;
            UsernameLevelTextBlock.Visibility = infoVisibility;
            CoinStackPanel.Visibility = infoVisibility;
            MiniCharacterContainer.Visibility = showChar ? Visibility.Visible : Visibility.Collapsed;

            // 3. 조건에 따라 레이아웃 속성을 동적으로 변경합니다.
            if (!showInfo && !showChar)
            {
                // [Goal 2: 추가 정보 X, 캐릭터 X]
                // -> 시간을 창의 정중앙에 배치합니다.

                // InfoStackPanel이 Grid 0번, 1번 컬럼을 모두 차지하도록 설정
                Grid.SetColumnSpan(InfoStackPanel, 2);
                // InfoStackPanel 자체를 수평 중앙 정렬
                InfoStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                // TimeTextBlock 텍스트를 수평 중앙 정렬
                TimeTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                // 정렬에 방해가 되는 좌/우 마진을 제거
                InfoStackPanel.Margin = new Thickness(0);
                TimeTextBlock.FontSize = 24;

            }
            else
            {
                // [Goal 1: 추가 정보 X, 캐릭터 O] 또는 [기본 상태]
                // -> 시간을 왼쪽 영역(Column 0)에 배치합니다.

                // InfoStackPanel이 Grid 0번 컬럼만 차지하도록 되돌림
                Grid.SetColumnSpan(InfoStackPanel, 1);
                // InfoStackPanel 자체를 수평 왼쪽 정렬
                InfoStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                // TimeTextBlock 텍스트를 수평 왼쪽 정렬
                TimeTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                // 기본 마진으로 되돌림
                InfoStackPanel.Margin = new Thickness(20, 0, 10, 0);
                TimeTextBlock.FontSize = 20;

                // ※ Goal 1의 세로 중앙 정렬은 XAML에서 InfoStackPanel에 설정한
                //   VerticalAlignment="Center"가 자동으로 처리해줍니다.
            }
        }
    }
}