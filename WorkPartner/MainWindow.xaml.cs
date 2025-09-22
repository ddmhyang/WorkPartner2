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
            _dashboardPage.SetParentWindow(this);
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
                    _dashboardPage.LoadAllData();
                    break;
                case "Avatar":
                    if (_avatarPage == null) _avatarPage = new AvatarPage();
                    _avatarPage.LoadData();
                    MainFrame.Navigate(_avatarPage);
                    break;
                case "Analysis":
                    if (_analysisPage == null) _analysisPage = new AnalysisPage();
                    await _analysisPage.LoadAndAnalyzeData(); // 비동기 로딩
                    MainFrame.Navigate(_analysisPage);
                    break;
                case "Settings":
                    if (_settingsPage == null)
                    {
                        _settingsPage = new SettingsPage();
                        _settingsPage.SetParentWindow(this); // 부모 윈도우 참조 설정
                    }
                    _settingsPage.LoadData();
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

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
    }
}

