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
        public int Coins { get; set; }
        public string Username { get; set; } = "사용자";
        public Dictionary<ItemType, Guid> EquippedItems { get; set; }
        public List<Guid> OwnedItemIds { get; set; }
        public Dictionary<ItemType, string> CustomColors { get; set; }

        // --- 새로 추가된 개인 설정 ---
        public string Theme { get; set; } = "Light"; // "Light" or "Dark"
        public string AccentColor { get; set; } = "#007ACC"; // 기본 파란색

        // --- 미니 타이머 세부 설정 ---
        public bool MiniTimerShowInfo { get; set; } = true;
        public bool MiniTimerShowCharacter { get; set; } = true;
        public bool MiniTimerShowBackground { get; set; } = true;
        // --------------------------

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
            EquippedItems = new Dictionary<ItemType, Guid>();
            OwnedItemIds = new List<Guid>();
            CustomColors = new Dictionary<ItemType, string>();
            Coins = 100;

            // 사용자가 장착한 아이템이 없을 때만 기본 아이템 장착
            if (EquippedItems == null || EquippedItems.Count == 0)
            {
                EquipDefaultItems();
            }
        }

        private void EquipDefaultItems()
        {
            if (!File.Exists(DataManager.ItemsDbFilePath)) return;

            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                var allItems = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();

                var defaultItemNames = new Dictionary<ItemType, string>
                {
                    { ItemType.HairStyle, "기본 머리" },
                    { ItemType.Clothes, "기본 옷" },
                    { ItemType.EyeShape, "기본 눈" },
                    { ItemType.MouthShape, "기본 입" },
                    { ItemType.Background, "기본 방석" },
                    { ItemType.Body, "기본 몸" },
                    { ItemType.HairColor, "검은색" },
                    { ItemType.EyeColor, "검은색 눈" },
                };

                foreach (var defaultItem in defaultItemNames)
                {
                    var item = allItems.FirstOrDefault(i => i.Type == defaultItem.Key && i.Name == defaultItem.Value);
                    if (item != null)
                    {
                        if (!string.IsNullOrEmpty(item.ColorValue))
                        {
                            CustomColors[item.Type] = item.ColorValue;
                        }
                        else
                        {
                            EquippedItems[item.Type] = item.Id;
                        }

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

