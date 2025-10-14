using System;
using System.Linq;
using System.Windows;
using WorkPartner.Services;
using WorkPartner.Services.Implementations;

namespace WorkPartner
{
    public partial class App : Application
    {
        // App.xaml.cs 파일의 기존 OnStartup 메서드를 찾아서 아래 코드로 완전히 교체하세요.

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- 1. 모든 실제 서비스 전문가들을 생성합니다. ---
            ITaskService taskService = new TaskService();
            IDialogService dialogService = new DialogService();
            ISettingsService settingsService = new SettingsService();
            ITimerService timerService = new TimerService(); // TimerService 추가

            // --- 2. ViewModel에게 이 전문가들을 모두 연결(주입)해줍니다. ---
            var dashboardViewModel = new ViewModels.DashboardViewModel(
                taskService,
                dialogService,
                settingsService,
                timerService // ViewModel에 timerService 전달
            );

            // --- 3. MainWindow를 생성하고, DashboardPage에 ViewModel을 연결합니다. ---
            var mainWindow = new MainWindow();
            mainWindow.SetDashboardViewModel(dashboardViewModel);
            mainWindow.Show();

            // 기존 테마 적용 로직은 그대로 둡니다.
            var settings = settingsService.LoadSettings();
            (Current as App)?.ApplyTheme(settings);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 프로그램 시작 시 저장된 설정을 불러와 테마를 적용합니다.
            var settings = DataManager.LoadSettings();
            ApplyTheme(settings);
        }

        public void ApplyTheme(AppSettings settings)
        {
            var themeDictionaries = Resources.MergedDictionaries;

            // 기존에 적용된 테마 리소스가 있다면 제거합니다.
            var oldTheme = themeDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
            if (oldTheme != null)
            {
                themeDictionaries.Remove(oldTheme);
            }

            // 설정 값에 따라 새로운 테마 리소스를 추가합니다.
            string themeUri = settings.Theme == "Dark" ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var newTheme = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            themeDictionaries.Add(newTheme);
        }
    }
}
