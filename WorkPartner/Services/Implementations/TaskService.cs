// 파일: WorkPartner/Services/Implementations/TaskService.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorkPartner.Services.Implementations
{
    public class TaskService : ITaskService
    {
        public async Task SaveTasksAsync(ObservableCollection<TaskItem> tasks)
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await using FileStream createStream = File.Create(DataManager.TasksFilePath);
            await JsonSerializer.SerializeAsync(createStream, tasks, options);
        }

        public async Task<ObservableCollection<TaskItem>> LoadTasksAsync()
        {
            if (!File.Exists(DataManager.TasksFilePath))
            {
                return new ObservableCollection<TaskItem>();
            }

            await using FileStream openStream = File.OpenRead(DataManager.TasksFilePath);
            var tasks = await JsonSerializer.DeserializeAsync<ObservableCollection<TaskItem>>(openStream);
            return tasks ?? new ObservableCollection<TaskItem>();
        }
    }
}