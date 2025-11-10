// 🎯 [수정] WorkPartner/Services/IDialogService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// 파일: WorkPartner/Services/IDialogService.cs
namespace WorkPartner.Services
{
    public interface IDialogService
    {
        void ShowMessageBox(string message);
        void ShowAlert(string message, string title);
    }
}