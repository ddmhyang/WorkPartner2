using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media; // Brushes를 사용하기 위해 추가

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        // [추가] 타이머 상태에 따른 브러쉬 정의
        private readonly SolidColorBrush _runningBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)) { Opacity = 0.6 };
        private readonly SolidColorBrush _stoppedBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.6 };

        public MiniTimerWindow()
        {
            InitializeComponent();
            // [추가] 초기 배경색 설정
            (this.Content as Border).Background = _stoppedBrush;
        }

        // 창 드래그 기능
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // 외부에서 시간을 업데이트하기 위한 메서드
        public void UpdateTime(string time)
        {
            TimeTextBlock.Text = time;
        }

        // [메서드 추가] 타이머 실행 중 스타일 적용
        public void SetRunningStyle()
        {
            (this.Content as Border).Background = _runningBrush;
        }

        // [메서드 추가] 타이머 멈춤 스타일 적용
        public void SetStoppedStyle()
        {
            (this.Content as Border).Background = _stoppedBrush;
        }
    }
}