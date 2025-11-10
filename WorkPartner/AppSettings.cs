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
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public double PendingWorkMinutes { get; set; } = 0; // ◀◀ [이 줄 추가]
        public string CurrentTask { get; set; } = "없음";

        // ✨ [삭제] 기존 아이템 속성을 삭제합니다.
        // public Dictionary<ItemType, Guid> EquippedItems { get; set; }
        // public Dictionary<ItemType, string> CustomColors { get; set; }

        // ✨ [추가] 새 아바타 시스템을 위한 속성입니다.
        public List<Guid> OwnedItemIds { get; set; }

        /// <summary>
        /// 장신구를 제외한 모든 파츠. (유형별 1개만 착용 가능)
        /// Key: ItemType (Background, Cushion, HairBack, Body, HairFront, Eye, Mouth, Clothes)
        /// Value: 착용 아이템 정보 (ID 및 색상)
        /// </summary>
        public Dictionary<ItemType, EquippedItemInfo> EquippedParts { get; set; }

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
            OwnedItemIds = new List<Guid>();
            Coins = 100;
            Username = "User";
            IsIdleDetectionEnabled = true;
            IdleTimeoutSeconds = 300;
            FocusModeNagMessage = "작업에 집중할 시간입니다!";
            FocusModeNagIntervalSeconds = 60;

            // ✨ [수정] 새 구조체로 초기화합니다.
            EquippedParts = new Dictionary<ItemType, EquippedItemInfo>();

            // 사용자가 장착한 아이템이 없을 때만 기본 아이템 장착
            if (EquippedParts.Count == 0)
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

                // ✨ [수정] items_db.json에 정의된 새 기본 아이템 이름으로 변경
                var defaultItemsMap = new Dictionary<ItemType, string>
                {
                    { ItemType.Scalp, "기본 두피" },
                    { ItemType.Head, "기본 머리" },
                    { ItemType.Upper, "기본 상체" },
                    { ItemType.Lower, "기본 하체" },
                    // (참고: 배경, 얼굴, 머리 등은 AvatarPage에서 장착 해제/교체 가능하므로 여기서 기본값으로 설정하지 않아도 됩니다.)
                };

                foreach (var defaultItem in defaultItemsMap)
                {
                    // ✨ [수정] 기본 파츠는 Price: 0 이어야 함
                    var item = allItems.FirstOrDefault(i => i.Type == defaultItem.Key && i.Price == 0);

                    // 만약 Price 0인 기본 아이템이 여러 개라면, 이름으로 한 번 더 찾습니다.
                    if (item == null)
                    {
                        item = allItems.FirstOrDefault(i => i.Type == defaultItem.Key && i.Name == defaultItem.Value);
                    }

                    if (item != null)
                    {
                        // ✨ [수정] 새 구조체인 EquippedParts에 추가합니다.
                        EquippedParts[item.Type] = new EquippedItemInfo(item.Id, null); // 기본 색조 0

                        if (!OwnedItemIds.Contains(item.Id))
                        {
                            OwnedItemIds.Add(item.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 파일 읽기 실패 시 조용히 넘어감
                System.Diagnostics.Debug.WriteLine($"EquipDefaultItems failed: {ex.Message}");
            }
        }
    }
}