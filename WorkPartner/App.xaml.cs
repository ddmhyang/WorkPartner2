using System;
using System.Linq;
using System.Windows;
using WorkPartner.Services;
using WorkPartner.Services.Implementations;

namespace WorkPartner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- 1. 모든 실제 서비스 전문가들을 생성합니다. ---
            ITaskService taskService = new TaskService();
            IDialogService dialogService = new DialogService();
            ISettingsService settingsService = new SettingsService();
            ITimerService timerService = new TimerService();
            ITimeLogService timeLogService = new TimeLogService();

            // --- 2. ViewModel에게 모든 전문가들을 연결(주입)해줍니다. ---
            var dashboardViewModel = new ViewModels.DashboardViewModel(
                taskService,
                dialogService,
                settingsService,
                timerService,
                timeLogService
            );

            // --- 3. MainWindow를 생성하고 ViewModel을 연결합니다. ---
            var mainWindow = new MainWindow();
            mainWindow.SetDashboardViewModel(dashboardViewModel);
            mainWindow.Show();

            // --- 4. 테마를 적용합니다. ---
            var settings = settingsService.LoadSettings();
            ApplyTheme(settings);
        }

        // ApplyTheme 메서드는 그대로 둡니다.
        public void ApplyTheme(AppSettings settings)
        {
            var themeDictionaries = Resources.MergedDictionaries;
            var oldTheme = themeDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
            if (oldTheme != null)
            {
                themeDictionaries.Remove(oldTheme);
            }
            string themeUri = settings.Theme == "Dark" ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var newTheme = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            themeDictionaries.Add(newTheme);
        }
    }
}