using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // MouseButtonEventArgs를 위해 필요

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppName { get; private set; }

        public AppSelectionWindow()
        {
            InitializeComponent();
            LoadRunningProcesses();
        }

        private void LoadRunningProcesses()
        {
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

        // ▼ [추가] XAML에서 찾는 더블클릭 이벤트 핸들러
        private void AppListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (this.FindName("AppListBox") is ListBox listBox && listBox.SelectedItem is InstalledProgram selected)
            {
                SelectedAppName = selected.ProcessName;
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