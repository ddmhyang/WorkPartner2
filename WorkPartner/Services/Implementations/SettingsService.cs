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
            return Task.CompletedTask; // 간단한 작업이므로 비동기 작업이 완료되었음을 바로 반환
        }
    }
}