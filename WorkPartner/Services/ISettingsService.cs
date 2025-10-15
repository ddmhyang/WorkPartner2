// 파일: WorkPartner/Services/ISettingsService.cs
using System.Threading.Tasks;

namespace WorkPartner.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
        Task SetTaskColorAsync(string taskName, string colorHex);
        AppSettings LoadSettings();
    }
}