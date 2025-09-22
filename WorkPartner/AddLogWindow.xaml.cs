using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class AddLogWindow : Window
    {
        public TimeLogEntry NewLogEntry { get; private set; }
        public bool IsDeleted { get; private set; } = false;
        private int _currentScore = 0;

        public AddLogWindow(ObservableCollection<TaskItem> tasks, TimeLogEntry logToEdit = null)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = tasks;

            if (logToEdit != null)
            {
                this.Title = "기록 수정";
                NewLogEntry = logToEdit;

                TaskComboBox.SelectedItem = tasks.FirstOrDefault(t => t.Text == logToEdit.TaskText);
                LogDatePicker.SelectedDate = logToEdit.StartTime.Date;
                StartTimeTextBox.Text = logToEdit.StartTime.ToString("HH:mm");
                EndTimeTextBox.Text = logToEdit.EndTime.ToString("HH:mm");

                // [로직 추가] 기존 평점을 불러와 UI에 반영
                _currentScore = logToEdit.FocusScore;
                UpdateRatingUI(_currentScore);
            }
            else
            {
                this.Title = "수동 기록 추가";
                DeleteButton.Visibility = Visibility.Collapsed;
                NewLogEntry = new TimeLogEntry(); // 새 로그 엔트리 생성
                if (tasks.Count > 0) { TaskComboBox.SelectedIndex = 0; }
                LogDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskComboBox.SelectedItem == null) { MessageBox.Show("과목을 선택해주세요."); return; }
            if (!DateTime.TryParse($"{LogDatePicker.Text} {StartTimeTextBox.Text}", out DateTime startTime) ||
                !DateTime.TryParse($"{LogDatePicker.Text} {EndTimeTextBox.Text}", out DateTime endTime))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다. (HH:mm 형식으로 입력)");
                return;
            }
            if (startTime >= endTime) { MessageBox.Show("종료 시간은 시작 시간보다 나중이어야 합니다."); return; }

            // 기존 NewLogEntry 객체를 업데이트하거나 새로 생성
            NewLogEntry.TaskText = (TaskComboBox.SelectedItem as TaskItem).Text;
            NewLogEntry.StartTime = startTime;
            NewLogEntry.EndTime = endTime;
            NewLogEntry.FocusScore = _currentScore; // [로직 추가] 저장 시 평점 반영

            this.DialogResult = true;
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("이 기록을 정말로 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                IsDeleted = true;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // [메서드 추가] 별점 버튼 클릭 이벤트
        private void RatingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int score))
            {
                _currentScore = score;
                UpdateRatingUI(score);
            }
        }

        // [메서드 추가] 별점 UI 업데이트
        private void UpdateRatingUI(int score)
        {
            foreach (Button btn in RatingPanel.Children)
            {
                int btnScore = int.Parse(btn.Tag.ToString());
                btn.Foreground = btnScore <= score ? Brushes.Gold : Brushes.LightGray;
            }
        }
    }
}