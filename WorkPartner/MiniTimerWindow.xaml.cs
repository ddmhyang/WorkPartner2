using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private string _lastLoadedImagePath = null;

        public MiniTimerWindow()
        {
            InitializeComponent();

            // 1. 초기 설정 로드
            LoadSettings();

            // ▼▼▼ [추가] 설정이 변경되면 즉시 알림을 받도록 등록 ▼▼▼
            DataManager.SettingsUpdated += OnSettingsUpdated;

            // 윈도우가 닫힐 때 이벤트 연결 해제를 위해 Closed 이벤트 추가
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

            // 1. 배경 설정
            this.Background = settings.MiniTimerShowBackground
                ? (Brush)FindResource("MainBackgroundBrush")
                : Brushes.Transparent;

            // 2. 정보 텍스트 On/Off
            if (CurrentTaskText != null)
            {
                CurrentTaskText.Visibility = settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;
            }

            // 3. 캐릭터(이미지) On/Off 및 재생
            if (CharacterBorder != null && MiniProfileImage != null)
            {
                if (settings.MiniTimerShowCharacter)
                {
                    CharacterBorder.Visibility = Visibility.Visible;

                    // 경로가 바뀌었을 때만 다시 재생
                    if (_lastLoadedImagePath != settings.UserImagePath)
                    {
                        _lastLoadedImagePath = settings.UserImagePath;
                        GifHelper.PlayGif(MiniProfileImage, _lastLoadedImagePath);
                    }
                }
                else
                {
                    CharacterBorder.Visibility = Visibility.Collapsed;
                    GifHelper.StopGif(MiniProfileImage);
                    _lastLoadedImagePath = null;
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
    }
}