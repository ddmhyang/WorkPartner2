using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private readonly DashboardPage _dashboardPage;
        private AvatarPage _avatarPage;
        private AnalysisPage _analysisPage;
        private SettingsPage _settingsPage;
        private MiniTimerWindow _miniTimer;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _dashboardPage.SetParentWindow(this); // 대시보드에 메인 윈도우 자신을 알려주는 코드
            MainFrame.Navigate(_dashboardPage);

            ToggleMiniTimer();
        }

        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageName)
            {
                await NavigateToPage(pageName);
            }
        }

        public async Task NavigateToPage(string pageName)
        {
            switch (pageName)
            {
                case "Dashboard":
                    MainFrame.Navigate(_dashboardPage);
                    _dashboardPage.LoadAllData(); // 대시보드로 돌아올 때 데이터 새로고침
                    break;
                case "Avatar":
                    if (_avatarPage == null) _avatarPage = new AvatarPage();
                    _avatarPage.LoadData();
                    MainFrame.Navigate(_avatarPage);
                    break;
                case "Analysis":
                    if (_analysisPage == null) _analysisPage = new AnalysisPage();
                    await _analysisPage.LoadAndAnalyzeData();
                    MainFrame.Navigate(_analysisPage);
                    break;
                case "Settings":
                    if (_settingsPage == null) _settingsPage = new SettingsPage();
                    MainFrame.Navigate(_settingsPage);
                    break;
            }
        }

        public void ToggleMiniTimer()
        {
            var settings = DataManager.LoadSettings();
            if (settings.IsMiniTimerEnabled)
            {
                if (_miniTimer == null)
                {
                    _miniTimer = new MiniTimerWindow();
                    _miniTimer.Closed += (s, e) => _miniTimer = null;
                    _dashboardPage.SetMiniTimerReference(_miniTimer);
                }
                _miniTimer.Show();
            }
            else
            {
                _miniTimer?.Close();
            }
        }

        // Window Controls
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
    }
}

