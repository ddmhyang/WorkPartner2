// 파일: WorkPartner/Services/ISettingsService.cs
using System.Threading.Tasks;

namespace WorkPartner.Services
{
    public interface ISettingsService
    {
        Task SetTaskColorAsync(string taskName, string colorHex);
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings); // ◀◀ [이 줄 추가]
    }
}