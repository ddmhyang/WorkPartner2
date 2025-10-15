using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WorkPartner
{
    // 캐릭터 외형 정보를 담는 클래스입니다.
    // 각 파츠의 ID와 색상 값을 저장합니다.
    public class CharacterAppearance
    {
        public string HairBack { get; set; } = "hairBack1";
        public string HairBackColor { get; set; } = "#FFC0CB"; // Default Pink

        public string HairFront { get; set; } = "hairFront1";
        public string HairFrontColor { get; set; } = "#FFC0CB"; // Default Pink

        public string Eye { get; set; } = "eye1";
        public string EyeColor { get; set; } = "#000000"; // Default Black

        public string Mouth { get; set; } = "mouth1";
        // 입은 현재 색상 변경 기능이 없으므로 색상 속성은 제외합니다.

        public string Clothes { get; set; } = "clothes1";
        public string ClothesColor { get; set; } = "#ADD8E6"; // Default Light Blue

        public string Cushion { get; set; } = "cushion1";
        public string CushionColor { get; set; } = "#90EE90"; // Default Light Green

        public string Background { get; set; } = "background1";

        // 장신구는 여러 개 착용 가능하므로 List로 관리합니다.
        public List<string> Accessories { get; set; } = new List<string>();
        public List<string> AccessoryColors { get; set; } = new List<string>();

        // 이 객체를 깊은 복사(Deep Copy)하는 메서드입니다.
        // '미리보기' 기능을 위해 원본과 복사본이 서로 영향을 주지 않도록 합니다.
        public CharacterAppearance Clone()
        {
            return new CharacterAppearance
            {
                HairBack = this.HairBack,
                HairBackColor = this.HairBackColor,
                HairFront = this.HairFront,
                HairFrontColor = this.HairFrontColor,
                Eye = this.Eye,
                EyeColor = this.EyeColor,
                Mouth = this.Mouth,
                Clothes = this.Clothes,
                ClothesColor = this.ClothesColor,
                Cushion = this.Cushion,
                CushionColor = this.CushionColor,
                Background = this.Background,
                // List는 ToList()를 사용하여 새로운 리스트로 복사합니다.
                Accessories = this.Accessories.ToList(),
                AccessoryColors = this.AccessoryColors.ToList()
            };
        }
    }


    public class AppSettings
    {
        public int TotalFocusTime { get; set; } = 0;
        public List<TimeLogEntry> TimeLog { get; set; } = new List<TimeLogEntry>();
        public List<string> AllowedApps { get; set; } = new List<string>();
        public int FocusTime { get; set; } = 25;
        public int BreakTime { get; set; } = 5;
        public int LongBreakTime { get; set; } = 15;
        public int LongBreakInterval { get; set; } = 4;
        public bool AutoStartBreak { get; set; } = false;
        public bool AutoStartFocus { get; set; } = false;
        public string SelectedSound { get; set; }
        public double SoundVolume { get; set; } = 0.5;
        public string Theme { get; set; } = "Light";
        public int Coins { get; set; } = 0;
        public List<string> OwnedItems { get; set; } = new List<string>();
        public bool IsPomodoro { get; set; } = true;
        public int DailyGoal { get; set; } = 2;
        public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        public List<MemoItem> Memos { get; set; } = new List<MemoItem>();
        public bool Topmost { get; set; }
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 800;
        public double Height { get; set; } = 600;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public bool ShowMiniTimerOnBreak { get; set; } = false;

        // 새로 추가된 캐릭터 외형 정보 속성입니다.
        public CharacterAppearance Appearance { get; set; } = new CharacterAppearance();
    }
}
