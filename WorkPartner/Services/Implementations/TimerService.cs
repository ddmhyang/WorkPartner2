// 파일: Services/Implementations/TimerService.cs (수정 후)

using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace WorkPartner.Services.Implementations
{
    public class TimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch;

        // ▼▼▼ 인터페이스 규칙을 구현하는 이벤트입니다. ▼▼▼
        public event Action<TimeSpan> TimeUpdated;

        // 기존 Tick 이벤트는 내부적으로만 사용되거나 삭제될 수 있습니다.
        // public event EventHandler Tick; 

        public bool IsRunning => _timer.IsEnabled;

        public TimerService()
        {
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            // ▼▼▼ 타이머가 Tick할 때마다 TimeUpdated 이벤트를 발생시켜 경과 시간을 알립니다. ▼▼▼
            _timer.Tick += (s, e) => TimeUpdated?.Invoke(_stopwatch.Elapsed);
        }

        public void Start()
        {
            _stopwatch.Start();
            _timer.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
            _timer.Stop();
        }
    }
}