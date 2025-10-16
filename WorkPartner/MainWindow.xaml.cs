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

            Loaded += MainWindow_Loaded;
        }

        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageName)
            {
                await NavigateToPage(pageName);
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleMiniTimer();
        }

        public async Task NavigateToPage(string pageName)
        {
            switch (pageName)
            {
                case "Dashboard":
                    MainFrame.Navigate(_dashboardPage);
                    if (_dashboardPage.DataContext is ViewModels.DashboardViewModel viewModel)
                    {
                        await viewModel.LoadAllDataAsync();
                    }
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
                    if (_settingsPage == null)
                    {
                        _settingsPage = new SettingsPage();
                        _settingsPage.SetParentWindow(this);
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
                    _miniTimer = new MiniTimerWindow { Owner = this };
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

        public void SetDashboardViewModel(object viewModel)
        {
            _dashboardPage.DataContext = viewModel;
        }
    }
}