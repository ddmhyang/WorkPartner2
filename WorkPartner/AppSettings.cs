using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public class AppSettings
    {
        public int Coins { get; set; }
        public string Username { get; set; } = "사용자";
        public Dictionary<ItemType, Guid> EquippedItems { get; set; }
        public List<Guid> OwnedItemIds { get; set; }
        public Dictionary<ItemType, string> CustomColors { get; set; }

        // (Json 무시) 이 설정은 UI 상태를 위한 것이므로 저장하지 않습니다.
        [Newtonsoft.Json.JsonIgnore]
        public bool IsIdleDetectionEnabled { get; set; } = true;
        public int IdleTimeoutSeconds { get; set; } = 300;
        public bool IsMiniTimerEnabled { get; set; } = false;
        public ObservableCollection<string> WorkProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> PassiveProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DistractionProcesses { get; set; } = new ObservableCollection<string>();
        public string FocusModeNagMessage { get; set; } = "집중 모드 중입니다!";
        public int FocusModeNagIntervalSeconds { get; set; } = 60;
        public Dictionary<string, string> TagRules { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();

        public AppSettings()
        {
            // 초기화
            EquippedItems = new Dictionary<ItemType, Guid>();
            OwnedItemIds = new List<Guid>();
            CustomColors = new Dictionary<ItemType, string>();
            Coins = 100; // 시작 코인

            // 기본 아이템 장착 로직 호출
            EquipDefaultItems();
        }

        private void EquipDefaultItems()
        {
            if (!File.Exists(DataManager.ItemsDbFilePath)) return;

            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                var allItems = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();

                // 장착할 기본 아이템 이름 목록
                var defaultItemNames = new Dictionary<ItemType, string>
                {
                    { ItemType.HairStyle, "기본 머리" },
                    { ItemType.Clothes, "기본 옷" },
                    { ItemType.EyeShape, "기본 눈" },
                    { ItemType.MouthShape, "기본 입" },
                    { ItemType.Background, "기본 방석" },
                    { ItemType.Body, "기본 몸" },
                    { ItemType.HairColor, "검은색" }, // 기본 머리 색
                    { ItemType.EyeColor, "검은색 눈" },   // 기본 눈 색
                };

                foreach (var defaultItem in defaultItemNames)
                {
                    var item = allItems.FirstOrDefault(i => i.Type == defaultItem.Key && i.Name == defaultItem.Value);
                    if (item != null)
                    {
                        // 색상 아이템인 경우
                        if (!string.IsNullOrEmpty(item.ColorValue))
                        {
                            CustomColors[item.Type] = item.ColorValue;
                        }
                        // 일반 장착 아이템인 경우
                        else
                        {
                            EquippedItems[item.Type] = item.Id;
                        }

                        // 기본 아이템은 소유 목록에도 추가
                        if (!OwnedItemIds.Contains(item.Id))
                        {
                            OwnedItemIds.Add(item.Id);
                        }
                    }
                }
            }
            catch
            {
                // 파일 읽기 실패 시 조용히 넘어감
            }
        }
    }
}

