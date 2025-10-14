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
    }
}