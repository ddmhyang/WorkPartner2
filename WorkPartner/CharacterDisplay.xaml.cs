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
using DrawingColor = System.Drawing.Color;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        // 아이템 DB 캐시
        private static List<ShopItem> _allItemsCache;
        private static DateTime _cacheTimestamp;

        // "원본" 회색조 이미지 경로(string) 저장 (이미지 사라짐 버그 해결용)
        private string _originalBackHairUriPath;
        private string _originalFrontHairUriPath;
        private Color _currentHairColor;

        // ✨ [추가] 2, 3번 목표: 원본 경로 및 색상 저장용
        private string _originalScalpUriPath;

        private string _originalFaceUriPath;
        private string _originalUpperUriPath;
        private string _originalLowerUriPath;
        private Color _currentSkinColor;


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

        /// <summary>
        /// 이미지를 메모리에 즉시 로드하는 헬퍼 메서드 (경로 오류 방지용)
        /// </summary>
        private BitmapImage LoadBitmapImageOnLoad(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            try
            {
                if (relativePath.StartsWith("/"))
                {
                    relativePath = relativePath.Substring(1);
                }

                Uri packUri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = packUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadBitmapImageOnLoad] 이미지 로드 실패 {relativePath}: {ex.Message}");
                return null;
            }
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
            SetImagePart(Part_Shoes, settings.EquippedParts.GetValueOrDefault(ItemType.Shoes));
            SetImagePart(Part_Upper, settings.EquippedParts.GetValueOrDefault(ItemType.Upper));
            SetImagePart(Part_Top, settings.EquippedParts.GetValueOrDefault(ItemType.Top));
            SetImagePart(Part_Outerwear, settings.EquippedParts.GetValueOrDefault(ItemType.Outerwear));
            SetImagePart(Part_Head, settings.EquippedParts.GetValueOrDefault(ItemType.Head));
            SetImagePart(Part_Scalp, settings.EquippedParts.GetValueOrDefault(ItemType.Scalp));
            SetImagePart(Part_BackHair, settings.EquippedParts.GetValueOrDefault(ItemType.BackHair));
            SetImagePart(Part_Face, settings.EquippedParts.GetValueOrDefault(ItemType.Face));
            SetImagePart(Part_AnimalEar, settings.EquippedParts.GetValueOrDefault(ItemType.AnimalEar));
            SetImagePart(Part_Accessory, settings.EquippedParts.GetValueOrDefault(ItemType.Accessory));
            SetImagePart(Part_FrontHair, settings.EquippedParts.GetValueOrDefault(ItemType.FrontHair));

            // 2. 장신구 렌더링 로직은 이전 단계에서 SetImagePart로 통합됨
        }

        /// <summary>
        /// 지정된 Image 컨트롤에 아이템 파츠와 색조 효과를 적용합니다.
        /// </summary>
        private void SetImagePart(Image imageControl, EquippedItemInfo equippedInfo)
        {
            if (imageControl == null) return; // (안전 코드)

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
                BitmapImage originalImage = LoadBitmapImageOnLoad(shopItem.ImagePath);

                Color finalColor = Colors.White; // 기본값 (색 변경 안 함)

                // ✨ [수정] 3번 목표: 피부 파츠는 CanChangeColor=false여도 강제로 색상 적용
                bool isSkinPart = (imageControl == Part_Face || imageControl == Part_Upper || imageControl == Part_Lower);

                // (색상 변경 가능 파츠 || 피부 파츠) && 색상값이 있으면
                if ((shopItem.CanChangeColor || isSkinPart) && !string.IsNullOrEmpty(equippedInfo.ColorHex))
                {
                    try { finalColor = (Color)ColorConverter.ConvertFromString(equippedInfo.ColorHex); }
                    catch { /* ignore invalid hex */ }
                }

                // ✨ [수정] 2, 3번 목표: 원본 경로 저장 로직 확장
                if (imageControl == Part_BackHair)
                {
                    _originalBackHairUriPath = shopItem.ImagePath;
                    _currentHairColor = finalColor;
                }
                else if (imageControl == Part_FrontHair)
                {
                    _originalFrontHairUriPath = shopItem.ImagePath;
                    _currentHairColor = finalColor;
                }
                else if (imageControl == Part_Scalp) // (2번 목표)
                {
                    _originalScalpUriPath = shopItem.ImagePath;
                    _currentHairColor = finalColor;
                }
                else if (imageControl == Part_Face) // (3번 목표)
                {
                    _originalFaceUriPath = shopItem.ImagePath;
                    _currentSkinColor = finalColor;
                }
                else if (imageControl == Part_Upper) // (3번 목표)
                {
                    _originalUpperUriPath = shopItem.ImagePath;
                    _currentSkinColor = finalColor;
                }
                else if (imageControl == Part_Lower) // (3번 목표)
                {
                    _originalLowerUriPath = shopItem.ImagePath;
                    _currentSkinColor = finalColor;
                }

                BitmapSource finalImage = ImageProcessor.ApplyColor(originalImage, finalColor);
                imageControl.Source = finalImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이미지 로드 실패 {shopItem.ImagePath}: {ex.Message}");
                imageControl.Source = null;
            }

            imageControl.Effect = null;
        }


        // 컬러 팔레트에서 호출할 함수 (C# "색상화" 방식 적용)
        public void SetPartColor(string partType, Color color)
        {
            // ✨ [수정] 2번 목표: 머리색 연동
            if (partType == "Hair")
            {
                // 1. 뒷머리
                if (Part_BackHair != null && !string.IsNullOrEmpty(_originalBackHairUriPath))
                {
                    BitmapImage originalBack = LoadBitmapImageOnLoad(_originalBackHairUriPath);
                    Part_BackHair.Source = ImageProcessor.ApplyColor(originalBack, color);
                }

                // 2. 앞머리
                if (Part_FrontHair != null && !string.IsNullOrEmpty(_originalFrontHairUriPath))
                {
                    BitmapImage originalFront = LoadBitmapImageOnLoad(_originalFrontHairUriPath);
                    Part_FrontHair.Source = ImageProcessor.ApplyColor(originalFront, color);
                }

                // 3. 두피 (Scalp)
                if (Part_Scalp != null && !string.IsNullOrEmpty(_originalScalpUriPath))
                {
                    BitmapImage originalScalp = LoadBitmapImageOnLoad(_originalScalpUriPath);
                    Part_Scalp.Source = ImageProcessor.ApplyColor(originalScalp, color);
                }

                _currentHairColor = color;
            }
            // ✨ [수정] 3번 목표: 피부색 연동
            else if (partType == "Skin")
            {
                // 1. 얼굴
                if (Part_Face != null && !string.IsNullOrEmpty(_originalFaceUriPath))
                {
                    BitmapImage originalFace = LoadBitmapImageOnLoad(_originalFaceUriPath);
                    Part_Face.Source = ImageProcessor.ApplyColor(originalFace, color);
                }

                // 2. 상체
                if (Part_Upper != null && !string.IsNullOrEmpty(_originalUpperUriPath))
                {
                    BitmapImage originalUpper = LoadBitmapImageOnLoad(_originalUpperUriPath);
                    Part_Upper.Source = ImageProcessor.ApplyColor(originalUpper, color);
                }

                // 3. 하체
                if (Part_Lower != null && !string.IsNullOrEmpty(_originalLowerUriPath))
                {
                    BitmapImage originalLower = LoadBitmapImageOnLoad(_originalLowerUriPath);
                    Part_Lower.Source = ImageProcessor.ApplyColor(originalLower, color);
                }

                _currentSkinColor = color;
            }
        }
    }
}