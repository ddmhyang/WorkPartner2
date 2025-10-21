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

        /// <summary>
        /// 저장된 설정(settings.json)을 기준으로 캐릭터를 업데이트합니다.
        /// </summary>
        public void UpdateCharacter()
        {
            var settings = DataManager.LoadSettings();
            UpdateCharacter(settings);
        }

        /// <summary>
        /// '미리보기'를 위해 특정 AppSettings 객체를 기준으로 캐릭터를 렌더링합니다.
        /// </summary>
        public void UpdateCharacter(AppSettings settings)
        {
            if (_allItemsCache == null)
            {
                LoadItemsDbCache();
            }

            // 1. 단일 파츠 렌더링 (EquippedParts)
            SetImagePart(Part_Background, settings.EquippedParts.GetValueOrDefault(ItemType.Background));
            SetImagePart(Part_Cushion, settings.EquippedParts.GetValueOrDefault(ItemType.Cushion));
            SetImagePart(Part_HairBack, settings.EquippedParts.GetValueOrDefault(ItemType.HairBack));
            SetImagePart(Part_Body, settings.EquippedParts.GetValueOrDefault(ItemType.Body));
            SetImagePart(Part_HairFront, settings.EquippedParts.GetValueOrDefault(ItemType.HairFront));
            SetImagePart(Part_Eye, settings.EquippedParts.GetValueOrDefault(ItemType.Eye));
            SetImagePart(Part_Mouth, settings.EquippedParts.GetValueOrDefault(ItemType.Mouth));
            SetImagePart(Part_Clothes, settings.EquippedParts.GetValueOrDefault(ItemType.Clothes));

            // 2. 장신구(Accessories) 렌더링 (EquippedAccessories)
            // XAML의 DataTemplate을 사용하는 대신, 코드 비하인드에서 직접 ItemsSource를 설정합니다.
            // ItemsControl이 EquippedItemInfo 객체의 리스트를 받아 처리하도록 합니다.
            // DataTemplate이 바인딩을 처리하므로 C# 코드는 간단해집니다.
            var accessoryViewModels = new List<object>();
            foreach (var accessoryInfo in settings.EquippedAccessories)
            {
                var shopItem = _allItemsCache.FirstOrDefault(i => i.Id == accessoryInfo.ItemId);
                if (shopItem != null)
                {
                    // DataTemplate이 바인딩할 수 있도록 익명 객체 생성
                    accessoryViewModels.Add(new
                    {
                        ImagePath = new BitmapImage(new Uri(shopItem.ImagePath, UriKind.Relative)),
                        HueShift = accessoryInfo.HueShift
                    });
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