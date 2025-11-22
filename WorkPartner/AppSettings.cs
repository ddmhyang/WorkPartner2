using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkPartner
{
    public class AppSettings
    {
        public string UserImagePath { get; set; }

        public double PendingWorkMinutes { get; set; } = 0;
        public string CurrentTask { get; set; } = "없음";

        public string Theme { get; set; } = "Light";
        public string AccentColor { get; set; } = "#2195F2";

        public bool MiniTimerShowInfo { get; set; } = true;

        public bool MiniTimerShowCharacter { get; set; } = true;

        public bool MiniTimerShowBackground { get; set; } = true;

        public bool IsIdleDetectionEnabled { get; set; } = true;
        public int IdleTimeoutSeconds { get; set; } = 300;
        public bool IsMiniTimerEnabled { get; set; } = false;
        public bool IsFocusModeEnabled { get; set; } = false;
        public string FocusModeNagMessage { get; set; } = "집중 모드 중입니다!";
        public int FocusModeNagIntervalSeconds { get; set; } = 60;

        public ObservableCollection<string> WorkProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DistractionProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> PassiveProcesses { get; set; } = new ObservableCollection<string>();
        public Dictionary<string, string> TagRules { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();

        public AppSettings() { }
    }
}