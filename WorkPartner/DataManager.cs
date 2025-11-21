using System;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using System.Text.Json;

namespace WorkPartner
{
    public static class DataManager
    {
        // --- 기존 경로들 ---
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        public static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");
        public static readonly string TasksFilePath = Path.Combine(AppDataPath, "tasks.json");
        public static readonly string TimeLogFilePath = Path.Combine(AppDataPath, "timelogs.json");
        public static readonly string TodosFilePath = Path.Combine(AppDataPath, "todos.json");
        public static readonly string MemosFilePath = Path.Combine(AppDataPath, "memos.json");

        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        // ▼▼▼ [수정] EventHandler -> Action으로 변경 (받는 쪽 파라미터 없음 오류 해결) ▼▼▼
        public static event Action SettingsUpdated;

        static DataManager()
        {
            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
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

                // ▼▼▼ [수정] Invoke(null, EventArgs.Empty) -> Invoke() 로 변경
                SettingsUpdated?.Invoke();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}"); }
        }

        // --- 나머지 저장 메서드들 (그대로 유지) ---

        public static void SaveTasks(ObservableCollection<TaskItem> tasks)
        {
            try
            {
                string json = JsonConvert.SerializeObject(tasks, Formatting.Indented);
                File.WriteAllText(TasksFilePath, json);
            }
            catch { }
        }

        public static void SaveTodos(ObservableCollection<TodoItem> todos)
        {
            try
            {
                string json = JsonConvert.SerializeObject(todos, Formatting.Indented);
                File.WriteAllText(TodosFilePath, json);
            }
            catch { }
        }

        public static void SaveTimeLogs(ObservableCollection<TimeLogEntry> logs)
        {
            try
            {
                string json = JsonConvert.SerializeObject(logs, Formatting.Indented);
                File.WriteAllText(TimeLogFilePath, json);
            }
            catch { }
        }

        public static void SaveTimeLogsImmediately(ObservableCollection<TimeLogEntry> logs)
        {
            SaveTimeLogs(logs);
        }

        public static void SaveMemos(ObservableCollection<MemoItem> memos)
        {
            try
            {
                string json = JsonConvert.SerializeObject(memos, Formatting.Indented);
                File.WriteAllText(MemosFilePath, json);
            }
            catch { }
        }

        // --- 데이터 관리 ---
        public static void ExportData(string filePath)
        {
            var settings = LoadSettings();
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static void ImportData(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                if (settings != null) SaveSettings(settings);
            }
        }

        public static void ResetAllData()
        {
            if (File.Exists(SettingsFilePath)) File.Delete(SettingsFilePath);
            if (File.Exists(TasksFilePath)) File.Delete(TasksFilePath);
            if (File.Exists(TimeLogFilePath)) File.Delete(TimeLogFilePath);
            if (File.Exists(TodosFilePath)) File.Delete(TodosFilePath);
            if (File.Exists(MemosFilePath)) File.Delete(MemosFilePath);
        }
    }
}