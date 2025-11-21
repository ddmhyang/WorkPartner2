using System;
using System.IO;
using Newtonsoft.Json;

namespace WorkPartner
{
    public static class DataManager
    {
        // 파일 경로들 (기존과 동일하게 유지)
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        public static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");
        public static readonly string TasksFilePath = Path.Combine(AppDataPath, "tasks.json");
        public static readonly string TimeLogFilePath = Path.Combine(AppDataPath, "timelogs.json");
        public static readonly string TodosFilePath = Path.Combine(AppDataPath, "todos.json");
        public static readonly string MemosFilePath = Path.Combine(AppDataPath, "memos.json");

        static DataManager()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath)) return new AppSettings();
            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}"); }
        }

        // ▼▼▼ [추가된 메서드들] ▼▼▼

        public static void ExportData(string filePath)
        {
            // 현재 설정을 지정된 경로로 내보내기
            var settings = LoadSettings();
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static void ImportData(string filePath)
        {
            // 지정된 경로에서 설정 불러와서 덮어쓰기
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                if (settings != null)
                {
                    SaveSettings(settings);
                }
            }
        }

        public static void ResetAllData()
        {
            // 모든 데이터 파일 삭제 (초기화)
            if (File.Exists(SettingsFilePath)) File.Delete(SettingsFilePath);
            if (File.Exists(TasksFilePath)) File.Delete(TasksFilePath);
            if (File.Exists(TimeLogFilePath)) File.Delete(TimeLogFilePath);
            if (File.Exists(TodosFilePath)) File.Delete(TodosFilePath);
            if (File.Exists(MemosFilePath)) File.Delete(MemosFilePath);
        }
    }
}