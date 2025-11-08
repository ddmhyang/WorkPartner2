// [최종 수정] WorkPartner/CharacterDisplay.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics; // ✨ 콘솔 출력을 위해 추가
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WorkPartner;
using DrawingColor = System.Drawing.Color; // ⚠️ 1단계에서 System.Drawing.Common NuGet 패키지 설치 필수!

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        // ( ... 기존 변수들 ... )
        private static List<ShopItem> _allItemsCache;
        private static DateTime _cacheTimestamp;
        private Uri _originalBackHairUri;
        private Uri _originalFrontHairUri;
        private Color _currentHairColor;

        public CharacterDisplay()
        {
            InitializeComponent();
            LoadItemsDbCache();
        }

        private void LoadItemsDbCache()
        {
            // ( ... 기존 코드 ... )
            if (_allItemsCache != null && (DateTime.Now - _cacheTimestamp).TotalMinutes < 5) return;
            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                _allItemsCache = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
                _cacheTimestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"아이템 DB 캐시 로딩 실패: {ex.Message}");
                _allItemsCache = new List<ShopItem>();
            }
        }

        public void UpdateCharacter()
        {
            Debug.WriteLine("[CharacterDisplay] UpdateCharacter() 호출됨 (설정 로드)");
            var settings = DataManager.LoadSettings();
            UpdateCharacter(settings);
        }
        
        public void UpdateCharacter(AppSettings settings)
        {
            Debug.WriteLine("[CharacterDisplay] UpdateCharacter(settings) 실행 시작");
            if (_allItemsCache == null) LoadItemsDbCache();

            // 1. 단일 파츠 렌더링
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

            // 2. 장신구(Accessories) 렌더링
            var accessoryViewModels = new List<object>();
            foreach (var accessoryInfo in settings.EquippedAccessories)
            {
                // ( ... 기존 코드 ... )
                var shopItem = _allItemsCache.FirstOrDefault(i => i.Id == accessoryInfo.ItemId);
                if (shopItem != null && shopItem.Type == ItemType.Accessory)
                {
                    try
                    {
                        BitmapImage originalAccessory = new BitmapImage(new Uri(shopItem.ImagePath, UriKind.Relative));
                        Color tintColor = Colors.White;
                        if (shopItem.CanChangeColor && !string.IsNullOrEmpty(accessoryInfo.ColorHex))
                        {
                            try { tintColor = (Color)ColorConverter.ConvertFromString(accessoryInfo.ColorHex); }
                            catch { /* ignore invalid hex */ }
                        }
                        
                        Debug.WriteLine($"[CharacterDisplay] 장신구 '{shopItem.Name}' 색상 적용: {tintColor}");
                        BitmapSource finalAccessory = ImageProcessor.ApplyTint(originalAccessory, tintColor);
                        accessoryViewModels.Add(new { ImagePath = finalAccessory });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"장신구 이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                    }
                }
            }
            Part_Accessories.ItemsSource = accessoryViewModels;
            Debug.WriteLine("[CharacterDisplay] UpdateCharacter(settings) 실행 완료");
        }

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
            
            try
            {
                Uri originalUri = new Uri(shopItem.ImagePath, UriKind.Relative);
                BitmapImage originalImage = new BitmapImage(originalUri);
                Color finalColor = Colors.White; 

                if (shopItem.CanChangeColor && !string.IsNullOrEmpty(equippedInfo.ColorHex))
                {
                    try { finalColor = (Color)ColorConverter.ConvertFromString(equippedInfo.ColorHex); }
                    catch { /* ignore invalid hex */ }
                }

                if (imageControl == Part_BackHair)
                {
                    _originalBackHairUri = originalUri;
                    _currentHairColor = finalColor;
                }
                else if (imageControl == Part_FrontHair)
                {
                    _originalFrontHairUri = originalUri;
                    _currentHairColor = finalColor;
                }

                Debug.WriteLine($"[CharacterDisplay] SetImagePart '{shopItem.Name}' 색상 적용: {finalColor}");
                // ✨ [수정] ImageProcessor.ApplyTint (곱하기) 호출
                BitmapSource finalImage = ImageProcessor.ApplyTint(originalImage, finalColor);
                imageControl.Source = finalImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                imageControl.Source = null;
            }
            
            imageControl.Effect = null;
        }

        // 컬러 팔레트에서 호출할 함수 (C# "곱하기" 방식 적용)
        public void SetPartColor(string partType, Color color)
        {
            // ✨ [로그 추가]
            Debug.WriteLine($"[CharacterDisplay] SetPartColor 호출됨. 파츠: {partType}, 새 색상: {color}");

            if (partType == "Hair")
            {
                if (_originalBackHairUri != null)
                {
                    Debug.WriteLine("[CharacterDisplay] 뒷머리 색상 적용 중...");
                    BitmapImage originalBack = new BitmapImage(_originalBackHairUri);
                    Part_BackHair.Source = ImageProcessor.ApplyTint(originalBack, color);
                }

                if (_originalFrontHairUri != null)
                {
                    Debug.WriteLine("[CharacterDisplay] 앞머리 색상 적용 중...");
                    BitmapImage originalFront = new BitmapImage(_originalFrontHairUri);
                    Part_FrontHair.Source = ImageProcessor.ApplyTint(originalFront, color);
                }
                _currentHairColor = color;
            }
        }
    }
}