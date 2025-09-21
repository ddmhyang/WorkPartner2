// InstalledProgram.cs
using System.Windows.Media;

namespace WorkPartner
{
    public class InstalledProgram
    {
        public string DisplayName { get; set; }
        public string ProcessName { get; set; }
        public ImageSource Icon { get; set; }
        public string IconPath { get; set; } // <-- 이 줄을 추가하세요!
    }
}