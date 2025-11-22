using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private StatisticsPage _statisticsPage;
        private SettingsPage _settingsPage;
        private MiniTimerWindow _miniTimer;

        private ViewModels.DashboardViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _dashboardPage.SetParentWindow(this);
            MainFrame.Navigate(_dashboardPage);

            Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _miniTimer?.Close();

            if (_dashboardPage.DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.Shutdown();
            }

            Application.Current.Shutdown();
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
                    await _dashboardPage.LoadAllDataAsync();
                    break;

                case "Statistics":
                    if (_statisticsPage == null)
                    {
                        _statisticsPage = new StatisticsPage();
                    }
                    MainFrame.Navigate(_statisticsPage);
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

        // [수정] 타이머가 이미 켜져있으면 닫지 않고 설정만 새로고침
        public void ToggleMiniTimer(bool? isEnabled = null)
        {
            bool enabled = isEnabled ?? DataManager.LoadSettings().IsMiniTimerEnabled;

            if (enabled)
            {
                if (_miniTimer == null)
                {
                    // 타이머가 없을 때만 새로 생성
                    _miniTimer = new MiniTimerWindow
                    {
                        Owner = null,
                        Topmost = true
                    };
                    _miniTimer.Closed += (s, e) => _miniTimer = null;
                    _dashboardPage.SetMiniTimerReference(_miniTimer);

                    _miniTimer.Show();
                }
                else
                {
                    // 이미 켜져있다면 설정만 업데이트 (깜빡임 방지 및 즉시 반영)
                    _miniTimer.ReloadSettings();
                }
            }
            else
            {
                // 꺼야 하는 경우에만 닫기
                if (_miniTimer != null)
                {
                    _miniTimer.Close();
                    _miniTimer = null;
                }
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        public void SetDashboardViewModel(object viewModel)
        {
            _mainViewModel = viewModel as ViewModels.DashboardViewModel;
            _dashboardPage.DataContext = _mainViewModel;
        }
    }
}