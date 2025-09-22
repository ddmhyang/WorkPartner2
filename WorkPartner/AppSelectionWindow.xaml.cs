using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

        public AppSelectionWindow(List<InstalledProgram> apps)
        {
            InitializeComponent();
            AppListView.ItemsSource = apps; // Corrected control name
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppListView.SelectedItem is InstalledProgram selectedApp) // Corrected control name
            {
                SelectedAppKeyword = selectedApp.ProcessName;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("목록에서 프로그램을 선택해주세요.", "알림");
            }
        }

        // Added missing event handler
        private void AppListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OkButton_Click(sender, e);
        }
    }
}
