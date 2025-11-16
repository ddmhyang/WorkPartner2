// 파일: WorkPartner/Services/ITimerService.cs
using System;

namespace WorkPartner.Services
{
    public interface ITimerService
    {
        event Action<TimeSpan> Tick;
        void Start();
        void Stop();
    }
}