// 파일: WorkPartner/Services/ISettingsService.cs
using System.Threading.Tasks;

namespace WorkPartner.Services
{
    public interface ISettingsService
    {
        // ▼▼▼ 누락되었던 정의 추가 ▼▼▼
        Task SetTaskColorAsync(string taskName, string colorHex);
        AppSettings LoadSettings();
    }
}