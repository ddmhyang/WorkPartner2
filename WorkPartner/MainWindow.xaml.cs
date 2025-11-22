using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private readonly DashboardPage _dashboardPage;
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
                    // 페이지가 로드될 때마다 데이터를 새로 읽도록 트리거할 수도 있습니다.
                    // (StatisticsPage.xaml.cs의 Loaded 이벤트가 알아서 처리함)
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

        public void ToggleMiniTimer(bool? isEnabled = null)
        {
            bool enabled = isEnabled ?? DataManager.LoadSettings().IsMiniTimerEnabled;

            if (enabled)
            {
                if (_miniTimer != null)
                {
                    _miniTimer.Close();
                    _miniTimer = null;
                }

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
                _miniTimer?.Close();
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