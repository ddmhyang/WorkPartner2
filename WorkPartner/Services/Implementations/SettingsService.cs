// 파일: Services/Implementations/SettingsService.cs (최종 수정)

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorkPartner.Services.Implementations
{
    public class SettingsService : ISettingsService
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "app_settings.json");

        // ★ 수정: 인터페이스와 일치하도록 AppSettings를 반환하는 동기 메소드로 변경
        public AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }
            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        // ★ 추가: 인터페이스에 정의된 SaveSettingsAsync 메소드 구현
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(SettingsFilePath, json);
        }

        // ★ 추가: 인터페이스에 정의된 SetTaskColorAsync 메소드 구현
        public async Task SetTaskColorAsync(string taskName, string color)
        {
            var settings = LoadSettings();
            settings.TaskColors[taskName] = color;
            await SaveSettingsAsync(settings);
        }

        // 참고: LoadSettingsAsync는 현재 인터페이스에 없으므로, LoadSettings()를 대신 사용합니다.
        public async Task<AppSettings> LoadSettingsAsync()
        {
            return await Task.Run(() => LoadSettings());
        }
    }
}