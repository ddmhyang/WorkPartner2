using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class DailyFocusRatingWindow : Window
    {
        public int SelectedScore { get; private set; }

        private int _initialScore;

        public DailyFocusRatingWindow(int initialScore)
        {
            InitializeComponent();
            _initialScore = initialScore;
            SelectedScore = initialScore;
        }

        private void DailyFocusRatingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStarRatingUI(_initialScore);
        }

        private void StarRating_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
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
            this.DialogResult = true;
            this.Close();
        }
    }
}