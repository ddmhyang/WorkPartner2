// 파일: AppSelectionWindow.xaml.cs

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq; // System.Linq 네임스페이스 추가

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

        // 생성자에서 websites 매개변수 제거
        public AppSelectionWindow(List<InstalledProgram> runningApps)
        {
            InitializeComponent();
            AppListBox.ItemsSource = runningApps;
            // WebsiteListBox 관련 코드 삭제
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void AppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        // WebsiteListBox_MouseDoubleClick 이벤트 핸들러 삭제

        private void ConfirmSelection()
        {
            if (AppListBox.SelectedItem is InstalledProgram selectedProgram)
            {
                SelectedAppKeyword = selectedProgram.ProcessName;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("목록에서 프로그램을 선택해주세요.");
            }
        }

        // "웹사이트 직접 추가" 버튼 클릭 이벤트 핸들러 추가
        private void AddWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            var inputWindow = new InputWindow("추가할 웹사이트 주소(키워드)를 입력하세요:", "youtube.com")
            {
                Owner = this
            };

            if (inputWindow.ShowDialog() == true)
            {
                string websiteKeyword = inputWindow.ResponseText.Trim().ToLower();
                if (!string.IsNullOrEmpty(websiteKeyword))
                {
                    SelectedAppKeyword = websiteKeyword;
                    this.DialogResult = true;
                    this.Close();
                }
            }
        }
    }
}