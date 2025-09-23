using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WorkPartner
{
    public class AppSettings
    {
        [Key]
        public int Id { get; set; } // 단일 설정을 위한 기본 키
        public string Username { get; set; } = "사용자";
        public int Coins { get; set; } = 100;
        public string Theme { get; set; } = "Light"; // "Light" or "Dark"
        public string AccentColor { get; set; } = "#007ACC";

        // 직렬화하여 저장할 속성들
        public string EquippedItemsJson { get; set; }
        public string OwnedItemIdsJson { get; set; }
        public string CustomColorsJson { get; set; }
        public string WorkProcessesJson { get; set; }
        public string PassiveProcessesJson { get; set; }
        public string DistractionProcessesJson { get; set; }
        public string TagRulesJson { get; set; } // 이전에 Dictionary<string, string> 이었음
        public string TaskColorsJson { get; set; } // 이전에 Dictionary<string, string> 이었음


        [NotMapped]
        public Dictionary<ItemType, Guid> EquippedItems
        {
            get => string.IsNullOrEmpty(EquippedItemsJson) ? new Dictionary<ItemType, Guid>() : JsonSerializer.Deserialize<Dictionary<ItemType, Guid>>(EquippedItemsJson);
            set => EquippedItemsJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public List<Guid> OwnedItemIds
        {
            get => string.IsNullOrEmpty(OwnedItemIdsJson) ? new List<Guid>() : JsonSerializer.Deserialize<List<Guid>>(OwnedItemIdsJson);
            set => OwnedItemIdsJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public Dictionary<ItemType, string> CustomColors
        {
            get => string.IsNullOrEmpty(CustomColorsJson) ? new Dictionary<ItemType, string>() : JsonSerializer.Deserialize<Dictionary<ItemType, string>>(CustomColorsJson);
            set => CustomColorsJson = JsonSerializer.Serialize(value);
        }


        [NotMapped]
        public ObservableCollection<string> WorkProcesses
        {
            get => string.IsNullOrEmpty(WorkProcessesJson) ? new ObservableCollection<string>() : JsonSerializer.Deserialize<ObservableCollection<string>>(WorkProcessesJson);
            set => WorkProcessesJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public ObservableCollection<string> PassiveProcesses
        {
            get => string.IsNullOrEmpty(PassiveProcessesJson) ? new ObservableCollection<string>() : JsonSerializer.Deserialize<ObservableCollection<string>>(PassiveProcessesJson);
            set => PassiveProcessesJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public ObservableCollection<string> DistractionProcesses
        {
            get => string.IsNullOrEmpty(DistractionProcessesJson) ? new ObservableCollection<string>() : JsonSerializer.Deserialize<ObservableCollection<string>>(DistractionProcessesJson);
            set => DistractionProcessesJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public Dictionary<string, string> TagRules
        {
            get => string.IsNullOrEmpty(TagRulesJson) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(TagRulesJson);
            set => TagRulesJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public Dictionary<string, string> TaskColors
        {
            get => string.IsNullOrEmpty(TaskColorsJson) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(TaskColorsJson);
            set => TaskColorsJson = JsonSerializer.Serialize(value);
        }

        // --- 일반 설정 ---
        public bool IsIdleDetectionEnabled { get; set; } = true;
        public int IdleTimeoutSeconds { get; set; } = 300;
        public bool IsMiniTimerEnabled { get; set; } = false;
        public bool MiniTimerShowInfo { get; set; } = true;
        public bool MiniTimerShowCharacter { get; set; } = true;
        public bool MiniTimerShowBackground { get; set; } = true;
        public string FocusModeNagMessage { get; set; } = "집중하세요!";
        public int FocusModeNagIntervalSeconds { get; set; } = 60;

        // 생성자에서 초기화 로직을 간단하게 유지
        public AppSettings()
        {
            // 기본값은 위 속성 정의에서 처리됩니다.
            // 복잡한 초기화는 데이터베이스 생성 시 시딩(Seeding)으로 처리합니다. (AppDbContext.cs 참고)
        }
    }
}
