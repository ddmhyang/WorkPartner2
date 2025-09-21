using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading; // Mutex를 사용하기 위해 추가해야 합니다.

namespace WorkPartner
{
    public partial class App : Application
    {
        // Mutex 객체를 앱 전체에서 사용할 수 있도록 멤버 변수로 선언합니다.
        // "WorkPartnerMutex"는 우리 앱만의 고유한 이름입니다. 아무거나 상관없어요.
        private static Mutex mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "WorkPartnerMutex";
            bool createdNew;

            // appName이라는 이름으로 Mutex를 요청합니다.
            // createdNew 변수는 깃발을 새로 꽂았는지(true) 아닌지(false) 알려줍니다.
            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // 깃발을 새로 꽂지 못했다면, 이미 앱이 실행 중이라는 의미입니다.
                // 사용자에게 알리고 현재 시작하려는 앱은 종료합니다.
                MessageBox.Show("WorkPartner가 이미 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            }

            // base.OnStartup(e)는 원래 있던 코드이므로 그대로 둡니다.
            // 이 코드는 프로그램의 나머지 시작 프로세스를 처리합니다.
            base.OnStartup(e);
        }
    }
}