using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        public MiniTimerWindow()
        {
            InitializeComponent();
            LoadSettings(); // 초기 설정 로드
        }

        private void LoadSettings()
        {
            var settings = DataManager.LoadSettings();
            UpdateUI(settings);
        }

        // ▼▼▼ [수정] 파라미터에서 AppSettings 제거하고 UI 업데이트 로직 단순화 ▼▼▼
        public void UpdateData(AppSettings settings, string currentTask, string time)
        {
            // 1. 텍스트 업데이트
            CurrentTaskText.Text = $"현재: {currentTask}";
            TimeText.Text = time;

            // 2. 설정에 따른 UI 보이기/숨기기
            UpdateUI(settings);

            // 🗑️ [삭제] 캐릭터 업데이트 로직 제거
            // CharacterPreview.UpdateCharacter(settings); 
        }

        private void UpdateUI(AppSettings settings)
        {
            if (settings == null) return;

            // 배경 표시 여부 (투명도 조절 등)
            this.Background = settings.MiniTimerShowBackground
                ? (Brush)FindResource("MainBackgroundBrush")
                : Brushes.Transparent;

            // 캐릭터(이제는 이미지) 보이기/숨기기
            if (CharacterBorder != null)
            {
                CharacterBorder.Visibility = settings.MiniTimerShowCharacter
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // 정보 텍스트 보이기/숨기기 (시간은 항상 보임)
            if (CurrentTaskText != null)
            {
                CurrentTaskText.Visibility = settings.MiniTimerShowInfo
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}