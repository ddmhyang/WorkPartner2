using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

        public AppSelectionWindow()
        {
            InitializeComponent();
        }

        public AppSelectionWindow(List<InstalledProgram> apps)
        {
            InitializeComponent();
            AppListView.ItemsSource = apps;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void AppListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (AppListView.SelectedItem is InstalledProgram selectedApp)
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}