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

        private void OnSettingsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadSettings();
            });
        }

        private void MiniTimerWindow_Closed(object sender, EventArgs e)
        {
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
                }
                else
                {
                    KeyringArea.Visibility = Visibility.Collapsed;
                    GifHelper.StopGif(MiniProfileImage);
                    _lastLoadedImagePath = null;
                }
            }
        }

        // ▼▼▼ [수정] 애니메이션 코드 싹 지우고 이동만 남김 ▼▼▼
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove(); // 그냥 창 이동만 함
            }
        }
    }
}