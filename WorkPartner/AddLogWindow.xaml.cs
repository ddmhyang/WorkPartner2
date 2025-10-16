using System;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;

namespace WorkPartner
{
    public partial class AddLogWindow : Window
    {
        public TimeLogEntry NewLogEntry { get; private set; }
        public bool IsDeleted { get; private set; } = false;

        public AddLogWindow(ObservableCollection<TaskItem> taskItems)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = taskItems;
            NewLogEntry = new TimeLogEntry { StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), FocusScore = 3 };
            this.DataContext = NewLogEntry;
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        public AddLogWindow(ObservableCollection<TaskItem> taskItems, TimeLogEntry existingLog)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = taskItems;
            // 중요: 원본을 직접 수정하지 않도록 복사본을 만듭니다.
            NewLogEntry = new TimeLogEntry
            {
                StartTime = existingLog.StartTime,
                EndTime = existingLog.EndTime,
                TaskText = existingLog.TaskText,
                FocusScore = existingLog.FocusScore
            };
            this.DataContext = NewLogEntry;

            var selectedTaskItem = taskItems.FirstOrDefault(t => t.Text == existingLog.TaskText);
            if (selectedTaskItem != null)
            {
                TaskComboBox.SelectedItem = selectedTaskItem;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskComboBox.SelectedItem == null)
            {
                MessageBox.Show("과목을 선택해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (StartTimePicker.Value == null || EndTimePicker.Value == null)
            {
                MessageBox.Show("시작 시간과 종료 시간을 모두 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (StartTimePicker.Value.Value >= EndTimePicker.Value.Value)
            {
                MessageBox.Show("종료 시간은 시작 시간보다 나중이어야 합니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewLogEntry.TaskText = (TaskComboBox.SelectedItem as TaskItem)?.Text;
            NewLogEntry.StartTime = StartTimePicker.Value.Value;
            NewLogEntry.EndTime = EndTimePicker.Value.Value;
            NewLogEntry.FocusScore = (int)FocusSlider.Value;

            this.DialogResult = true;
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("이 학습 기록을 정말로 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
    }
}