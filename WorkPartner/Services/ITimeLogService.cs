// 파일: Services/ITimeLogService.cs
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace WorkPartner.Services
{
    public interface ITimeLogService
    {
        Task<ObservableCollection<TimeLogEntry>> LoadTimeLogsAsync();
        void SaveTimeLogs(ObservableCollection<TimeLogEntry> logs);
    }
}