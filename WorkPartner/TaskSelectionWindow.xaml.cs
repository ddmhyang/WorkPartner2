using System.Collections.Generic;
using System.Windows;

namespace WorkPartner
{
    public partial class TaskSelectionWindow : Window
    {
        public TaskItem SelectedTask { get; private set; }

        public TaskSelectionWindow(IEnumerable<TaskItem> tasks)
        {
            InitializeComponent();
            TaskComboBox.ItemsSource = tasks;
            if (TaskComboBox.Items.Count > 0)
            {
                TaskComboBox.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskComboBox.SelectedItem != null)
            {
                SelectedTask = TaskComboBox.SelectedItem as TaskItem;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("과목을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
