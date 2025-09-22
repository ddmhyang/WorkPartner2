using System;
using System.Linq;
using System.Windows;

namespace WorkPartner
{
    public partial class App : Application
    {
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
