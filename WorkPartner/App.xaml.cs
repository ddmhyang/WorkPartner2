using System;
using System.Linq;
using System.Windows;
using WorkPartner.Services;
using WorkPartner.Services.Implementations;
using System.Windows.Media; // Color와 Brush를 사용하기 위해 추가

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
            mainWindow.Activate();

            var settings = DataManager.LoadSettings();
            ApplyTheme(settings);
        }

        public void ApplyTheme(AppSettings settings)
        {
            // 1. 기존 테마 리소스 딕셔너리를 찾아서 제거합니다.
            var existingDict = Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
            if (existingDict != null)
            {
                Resources.MergedDictionaries.Remove(existingDict);
            }

            // 2. 설정에 맞는 새 테마 딕셔너리를 추가합니다.
            string themeUri = settings.Theme == "Dark" ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var newDict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            Resources.MergedDictionaries.Add(newDict);

            // 3. 강조 색상을 동적으로 변경하여 리소스에 추가/업데이트합니다.
            var accentColor = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
            Resources["AccentColor"] = accentColor;
            Resources["AccentColorBrush"] = new SolidColorBrush(accentColor);
        }
    }
}
