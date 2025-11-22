using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

        public AppSelectionWindow()
        {
            InitializeComponent();
            LoadRunningProcesses();
        }

        // ▼▼▼ [복구] 잘 작동하던 Branch 9 버전의 로직 ▼▼▼
        private void LoadRunningProcesses()
        {
            // 복잡한 필터링 없이, 현재 실행 중인 모든 프로세스의 이름을 가져와서 중복 제거
            var processes = Process.GetProcesses()
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(n => n)
                .Select(n => new InstalledProgram { ProcessName = n, DisplayName = n })
                .ToList();

            if (this.FindName("AppListBox") is ListBox listBox)
            {
                listBox.ItemsSource = processes;
            }
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
            if (this.FindName("AppListBox") is ListBox listBox && listBox.SelectedItem is InstalledProgram selected)
            {
                SelectedAppKeyword = selected.ProcessName;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("앱을 선택해주세요.");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}