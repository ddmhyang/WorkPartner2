using System;

namespace WorkPartner.Services
{
    public interface ITimerService
    {
        bool IsRunning { get; }
        string CurrentTask { get; }
        event Action TimerStateChanged;
        event Action<TimeSpan> TimeUpdated; // 시간 갱신 신호
        void Start(string task);
        void Stop();
    }
}