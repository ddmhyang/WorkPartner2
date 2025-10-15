// 파일: Services/ITimerService.cs (수정 후)

using System;

namespace WorkPartner.Services
{
    public interface ITimerService
    {
        void Start();
        void Stop();
        bool IsRunning { get; }

        // ▼▼▼ 이 줄을 추가하여 통신 규칙을 정의합니다. ▼▼▼
        event Action<TimeSpan> TimeUpdated;
    }
}