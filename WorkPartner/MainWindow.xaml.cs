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
        private AvatarPage _avatarPage;
        private AnalysisPage _analysisPage;
        private SettingsPage _settingsPage;
        private MiniTimerWindow _miniTimer;

        private ViewModels.DashboardViewModel _mainViewModel; // ViewModel을 저장할 변수 선언
        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _dashboardPage.SetParentWindow(this);
            MainFrame.Navigate(_dashboardPage);

            Loaded += MainWindow_Loaded; // <--- 이 줄을 추가합니다.
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // 1. 미니 타이머가 열려있으면 닫습니다. (종료 문제의 원인)
            _miniTimer?.Close();

            // 2. 뷰모델(두뇌)에게 종료를 알립니다. (데이터 최종 저장)
            if (_dashboardPage.DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.Shutdown();
            }

            // 3. [중요] 어떤 방식으로 닫든(X버튼, Close 버튼), 앱 전체를 강제 종료합니다.
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
            ToggleMiniTimer(); // 메인 창이 모두 로드된 후 미니 타이머를 켭니다.
        }

        public async Task NavigateToPage(string pageName)
        {
            switch (pageName)
            {
                case "Dashboard":
                    MainFrame.Navigate(_dashboardPage);
                    break;
                case "Avatar":
                    if (_avatarPage == null) _avatarPage = new AvatarPage();
                    _avatarPage.LoadData();
                    MainFrame.Navigate(_avatarPage);
                    break;
                case "Analysis":
                    if (_analysisPage == null) _analysisPage = new AnalysisPage();

                    // ▼▼▼ [추가] 분석 페이지에 메인 ViewModel 전달 ▼▼▼
                    _analysisPage.SetViewModel(_mainViewModel);
                    // ▲▲▲

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

        public void ToggleMiniTimer(bool? isEnabled = null)
        {
            // ✨ [수정] 
            // isEnabled가 null이면(앱 시작 시) 디스크에서 로드하고,
            // null이 아니면(설정 페이지에서 전달) 그 값을 사용합니다.
            bool enabled = isEnabled ?? DataManager.LoadSettings().IsMiniTimerEnabled;

            if (enabled)
            {
                if (_miniTimer == null)
                {
                    _miniTimer = new MiniTimerWindow
                    {
                        //Owner = this // 오류가 해결된 상태이므로 이 코드를 그대로 둡니다.
                        Owner = null,        // 👈 [수정 1] 소유권 연결을 끊습니다.
                        Topmost = true
                    };
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

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close(); // 👈 그냥 창을 닫으라고만 요청
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;


        public void SetDashboardViewModel(object viewModel)
        {
            // ▼▼▼ [수정] ViewModel을 메인 윈도우에 저장 ▼▼▼
            _mainViewModel = viewModel as ViewModels.DashboardViewModel;
            _dashboardPage.DataContext = _mainViewModel;
            // ▲▲▲
        }
    }
}

