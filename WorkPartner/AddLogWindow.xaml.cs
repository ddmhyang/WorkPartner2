using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace WorkPartner
{
    public partial class AddLogWindow : Window
    {
        // ✨ DashboardPage에서 이 창의 결과를 가져갈 수 있도록 만든 공개 속성들
        public TimeLogEntry NewLogEntry { get; private set; }
        public bool IsDeleted { get; private set; } = false;

        // 생성자 1: 새 로그 항목을 만들 때
        public AddLogWindow(ObservableCollection<TaskItem> taskItems)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = taskItems;
            NewLogEntry = new TimeLogEntry { StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) };
            this.DataContext = NewLogEntry;

            DeleteButton.Visibility = Visibility.Collapsed; // 새 항목 추가 시에는 삭제 버튼 숨기기
        }

        // 생성자 2: 기존 로그 항목을 수정할 때
        public AddLogWindow(ObservableCollection<TaskItem> taskItems, TimeLogEntry existingLog)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = taskItems;
            NewLogEntry = existingLog; // 전달받은 기존 로그를 바인딩
            this.DataContext = NewLogEntry;

            // 기존 과목을 콤보박스에서 선택된 상태로 설정
            var selectedTaskItem = taskItems.FirstOrDefault(t => t.Text == existingLog.TaskText);
            if (selectedTaskItem != null)
            {
                TaskComboBox.SelectedItem = selectedTaskItem;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 유효성 검사
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

            // NewLogEntry 객체에 UI의 값을 최종적으로 반영
            NewLogEntry.TaskText = (TaskComboBox.SelectedItem as TaskItem)?.Text;
            NewLogEntry.StartTime = StartTimePicker.Value.Value;
            NewLogEntry.EndTime = EndTimePicker.Value.Value;
            NewLogEntry.FocusScore = (int)FocusSlider.Value;

            this.DialogResult = true; // 성공적으로 창이 닫혔음을 알림
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("이 학습 기록을 정말로 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                IsDeleted = true;
                this.DialogResult = true; // 삭제도 성공적인 처리로 간주
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // 취소
            this.Close();
        }
    }
}