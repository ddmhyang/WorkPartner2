using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkPartner
{
    public class AppSettings
    {
        // 🗑️ [삭제됨] Username 변수 제거

        // ▼ 사용자가 설정한 이미지(움짤) 경로
        public string UserImagePath { get; set; }

        // --- 작업 상태 저장 ---
        public double PendingWorkMinutes { get; set; } = 0;
        public string CurrentTask { get; set; } = "없음";

        // --- 테마 및 디자인 ---
        public string Theme { get; set; } = "Light";
        public string AccentColor { get; set; } = "#2195F2";

        // --- 미니 타이머 설정 ---
        public bool MiniTimerShowInfo { get; set; } = true;

        // ▼ [핵심] 미니 타이머에 캐릭터(이미지)를 띄울지 여부
        public bool MiniTimerShowCharacter { get; set; } = true;

        public bool MiniTimerShowBackground { get; set; } = true;

        // --- 집중 모드 & 감지 설정 ---
        public bool IsIdleDetectionEnabled { get; set; } = true;
        public int IdleTimeoutSeconds { get; set; } = 300;
        public bool IsMiniTimerEnabled { get; set; } = false;
        public bool IsFocusModeEnabled { get; set; } = false;
        public string FocusModeNagMessage { get; set; } = "집중 모드 중입니다!";
        public int FocusModeNagIntervalSeconds { get; set; } = 60;

        // --- 리스트 데이터 ---
        public ObservableCollection<string> WorkProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DistractionProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> PassiveProcesses { get; set; } = new ObservableCollection<string>();
        public Dictionary<string, string> TagRules { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();

        public AppSettings() { }
    }
}