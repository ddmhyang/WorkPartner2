// 𝙃𝙚𝙧𝙚'𝙨 𝙩𝙝𝙚 𝙘𝙤𝙙𝙚 𝙞𝙣 ddmhyang/workpartner2/WorkPartner2-4/WorkPartner/DataManager.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading;

namespace WorkPartner
{
    public static class DataManager
    {
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        public static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");
        public static readonly string TasksFilePath = Path.Combine(AppDataPath, "tasks.json");
        public static readonly string TodosFilePath = Path.Combine(AppDataPath, "todos.json");
        public static readonly string TimeLogFilePath = Path.Combine(AppDataPath, "timelogs.json");
        public static readonly string MemosFilePath = Path.Combine(AppDataPath, "memos.json");
        public static readonly string ItemsDbFilePath = Path.Combine(AppDataPath, "items_db.json");
        // ✨ [오류 수정] PredictionService에서 사용하던 ModelFilePath를 다시 추가했습니다.
        public static readonly string ModelFilePath = Path.Combine(AppDataPath, "model_input.json");

        public static readonly string UserModelFilePath = Path.Combine(AppDataPath, "user_model.zip");
        // ✨ [오류 수정] 다른 클래스에서 접근할 수 있도록 접근 제어자를 internal로 변경했습니다.
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private static Timer _saveTimer;
        private static Action _pendingSaveAction;
        private const int SaveDelayMilliseconds = 1500;

        public static event Action SettingsUpdated;

        static DataManager()
        {
            Directory.CreateDirectory(AppDataPath);

            // ✨ [추가] items_db.json 파일이 AppData에 없으면, 실행 파일 경로에서 복사
            InitializeDefaultDatabase(ItemsDbFilePath, "items_db.json");
            // ✨ [추가] AI 모델 파일도 동일하게 처리 (AnalysisPage 로딩 대비)
            InitializeDefaultDatabase(ModelFilePath, "model_input.json");
        }

        private static void InitializeDefaultDatabase(string destinationPath, string sourceFileName)
        {
            // AppData 폴더(destinationPath)에 파일이 이미 존재하면 아무것도 하지 않음
            if (File.Exists(destinationPath))
            {
                return;
            }

            try
            {
                // 실행 파일과 같은 위치(예: bin/Debug)에 있는 원본 DB 파일을 찾습니다.
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sourceFileName);

                if (File.Exists(sourcePath))
                {
                    // AppData 폴더로 복사
                    File.Copy(sourcePath, destinationPath);
                }
                else
                {
                    // 원본 파일도 없는 경우 (프로젝트 설정 오류)
                    System.Diagnostics.Debug.WriteLine($"Source file not found: {sourcePath}");
                }
            }
            catch (Exception ex)
            {
                // 복사 중 오류 (권한 문제 등)
                System.Diagnostics.Debug.WriteLine($"Error copying default file '{sourceFileName}': {ex.Message}");
            }
        }

        // 파일: WorkPartner/DataManager.cs

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = new AppSettings();
                WriteSettingsToFile(defaultSettings);
                return defaultSettings;
            }
            try
            {
                // ✨ [핵심 수정] 파일 잠금을 피하기 위해 FileShare.ReadWrite 옵션으로 읽습니다.
                string json;
                using (var stream = new FileStream(SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }

                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            }
            catch
            {
                var backupSettings = new AppSettings();
                WriteSettingsToFile(backupSettings);
                return backupSettings;
            }
        }

        public static void WriteSettingsToFile(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void SaveSettings(AppSettings settings)
        {
            WriteSettingsToFile(settings);
            SettingsUpdated?.Invoke();
        }

        private static void DebounceSave<T>(string filePath, T data)
        {
            _pendingSaveAction = () =>
            {
                try
                {
                    string json = JsonSerializer.Serialize(data, JsonOptions);

                    // [수정 전]
                    // File.WriteAllText(filePath, json);

                    // ✨ [핵심 수정]
                    // File.WriteAllText 대신 FileStream을 사용하여
                    // 다른 프로세스의 읽기를 허용(FileShare.Read)합니다.
                    using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(json);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving {Path.GetFileName(filePath)}: {ex.Message}");
                }
            };

            _saveTimer?.Dispose();
            _saveTimer = new Timer(DoSave, null, SaveDelayMilliseconds, Timeout.Infinite);
        }

        private static void DoSave(object state)
        {
            _pendingSaveAction?.Invoke();
            _pendingSaveAction = null;
        }

        public static void SaveTasks(IEnumerable<TaskItem> tasks)
        {
            // DebounceSave(TasksFilePath, tasks); // <- 주석 처리
            // ▼ [즉시 저장 코드로 변경]
            try
            {
                string json = JsonSerializer.Serialize(tasks, JsonOptions);
                File.WriteAllText(TasksFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving {Path.GetFileName(TasksFilePath)}: {ex.Message}");
            }
        }

        public static void SaveTodos(IEnumerable<TodoItem> todos)
        {
            // DebounceSave(TodosFilePath, todos); // <- 주석 처리
            // ▼ [즉시 저장 코드로 변경]
            try
            {
                string json = JsonSerializer.Serialize(todos, JsonOptions);
                File.WriteAllText(TodosFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving {Path.GetFileName(TodosFilePath)}: {ex.Message}");
            }
        }


        public static void SaveTimeLogs(ObservableCollection<TimeLogEntry> logs)
        {
            DebounceSave(TimeLogFilePath, logs);
        }

        // ✨ [버그 1 수정]
        // Debounce(지연 저장)를 사용하지 않고, 즉시 파일에 저장하는 메서드를 추가합니다.
        // 수동 추가/수정/삭제 시 이 메서드를 사용합니다.
        public static void SaveTimeLogsImmediately(ObservableCollection<TimeLogEntry> logs)
        {
            try
            {
                string json = JsonSerializer.Serialize(logs, JsonOptions);
                File.WriteAllText(TimeLogFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving {Path.GetFileName(TimeLogFilePath)}: {ex.Message}");
            }
        }
    }
}
