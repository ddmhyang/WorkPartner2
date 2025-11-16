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

            // ▼▼▼ [추가] 캐릭터 전용(MiniCharacterDisplay)은 항상 배경을 숨기도록 고정 ▼▼▼
            MiniCharacterDisplay.ForceHideBackground = true;

            // 2. BackgroundCharacterDisplay는 '배경만' 그리도록 (캐릭터 숨김)
            BackgroundCharacterDisplay.ForceShowOnlyBackground = true;
            // ▲▲▲
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
                // [수정] 캐릭터(앞) 업데이트
                MiniCharacterDisplay.UpdateCharacter(settings);
            }

            // ▼▼▼ [추가] 배경이 보일 경우 배경도 업데이트 ▼▼▼
            if (settings.MiniTimerShowBackground)
            {
                // [수정] 배경(뒤) 업데이트
                BackgroundCharacterDisplay.UpdateCharacter(settings);
            }

            if (settings.MiniTimerShowBackground)
            {
                // [수정] 배경(뒤) 업데이트
                BackgroundCharacterDisplay.UpdateCharacter(settings);
            }
            // ▲▲▲
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
            // ▼▼▼ [수정] 배경화면 로직 전체를 교체합니다. ▼▼▼

            if (settings.MiniTimerShowBackground)
            {
                // 1. [설정 ON] 배경 전용(BackgroundCharacterDisplay) 컨트롤을 켭니다.
                BackgroundCharacterDisplay.Visibility = Visibility.Visible;
                // (이 컨트롤은 ForceShowOnlyBackground=true 이므로 배경만 그림)

                // 2. 타이머 창의 테두리 배경은 투명하게 만들어 아바타 배경이 보이게 함
                TimerBorder.Background = Brushes.Transparent;
            }
            else
            {
                // 1. [설정 OFF] 배경 전용(BackgroundCharacterDisplay) 컨트롤을 끕니다.
                BackgroundCharacterDisplay.Visibility = Visibility.Collapsed;

                // 3. 타이머 창의 테두리 배경을 기본 불투명(Stopped) 상태로 되돌림
                SetStoppedStyle();
            }


            // 캐릭터 표시 설정
            MiniCharacterContainer.Visibility = settings.MiniTimerShowCharacter ? Visibility.Visible : Visibility.Collapsed;

            // 추가 정보(과목, 닉네임, 레벨, 코인) 표시 설정
            bool showInfo = settings.MiniTimerShowInfo;
            bool showChar = settings.MiniTimerShowCharacter;

            var infoVisibility = showInfo ? Visibility.Visible : Visibility.Collapsed;
            TaskTextBlock.Visibility = infoVisibility;
            UsernameLevelTextBlock.Visibility = infoVisibility;
            CoinStackPanel.Visibility = infoVisibility;

            // 3. 조건에 따라 레이아웃 속성을 동적으로 변경합니다.
            if (!showInfo && !showChar)
            {
                // (Goal 2: 둘 다 숨김)
                Grid.SetColumnSpan(InfoStackPanel, 2);
                InfoStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                TimeTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                InfoStackPanel.Margin = new Thickness(0);
                TimeTextBlock.FontSize = 32;
            }
            else
            {
                // (Goal 1 / 기본 상태)
                Grid.SetColumnSpan(InfoStackPanel, 1);
                InfoStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                TimeTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                InfoStackPanel.Margin = new Thickness(10, 0, 10, 0);
                TimeTextBlock.FontSize = 20;
            }
        }
    }
}