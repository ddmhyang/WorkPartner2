// [최종 수정] WorkPartner/CharacterDisplay.xaml.cs
using System;
using System.Collections.Generic;
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
        // 아이템 DB 캐시
        private static List<ShopItem> _allItemsCache;
        private static DateTime _cacheTimestamp;

        // "원본" 회색조 이미지 경로(Uri) 저장 (이미지 사라짐 버그 해결용)
        private Uri _originalBackHairUri;
        private Uri _originalFrontHairUri;
        // 현재 적용된 "색상(Color)" 저장 (실시간 변경용)
        private Color _currentHairColor;


        public CharacterDisplay()
        {
            InitializeComponent();
            LoadItemsDbCache();
        }

        private void LoadItemsDbCache()
        {
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

        public void UpdateCharacter(AppSettings settings)
        {
            if (_allItemsCache == null)
            {
                LoadItemsDbCache();
            }

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

                        // ✨ [수정] ImageProcessor.ApplyColor (색상화) 호출
                        BitmapSource finalAccessory = ImageProcessor.ApplyColor(originalAccessory, tintColor);

                        accessoryViewModels.Add(new
                        {
                            ImagePath = finalAccessory
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

            try
            {
                Uri originalUri = new Uri(shopItem.ImagePath, UriKind.Relative);
                BitmapImage originalImage = new BitmapImage(originalUri);

                Color finalColor = Colors.White; // 기본값 (색 변경 안 함)
                if (shopItem.CanChangeColor && !string.IsNullOrEmpty(equippedInfo.ColorHex))
                {
                    try { finalColor = (Color)ColorConverter.ConvertFromString(equippedInfo.ColorHex); }
                    catch { /* ignore invalid hex */ }
                }

                // "원본" 헤어 경로(Uri)와 현재 "색상" 저장
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

                // ✨ [수정] ImageProcessor.ApplyColor (색상화) 호출
                BitmapSource finalImage = ImageProcessor.ApplyColor(originalImage, finalColor);

                imageControl.Source = finalImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                imageControl.Source = null;
            }

            // (중요) Effect 속성은 항상 null로 유지
            imageControl.Effect = null;
        }


        // 컬러 팔레트에서 호출할 함수 (C# "색상화" 방식 적용)
        public void SetPartColor(string partType, Color color)
        {
            if (partType == "Hair")
            {
                // 1. 뒷머리 변경 (저장된 "원본" Uri 사용)
                if (_originalBackHairUri != null)
                {
                    BitmapImage originalBack = new BitmapImage(_originalBackHairUri);
                    // ✨ [수정] ImageProcessor.ApplyColor (색상화) 호출
                    Part_BackHair.Source = ImageProcessor.ApplyColor(originalBack, color);
                }

                // 2. 앞머리 변경 (저장된 "원본" Uri 사용)
                if (_originalFrontHairUri != null)
                {
                    BitmapImage originalFront = new BitmapImage(_originalFrontHairUri);
                    // ✨ [수정] ImageProcessor.ApplyColor (색상화) 호출
                    Part_FrontHair.Source = ImageProcessor.ApplyColor(originalFront, color);
                }

                // 3. 현재 색상 저장
                _currentHairColor = color;
            }
        }
    }
}