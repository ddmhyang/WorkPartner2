using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace WorkPartner
{
    public static class DataManager
    {
        public static event Action SettingsUpdated;

        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        public static string ModelFilePath => Path.Combine(AppDataFolder, "FocusPredictionModel.zip");
        public static string ItemsDbFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items_db.json");

        static DataManager()
        {
            // AppData 폴더 생성 확인
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            // 데이터베이스 초기화
            using (var db = new AppDbContext())
            {
                db.Database.Migrate(); // 데이터베이스가 없으면 생성하고 최신 상태로 마이그레이션합니다.
            }
        }

        // --- Settings ---
        public static AppSettings LoadSettings()
        {
            using (var db = new AppDbContext())
            {
                // 설정은 하나만 존재하므로 FirstOrDefault 사용
                var settings = db.AppSettings.FirstOrDefault();
                if (settings == null)
                {
                    // 데이터베이스가 비어있을 경우 기본 설정 생성 및 저장
                    settings = new AppSettings();
                    db.AppSettings.Add(settings);
                    db.SaveChanges();
                }
                return settings;
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            using (var db = new AppDbContext())
            {
                // JSON 문자열 속성 업데이트 (INotifyPropertyChanged가 처리하지 못하는 부분)
                settings.WorkProcessesJson = JsonSerializer.Serialize(settings.WorkProcesses);
                settings.PassiveProcessesJson = JsonSerializer.Serialize(settings.PassiveProcesses);
                settings.DistractionProcessesJson = JsonSerializer.Serialize(settings.DistractionProcesses);
                settings.TaskColorsJson = JsonSerializer.Serialize(settings.TaskColors);
                settings.EquippedItemsJson = JsonSerializer.Serialize(settings.EquippedItems);
                settings.OwnedItemIdsJson = JsonSerializer.Serialize(settings.OwnedItemIds);
                settings.CustomColorsJson = JsonSerializer.Serialize(settings.CustomColors);

                db.AppSettings.Update(settings);
                db.SaveChanges();
            }
            SettingsUpdated?.Invoke(); // 변경 알림
        }


        // --- TimeLogs ---
        public static List<TimeLogEntry> LoadTimeLogs()
        {
            using (var db = new AppDbContext())
            {
                return db.TimeLogs.ToList();
            }
        }

        public static void SaveTimeLogs(IEnumerable<TimeLogEntry> logs)
        {
            using (var db = new AppDbContext())
            {
                // 기존 로그를 모두 삭제하고 새로 저장하는 방식 (간단한 구현)
                // 더 효율적인 방법: 변경된 로그만 추적하여 업데이트/추가/삭제
                db.TimeLogs.RemoveRange(db.TimeLogs);
                db.TimeLogs.AddRange(logs);
                db.SaveChanges();
            }
        }

        // --- Tasks ---
        public static List<TaskItem> LoadTasks()
        {
            using (var db = new AppDbContext())
            {
                return db.Tasks.ToList();
            }
        }

        public static void SaveTasks(IEnumerable<TaskItem> tasks)
        {
            using (var db = new AppDbContext())
            {
                // 기존 작업 목록과 비교하여 추가/수정/삭제 처리
                var existingTasks = db.Tasks.ToList();
                var tasksToDelete = existingTasks.Where(et => !tasks.Any(t => t.Id == et.Id)).ToList();
                var tasksToAdd = tasks.Where(t => t.Id == 0).ToList(); // Id가 0이면 새 항목
                var tasksToUpdate = tasks.Where(t => t.Id != 0 && existingTasks.Any(et => et.Id == t.Id)).ToList();

                if (tasksToDelete.Any()) db.Tasks.RemoveRange(tasksToDelete);
                if (tasksToAdd.Any()) db.Tasks.AddRange(tasksToAdd);
                foreach (var task in tasksToUpdate)
                {
                    db.Tasks.Update(task);
                }

                db.SaveChanges();
            }
        }


        // --- Todos ---
        public static List<TodoItem> LoadTodos()
        {
            using (var db = new AppDbContext())
            {
                return db.Todos.ToList();
            }
        }

        public static void SaveTodos(IEnumerable<TodoItem> todos)
        {
            using (var db = new AppDbContext())
            {
                // 기존 작업 목록과 비교하여 추가/수정/삭제 처리
                var existingTodos = db.Todos.ToList();
                var todosToDelete = existingTodos.Where(et => !todos.Any(t => t.Id == et.Id)).ToList();
                var todosToAdd = todos.Where(t => t.Id == 0).ToList(); // Id가 0이면 새 항목
                var todosToUpdate = todos.Where(t => t.Id != 0 && existingTodos.Any(et => et.Id == t.Id)).ToList();

                if (todosToDelete.Any()) db.Todos.RemoveRange(todosToDelete);
                if (todosToAdd.Any()) db.Todos.AddRange(todosToAdd);
                foreach (var todo in todosToUpdate)
                {
                    db.Todos.Update(todo);
                }

                db.SaveChanges();
            }
        }

        // --- Shop Items ---
        public static List<ShopItem> LoadShopItems()
        {
            if (File.Exists(ItemsDbFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ItemsDbFilePath);
                    return JsonConvert.DeserializeObject<List<ShopItem>>(json, new JsonSerializerSettings
                    {
                        Converters = { new StringEnumConverter() }
                    }) ?? new List<ShopItem>();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"상점 아이템 파일(items_db.json)을 불러오는 중 오류 발생: {ex.Message}");
                    return new List<ShopItem>();
                }
            }
            return new List<ShopItem>();
        }

        // AI 모델 파일 준비는 그대로 유지
        public static void PrepareFileForEditing(string sourceFileName)
        {
            try
            {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sourceFileName);
                string destinationPath = Path.Combine(AppDataFolder, sourceFileName);

                if (!File.Exists(destinationPath) && File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destinationPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 준비 중 오류 발생: {sourceFileName}\n{ex.Message}");
            }
        }
    }
}
