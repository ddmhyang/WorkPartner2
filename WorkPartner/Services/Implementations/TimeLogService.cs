// 파일: Services/Implementations/TimeLogService.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorkPartner.Services.Implementations
{
    public class TimeLogService : ITimeLogService
    {
        private readonly string _filePath = DataManager.TimeLogFilePath;

        public async Task<ObservableCollection<TimeLogEntry>> LoadTimeLogsAsync()
        {

            if (!File.Exists(_filePath))
            {
                return new ObservableCollection<TimeLogEntry>();
            }
            try
            {
                await using var stream = File.OpenRead(_filePath);
                var logs = await JsonSerializer.DeserializeAsync<ObservableCollection<TimeLogEntry>>(stream);
                return logs ?? new ObservableCollection<TimeLogEntry>();
            }
            catch
            {
                return new ObservableCollection<TimeLogEntry>();
            }
        }

        public async Task SaveTimeLogsAsync(ObservableCollection<TimeLogEntry> timeLogs)
        {
            try
            {
                await using var stream = File.Create(_filePath);
                // DataManager에 정의된 공용 JsonOptions를 사용합니다.
                await JsonSerializer.SerializeAsync(stream, timeLogs, DataManager.JsonOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving time logs: {ex.Message}");
            }
        }
    }
}