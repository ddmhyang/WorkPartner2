using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace WorkPartner
{
    public enum BulkEditResult
    {
        Cancel,
        ChangeTask,
        Delete
    }

    public partial class BulkEditLogsWindow : Window
    {
        public BulkEditResult Result { get; private set; } = BulkEditResult.Cancel;
        public TaskItem SelectedTask { get; private set; }

        public BulkEditLogsWindow(List<TimeLogEntry> selectedLogs, ObservableCollection<TaskItem> allTasks)
        {
            InitializeComponent();
            InfoTextBlock.Text = $"{selectedLogs.Count} 개의 기록이 선택되었습니다.";
            TaskComboBox.ItemsSource = allTasks;
            if (allTasks.Count > 0)
            {
                TaskComboBox.SelectedIndex = 0;
            }
        }

        private void ChangeTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskComboBox.SelectedItem is TaskItem task)
            {
                SelectedTask = task;
                Result = BulkEditResult.ChangeTask;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a task.", "No Task Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete all selected time logs?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Result = BulkEditResult.Delete;
                DialogResult = true;
            }
        }
    }
}
