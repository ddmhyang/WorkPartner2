// ✨ [3단계-새 파일] WorkPartner/DailyFocusRatingWindow.xaml.cs
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class DailyFocusRatingWindow : Window
    {
        // 이 창이 부모(DashboardPage)에게 전달할 값
        public int SelectedScore { get; private set; }

        // 부모로부터 전달받을 기존 값
        private int _initialScore;

        public DailyFocusRatingWindow(int initialScore)
        {
            InitializeComponent();
            _initialScore = initialScore;
            SelectedScore = initialScore; // 기본값은 기존 값
        }

        private void DailyFocusRatingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 창이 뜰 때, 전달받은 기존 점수로 별을 채워넣음
            UpdateStarRatingUI(_initialScore);
        }

        private void StarRating_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                // 사용자가 선택한 점수를 저장
                SelectedScore = int.Parse(rb.Tag.ToString());
            }
        }

        private void UpdateStarRatingUI(int score)
        {
            var starButton = StarRatingPanel.Children.OfType<RadioButton>()
                             .FirstOrDefault(rb => rb.Tag.ToString() == score.ToString());
            if (starButton != null)
            {
                starButton.IsChecked = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // "저장" 버튼을 누르면 DialogResult를 true로 설정하여
            // 부모 창이 SelectedScore 값을 읽어가도록 함
            this.DialogResult = true;
            this.Close();
        }
    }
}