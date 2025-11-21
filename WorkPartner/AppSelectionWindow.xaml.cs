using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        // ▼ 이 속성을 추가해서 SettingsPage에서 읽을 수 있게 함
        public string SelectedAppName { get; private set; }

        public AppSelectionWindow()
        {
            InitializeComponent();
            LoadRunningProcesses();
        }

        // 파라미터가 있는 생성자는 삭제하고, 내부에서 로드합니다.
        private void LoadRunningProcesses()
        {
            var processes = Process.GetProcesses()
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(n => n)
                .Select(n => new InstalledProgram { ProcessName = n, DisplayName = n }) // InstalledProgram 클래스 활용
                .ToList();

            // XAML에 ListBox 이름이 'AppListBox'라고 가정합니다.
            // 만약 XAML에 이름이 없다면 AppSelectionWindow.xaml도 확인해야 하지만, 
            // 보통 이런 이름입니다. (오류 나면 알려주세요)
            if (this.FindName("AppListBox") is ListBox listBox)
            {
                listBox.ItemsSource = processes;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
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