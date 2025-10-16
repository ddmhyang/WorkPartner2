// 파일: WorkPartner/Services/ITaskService.cs
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace WorkPartner.Services
{
    public interface ITaskService
    {
        // ▼▼▼ 오류 수정: SaveTasksAsync와 LoadTasksAsync 정의 추가 ▼▼▼
        Task SaveTasksAsync(ObservableCollection<TaskItem> tasks);
        Task<ObservableCollection<TaskItem>> LoadTasksAsync();
    }
}