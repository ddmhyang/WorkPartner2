using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private string _lastLoadedImagePath = null;

        public MiniTimerWindow()
        {
            InitializeComponent();
            LoadSettings();
            DataManager.SettingsUpdated += OnSettingsUpdated;
            this.Closed += MiniTimerWindow_Closed;
        }

        // ▼▼▼ [추가] 설정 변경 알림이 오면 실행되는 메서드 ▼▼▼
        private void OnSettingsUpdated()
        {
            // 다른 스레드에서 올 수 있으므로 Dispatcher 사용 (안전하게 UI 갱신)
            Dispatcher.Invoke(() =>
            {
                LoadSettings();
            });
        }

        private void MiniTimerWindow_Closed(object sender, EventArgs e)
        {
            // 창이 닫힐 때 이벤트 연결 끊기 (메모리 누수 방지)
            DataManager.SettingsUpdated -= OnSettingsUpdated;
        }

        private void LoadSettings()
        {
            var settings = DataManager.LoadSettings();
            UpdateUI(settings);
        }

        public void UpdateData(AppSettings settings, string currentTask, string time)
        {
            CurrentTaskText.Text = $"현재: {currentTask}";
            TimeText.Text = time;
            UpdateUI(settings);
        }

        private void UpdateUI(AppSettings settings)
        {
            if (settings == null) return;

            if (CurrentTaskText != null)
                CurrentTaskText.Visibility = settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;

            if (KeyringArea != null && MiniProfileImage != null)
            {
                if (settings.MiniTimerShowCharacter)
                {
                    KeyringArea.Visibility = Visibility.Visible;

                    // 이미지 로드
                    if (_lastLoadedImagePath != settings.UserImagePath)
                    {
                        _lastLoadedImagePath = settings.UserImagePath;
                        GifHelper.PlayGif(MiniProfileImage, _lastLoadedImagePath);
                    }

                    // ▼▼▼ [수정] 여기서 자동 실행하던 sb.Begin() 코드를 삭제했습니다.
                    // 이제 평소에는 가만히 있습니다.
                }
                else
                {
                    KeyringArea.Visibility = Visibility.Collapsed;
                    GifHelper.StopGif(MiniProfileImage);
                    _lastLoadedImagePath = null;

                    // 혹시 실행 중이었다면 정지
                    var sb = this.Resources["SwingAnimation"] as Storyboard;
                    sb?.Stop();
                }
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                // 1. 이동 중 애니메이션 (격하게 흔들림) 시작
                var swingSb = this.Resources["SwingAnimation"] as Storyboard;
                var decaySb = this.Resources["DecayAnimation"] as Storyboard;

                // 기존 감속 애니메이션이 있다면 멈춤
                decaySb?.Stop();
                swingSb?.Begin();

                // 2. 창 이동 (마우스를 뗄 때까지 여기서 코드 실행이 멈춥니다)
                this.DragMove();

                // 3. 드래그가 끝남 (마우스 뗌)
                if (swingSb != null && decaySb != null)
                {
                    // (1) 현재 흔들리고 있는 각도를 알아내기 위해 잠시 일시정지
                    swingSb.Pause();

                    // (2) 현재 각도 가져오기
                    double currentAngle = KeyringTransform.Angle;

                    // (3) 흔들림 애니메이션 완전 종료
                    swingSb.Stop();

                    // (4) 감속 애니메이션 설정: "현재 각도"에서부터 "0도"로
                    // DecayDoubleAnim은 XAML에서 이름을 지어준 애니메이션 객체입니다.
                    if (this.FindName("DecayDoubleAnim") is DoubleAnimation decayAnim)
                    {
                        decayAnim.From = currentAngle;
                    }

                    // (5) 감속 애니메이션 시작 (띠용~ 하며 멈춤)
                    decaySb.Begin();
                }
            }
        }
    }
}