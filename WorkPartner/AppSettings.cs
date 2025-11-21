using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Linq;

namespace WorkPartner
{
    public class AppSettings
    {
        public string Username { get; set; } = "사용자";
        public double PendingWorkMinutes { get; set; } = 0; // ◀◀ [이 줄 추가]
        public string CurrentTask { get; set; } = "없음";

        // ✨ [추가] 새 아바타 시스템을 위한 속성입니다.


        /// <summary>
        /// 장신구 파츠. (여러 개 중복 착용 가능)
        /// </summary>


        // --- 새로 추가된 개인 설정 ---
        public string Theme { get; set; } = "Light";
        public string AccentColor { get; set; } = "#2195F2";

        // --- 미니 타이머 세부 설정 ---
        public bool MiniTimerShowInfo { get; set; } = true;
        public bool MiniTimerShowCharacter { get; set; } = true;
        public bool MiniTimerShowBackground { get; set; } = true;
        // --------------------------

        public bool IsIdleDetectionEnabled { get; set; } = true;
        public int IdleTimeoutSeconds { get; set; } = 300;
        public bool IsMiniTimerEnabled { get; set; } = false;
        public bool IsFocusModeEnabled { get; set; } = false;
        public string FocusModeNagMessage { get; set; } = "집중 모드 중입니다!";
        public int FocusModeNagIntervalSeconds { get; set; } = 60;
        public Dictionary<string, string> TagRules { get; set; } = new Dictionary<string, string>();
        public ObservableCollection<string> WorkProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DistractionProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> PassiveProcesses { get; set; } = new ObservableCollection<string>();

        public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();


        public AppSettings()
        {
            Username = "User";
            IsIdleDetectionEnabled = true;
            IdleTimeoutSeconds = 300;
            FocusModeNagMessage = "작업에 집중할 시간입니다!";
            FocusModeNagIntervalSeconds = 60;
        }

    }
}