// 파일: WorkPartner/Services/Implementations/SettingsService.cs
using System.Threading.Tasks;

namespace WorkPartner.Services.Implementations
{
    public class SettingsService : ISettingsService
    {
        public AppSettings LoadSettings()
        {
            return DataManager.LoadSettings();
        }

        public Task SetTaskColorAsync(string taskName, string colorHex)
        {
            var settings = DataManager.LoadSettings();
            settings.TaskColors[taskName] = colorHex;
            DataManager.SaveSettings(settings);
            return Task.CompletedTask;
        }

        // ▼▼▼ [이 메서드 전체 추가] ▼▼▼
        public void SaveSettings(AppSettings settings)
        {
            DataManager.SaveSettings(settings);
        }
    }
}