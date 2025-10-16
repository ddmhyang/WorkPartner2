// 파일: WorkPartner/Services/ITimerService.cs
using System;

namespace WorkPartner.Services
{
    /// <summary>
    /// 1초마다 Tick 이벤트를 발생시키는 타이머 서비스의 명세서(인터페이스)입니다.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// 타이머가 1초마다 발생시키는 이벤트입니다.
        /// </summary>
        event Action<TimeSpan> Tick;

        /// <summary>
        /// 타이머를 시작합니다.
        /// </summary>
        void Start();

        /// <summary>
        /// 타이머를 정지합니다.
        /// </summary>
        void Stop();
    }
}