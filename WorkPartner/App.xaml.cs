using System;
using System.Linq;
using System.Windows;
using WorkPartner.Services;
using WorkPartner.Services.Implementations;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ITaskService taskService = new TaskService();
            IDialogService dialogService = new DialogService();
            ISettingsService settingsService = new SettingsService();
            ITimerService timerService = new TimerService();
            ITimeLogService timeLogService = new TimeLogService();

            var dashboardViewModel = new ViewModels.DashboardViewModel(
                taskService,
                dialogService,
                settingsService,
                timerService,
                timeLogService
                );
            MainWindow mainWindow = new MainWindow();
            mainWindow.SetDashboardViewModel(dashboardViewModel);
            mainWindow.Show();
            mainWindow.Activate();

            var settings = DataManager.LoadSettings();
            ApplyTheme(settings);
        }

        public void ApplyTheme(AppSettings settings)
        {
            var existingDict = Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
            if (existingDict != null)
            {
                Resources.MergedDictionaries.Remove(existingDict);
            }

            string themeUri = settings.Theme == "Dark" ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var newDict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            Resources.MergedDictionaries.Add(newDict);

            var accentColor = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
            Resources["AccentColor"] = accentColor;
            Resources["AccentColorBrush"] = new SolidColorBrush(accentColor);
        }
    }
}
