// 파일: WorkPartner/Services/Implementations/TimerService.cs
using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace WorkPartner.Services.Implementations
{
    /// <summary>
    /// DispatcherTimer와 Stopwatch를 사용하여 ITimerService를 실제로 구현한 클래스입니다.
    /// </summary>
    public class TimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch;

        public event Action<TimeSpan> Tick;

        public TimerService()
        {
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
        }


        private void OnTimerTick(object sender, EventArgs e)
        {
            // Tick 이벤트를 구독한 모든 곳에 스톱워치의 경과 시간을 알려줍니다.
            Tick?.Invoke(_stopwatch.Elapsed);
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

        // 참고: 스톱워치를 리셋하거나 현재 작업 시간을 기록하는 등의
        // 구체적인 로직은 이 서비스가 아닌 ViewModel이 담당합니다.
        // 이 서비스는 오직 '시간을 재고 알리는' 역할에만 충실합니다.
    }
}