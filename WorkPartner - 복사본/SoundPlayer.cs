using System;
using System.Media; // 효과음 재생을 위해 추가

namespace WorkPartner
{
    /// <summary>
    /// 앱 전체에서 사용할 효과음을 재생하는 정적 클래스입니다.
    /// </summary>
    public static class SoundPlayer
    {
        // TODO: 나중에 실제 사운드 파일 경로로 교체하세요.
        // 예: private static readonly SoundPlayer PurchaseSound = new SoundPlayer("Sounds/purchase.wav");

        /// <summary>
        /// 과제 완료, 코인 획득 등 긍정적인 행동에 대한 효과음을 재생합니다.
        /// </summary>
        public static void PlayCompleteSound()
        {
            try
            {
                // 임시로 기본 시스템 소리를 재생합니다.
                SystemSounds.Asterisk.Play();
            }
            catch (Exception)
            {
                // 사운드 재생에 실패해도 프로그램이 중단되지 않도록 합니다.
            }
        }

        /// <summary>
        /// 상점에서 아이템 구매 시 효과음을 재생합니다.
        /// </summary>
        public static void PlayPurchaseSound()
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// AI 비서의 알림 등 주목이 필요할 때 효과음을 재생합니다.
        /// </summary>
        public static void PlayNotificationSound()
        {
            try
            {
                SystemSounds.Beep.Play();
            }
            catch (Exception) { }
        }
    }
}
