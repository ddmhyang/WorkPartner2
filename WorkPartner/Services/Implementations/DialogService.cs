// 파일: WorkPartner/Services/Implementations/DialogService.cs
using System.Windows;

namespace WorkPartner.Services.Implementations
{
    public class DialogService : IDialogService
    {
        public void ShowMessageBox(string message)
        {
            MessageBox.Show(message, "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ✨ [오류 수정] 이 메서드 전체를 DialogService.cs 클래스 내부에 추가하세요.
        public void ShowAlert(string message, string title)
        {
            // AlertWindow.xaml.cs 에 이 생성자가 있다고 가정합니다.
            var alertWindow = new AlertWindow(message, title);

            // 메인 윈도우를 주인으로 설정하여 화면 중앙에 표시되도록 합니다.
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                alertWindow.Owner = null;     // 👈 [수정 1] 소유권 연결을 끊습니다.
                alertWindow.Topmost = true;
                alertWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                // 메인 윈도우가 없으면 화면 중앙
                alertWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            alertWindow.ShowDialog();
        }
    }
}