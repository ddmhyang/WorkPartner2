// [수정] WorkPartner/CharacterDisplay.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        // 아이템 DB 캐시
        private static List<ShopItem> _allItemsCache;
        private static DateTime _cacheTimestamp;

        public CharacterDisplay()
        {
            InitializeComponent();
            LoadItemsDbCache();
        }

        /// <summary>
        /// 아이템 DB를 읽어와 캐시합니다. (성능 향상)
        /// </summary>
        private void LoadItemsDbCache()
        {
            // 5분이 지나지 않았으면 캐시 사용
            if (_allItemsCache != null && (DateTime.Now - _cacheTimestamp).TotalMinutes < 5)
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                _allItemsCache = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
                _cacheTimestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"아이템 DB 캐시 로딩 실패: {ex.Message}");
                _allItemsCache = new List<ShopItem>();
            }
        }
        public void UpdateCharacter()
        {
            var settings = DataManager.LoadSettings();
            UpdateCharacter(settings);
        }
        /// <summary>
        /// 저장된 설정(settings.json)을 기준으로 캐릭터를 업데이트합니다.
        /// </summary>
        public void UpdateCharacter(AppSettings settings)
        {
            if (_allItemsCache == null)
            {
                LoadItemsDbCache();
            }

            // 1. 단일 파츠 렌더링 (EquippedParts)
            // ✨ [수정] XAML과 1:1로 매칭되는 새 파츠 로직으로 변경
            SetImagePart(Part_Background, settings.EquippedParts.GetValueOrDefault(ItemType.Background));
            SetImagePart(Part_Tail, settings.EquippedParts.GetValueOrDefault(ItemType.Tail));
            SetImagePart(Part_Lower, settings.EquippedParts.GetValueOrDefault(ItemType.Lower));
            SetImagePart(Part_Bottom, settings.EquippedParts.GetValueOrDefault(ItemType.Bottom));
            SetImagePart(Part_Upper, settings.EquippedParts.GetValueOrDefault(ItemType.Upper));
            SetImagePart(Part_Top, settings.EquippedParts.GetValueOrDefault(ItemType.Top));
            SetImagePart(Part_Outerwear, settings.EquippedParts.GetValueOrDefault(ItemType.Outerwear));
            SetImagePart(Part_Head, settings.EquippedParts.GetValueOrDefault(ItemType.Head));
            SetImagePart(Part_Scalp, settings.EquippedParts.GetValueOrDefault(ItemType.Scalp));
            SetImagePart(Part_BackHair, settings.EquippedParts.GetValueOrDefault(ItemType.BackHair));
            SetImagePart(Part_Face, settings.EquippedParts.GetValueOrDefault(ItemType.Face));
            SetImagePart(Part_AnimalEar, settings.EquippedParts.GetValueOrDefault(ItemType.AnimalEar));
            SetImagePart(Part_FrontHair, settings.EquippedParts.GetValueOrDefault(ItemType.FrontHair));


            // 2. 장신구(Accessories) 렌더링 (EquippedAccessories)
            // ✨ [수정] ItemType.Accessories -> ItemType.Accessory로 변경
            var accessoryViewModels = new List<object>();
            foreach (var accessoryInfo in settings.EquippedAccessories)
            {
                var shopItem = _allItemsCache.FirstOrDefault(i => i.Id == accessoryInfo.ItemId);

                // ✨ [추가] ItemType.Accessory 타입인지 한 번 더 확인 (안전장치)
                if (shopItem != null && shopItem.Type == ItemType.Accessory)
                {
                    try
                    {
                        accessoryViewModels.Add(new
                        {
                            ImagePath = new BitmapImage(new Uri(shopItem.ImagePath, UriKind.Relative)),
                            HueShift = accessoryInfo.HueShift
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"장신구 이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                    }
                }
            }
            Part_Accessories.ItemsSource = accessoryViewModels;
        }

        /// <summary>
        /// 지정된 Image 컨트롤에 아이템 파츠와 색조 효과를 적용합니다.
        /// </summary>
        private void SetImagePart(Image imageControl, EquippedItemInfo equippedInfo)
        {
            if (equippedInfo == null || equippedInfo.ItemId == Guid.Empty)
            {
                imageControl.Source = null;
                imageControl.Effect = null;
                return;
            }

            var shopItem = _allItemsCache.FirstOrDefault(i => i.Id == equippedInfo.ItemId);
            if (shopItem == null || string.IsNullOrEmpty(shopItem.ImagePath))
            {
                imageControl.Source = null;
                imageControl.Effect = null;
                return;
            }

            // 1. 이미지 소스 설정
            try
            {
                imageControl.Source = new BitmapImage(new Uri(shopItem.ImagePath, UriKind.Relative));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                imageControl.Source = null;
            }

            // 2. 색조 변경(Hue Shift) 효과 적용
            if (shopItem.CanChangeColor && equippedInfo.HueShift != 0)
            {
                imageControl.Effect = new HueShiftEffect { HueShift = equippedInfo.HueShift };
            }
            else
            {
                imageControl.Effect = null; // 효과 없음
            }
        }
    }
}