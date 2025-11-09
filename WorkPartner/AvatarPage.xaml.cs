// [최종 수정] WorkPartner/AvatarPage.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public partial class AvatarPage : UserControl
    {
        private AppSettings _savedSettings; // 실제 저장된 설정
        private AppSettings _previewSettings; // 미리보기용 임시 설정

        private List<ShopItem> _allItems;
        private ItemType _selectedCategory = ItemType.Background; // 기본 카테고리
        private Button _selectedCategoryButton = null;

        private ShopItem _selectedShopItem; // 현재 상점에서 선택한 아이템
        private Border _selectedItemBorder; // 현재 상점에서 선택한 아이템의 테두리

        private HslColorPicker _hslPicker;
        private AvatarPageHelpers _avatarHelpers;


        public AvatarPage()
        {
            InitializeComponent();

            _hslPicker = FindName("ItemHslPicker") as HslColorPicker;
            _avatarHelpers = new AvatarPageHelpers();

            if (_hslPicker != null)
            {
                _hslPicker.ColorChanged += ColorPalette_ColorChanged;
            }

            LoadData();
        }

        public void LoadData()
        {
            _savedSettings = DataManager.LoadSettings();
            _previewSettings = DeepClone(_savedSettings);

            CoinDisplay.Text = _savedSettings.Coins.ToString("N0");

            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                _allItems = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"아이템 DB 로딩 실패: {ex.Message}");
                _allItems = new List<ShopItem>();
            }


            CharacterPreview.UpdateCharacter(_previewSettings);
            PopulateCategories();
            UpdateItemList();
            UpdateControlsVisibility();
        }

        #region 카테고리 및 아이템 목록 UI
        private void PopulateCategories()
        {
            CategoryPanel.Children.Clear();
            var categories = _allItems.Select(i => i.Type).Distinct().OrderBy(t => GetCategoryOrder(t));
            var basicPartsToExclude = new[] { ItemType.Scalp, ItemType.Head, ItemType.Upper, ItemType.Lower };

            foreach (var category in categories)
            {
                if (basicPartsToExclude.Contains(category)) continue;

                var button = new Button
                {
                    Content = GetCategoryDisplayName(category),
                    Tag = category,
                    Style = (Style)FindResource("CategoryButtonStyle")
                };
                button.Click += CategoryButton_Click;
                CategoryPanel.Children.Add(button);

                if (category == _selectedCategory)
                {
                    button.Style = (Style)FindResource("SelectedCategoryButtonStyle");
                    _selectedCategoryButton = button;
                }
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ItemType category)
            {
                _selectedCategory = category;

                if (_selectedCategoryButton != null)
                {
                    _selectedCategoryButton.Style = (Style)FindResource("CategoryButtonStyle");
                }
                button.Style = (Style)FindResource("SelectedCategoryButtonStyle");
                _selectedCategoryButton = button;

                UpdateItemList();
                UpdateControlsVisibility();
            }
        }

        private void UpdateItemList()
        {
            ItemPanel.Children.Clear();
            _selectedItemBorder = null;

            var itemsInCategory = _allItems.Where(i => i.Type == _selectedCategory);

            foreach (var item in itemsInCategory)
            {
                var itemView = CreateItemView(item);
                ItemPanel.Children.Add(itemView);
            }
        }

        private Border CreateItemView(ShopItem item)
        {
            bool isOwned = _savedSettings.OwnedItemIds.Contains(item.Id);
            bool isEquipped = IsItemEquippedInPreview(item);

            var border = new Border
            {
                Width = 60,
                Height = 80,
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(5),
                BorderBrush = isEquipped ? (Brush)FindResource("AccentColorBrush") : Brushes.LightGray,
                BorderThickness = new Thickness(isEquipped ? 2 : 1),
                Background = (Brush)FindResource("SecondaryBackgroundBrush"),
                Cursor = Cursors.Hand,
                Tag = item
            };

            var grid = new Grid();
            var image = new Image { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
            if (!string.IsNullOrEmpty(item.IconPath))
            {
                try { image.Source = LoadBitmapImageOnLoad(item.IconPath); }
                catch { /* Image not found */ }
            }

            var nameLabel = new TextBlock { Text = item.Name, FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 18), Foreground = (Brush)FindResource("PrimaryTextBrush") };
            var pricePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) };

            if (isOwned)
            {
                var ownedLabel = new TextBlock { Text = "보유 중", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.Green };
                pricePanel.Children.Add(ownedLabel);
            }
            else
            {
                var coinIcon = new Image { Source = LoadBitmapImageOnLoad("/images/coin.png"), Width = 10, Height = 10 };
                var priceLabel = new TextBlock { Text = item.Price.ToString("N0"), FontSize = 9, Margin = new Thickness(3, 0, 0, 0), Foreground = (Brush)FindResource("PrimaryTextBrush") };
                pricePanel.Children.Add(coinIcon);
                pricePanel.Children.Add(priceLabel);
            }

            grid.Children.Add(image);
            grid.Children.Add(nameLabel);
            grid.Children.Add(pricePanel);
            border.Child = grid;

            border.MouseLeftButtonDown += Item_Click;

            if (isEquipped)
            {
                _selectedItemBorder = border;
                _selectedShopItem = item;
            }

            return border;
        }
        #endregion

        #region 미리보기 (Preview) 로직

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not ShopItem selectedItem) return;

            _selectedShopItem = selectedItem;

            if (_selectedItemBorder != null)
            {
                _selectedItemBorder.BorderBrush = Brushes.LightGray;
                _selectedItemBorder.BorderThickness = new Thickness(1);
            }
            border.BorderBrush = (Brush)FindResource("AccentColorBrush");
            border.BorderThickness = new Thickness(2);
            _selectedItemBorder = border;

            ApplyPreview(selectedItem);
            UpdateControlsVisibility();
        }

        private void ApplyPreview(ShopItem itemToEquip)
        {
            string currentHex = null;

            // ✨ [수정] 장신구(Accessory) 관련 if문 삭제
            // (이제 장신구도 다른 파츠와 동일하게 이 로직을 따릅니다)
            if (_previewSettings.EquippedParts.TryGetValue(itemToEquip.Type, out var current) && current.ItemId == itemToEquip.Id)
            {
                _previewSettings.EquippedParts.Remove(itemToEquip.Type);
            }
            else
            {
                _previewSettings.EquippedParts.TryGetValue(itemToEquip.Type, out var oldItem);
                currentHex = oldItem?.ColorHex;

                _previewSettings.EquippedParts[itemToEquip.Type] = new EquippedItemInfo(itemToEquip.Id, currentHex);
            }

            CharacterPreview.UpdateCharacter(_previewSettings);
            UpdateItemList();
        }

        private void ColorPalette_ColorChanged(object sender, Color newColor)
        {
            if (_selectedShopItem == null || !IsItemEquippedInPreview(_selectedShopItem)) return;

            string newColorHex = newColor.ToString();
            EquippedItemInfo itemInfo = null;

            // ✨ [수정] 장신구 관련 if문 삭제
            _previewSettings.EquippedParts.TryGetValue(_selectedShopItem.Type, out itemInfo);

            if (itemInfo != null)
            {
                itemInfo.ColorHex = newColorHex;
                CharacterPreview.UpdateCharacter(_previewSettings); // 캐릭터 새로고침
            }
        }

        private void UpdateControlsVisibility()
        {
            if (_hslPicker == null) return;

            if (_selectedShopItem != null && _selectedShopItem.CanChangeColor && IsItemEquippedInPreview(_selectedShopItem))
            {
                _hslPicker.Visibility = Visibility.Visible;

                EquippedItemInfo itemInfo = null;
                // ✨ [수정] 장신구 관련 if문 삭제
                _previewSettings.EquippedParts.TryGetValue(_selectedShopItem.Type, out itemInfo);

                string currentHex = itemInfo?.ColorHex;
                Color currentColor = Colors.White; // 기본값
                if (!string.IsNullOrEmpty(currentHex))
                {
                    try { currentColor = (Color)ColorConverter.ConvertFromString(currentHex); }
                    catch { /* 기본값 White 사용 */ }
                }

                (double H, double S, double L) hsl = WpfColorToHsl(currentColor);
                _hslPicker.SetHsl(hsl.H, hsl.S, hsl.L);
            }
            else
            {
                _hslPicker.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 저장 및 되돌리기 로직

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            int totalCost = 0;
            var itemsToBuy = new List<ShopItem>();

            // 1. 구매할 아이템 목록 계산 (파츠)
            // (✨ 이제 이 루프가 장신구도 함께 처리합니다)
            foreach (var previewPart in _previewSettings.EquippedParts.Values)
            {
                if (previewPart != null && !_savedSettings.OwnedItemIds.Contains(previewPart.ItemId))
                {
                    var shopItem = _allItems.FirstOrDefault(i => i.Id == previewPart.ItemId);
                    if (shopItem != null)
                    {
                        itemsToBuy.Add(shopItem);
                        totalCost += shopItem.Price;
                    }
                }
            }

            // 2. ✨ [수정] 장신구(Accessory) 관련 루프 삭제
            // (위의 파츠 루프에 통합됨)

            itemsToBuy = itemsToBuy.Distinct().ToList();
            totalCost = itemsToBuy.Sum(i => i.Price);

            // 3. 코인 확인 및 구매
            if (totalCost > 0)
            {
                if (_savedSettings.Coins < totalCost)
                {
                    MessageBox.Show($"코인이 부족합니다. (필요: {totalCost:N0} 코인)", "구매 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"새 아이템 {itemsToBuy.Count}개를 {totalCost:N0} 코인에 구매하고 저장하시겠습니까?", "구매 확인", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.No)
                {
                    return;
                }

                _savedSettings.Coins -= totalCost;
                foreach (var item in itemsToBuy)
                {
                    _savedSettings.OwnedItemIds.Add(item.Id);
                }
            }

            // 4. _savedSettings에 _previewSettings를 덮어쓰기 (DeepClone)
            _savedSettings.EquippedParts = DeepClone(_previewSettings.EquippedParts);
            // ✨ [수정] 장신구 관련 DeepClone 삭제

            // 5. 저장 및 UI 갱신
            DataManager.SaveSettings(_savedSettings);
            CoinDisplay.Text = _savedSettings.Coins.ToString("N0");
            UpdateItemList();
            MessageBox.Show("성공적으로 저장되었습니다!", "저장 완료");
        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            _previewSettings = DeepClone(_savedSettings);
            CharacterPreview.UpdateCharacter(_previewSettings);
            UpdateItemList();
            UpdateControlsVisibility();
        }

        #endregion

        #region 유틸리티 메서드
        private bool IsItemEquippedInPreview(ShopItem item)
        {
            // ✨ [수정] 장신구 관련 if문 삭제
            return _previewSettings.EquippedParts.TryGetValue(item.Type, out var equipped) && equipped.ItemId == item.Id;
        }

        private string GetCategoryDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.FrontHair => "앞머리",
                ItemType.BackHair => "뒷머리",
                ItemType.Face => "얼굴",
                ItemType.Top => "상의",
                ItemType.Outerwear => "아우터",
                ItemType.Bottom => "하의",
                ItemType.Accessory => "장신구",
                ItemType.Tail => "꼬리",
                ItemType.AnimalEar => "동물귀",
                ItemType.Shoes => "신발",
                ItemType.Background => "배경",
                ItemType.Scalp => "두피",
                ItemType.Head => "머리",
                ItemType.Upper => "상체",
                ItemType.Lower => "하체",
                _ => itemType.ToString(),
            };
        }

        private int GetCategoryOrder(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Background => 1,
                ItemType.Tail => 2,
                ItemType.Lower => 3,
                ItemType.Bottom => 4,
                ItemType.Upper => 5,
                ItemType.Top => 6,
                ItemType.Outerwear => 7,
                ItemType.Head => 8,
                ItemType.Scalp => 9,
                ItemType.BackHair => 10,
                ItemType.Face => 11,
                ItemType.AnimalEar => 12,
                ItemType.FrontHair => 13,
                ItemType.Accessory => 14,
                ItemType.Shoes => 15,
                _ => 100,
            };
        }

        private T DeepClone<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

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
                System.Diagnostics.Debug.WriteLine($"[LoadBitmapImageOnLoad] 아이콘 로드 실패 {relativePath}: {ex.Message}");
                return null;
            }
        }

        private (double H, double S, double L) WpfColorToHsl(Color wpfColor)
        {
            double r = wpfColor.R / 255.0;
            double g = wpfColor.G / 255.0;
            double b = wpfColor.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            double h = 0, s = 0, l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // 회색조
            }
            else
            {
                double delta = max - min;
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

                if (max == r)
                {
                    h = (g - b) / delta + (g < b ? 6.0 : 0.0);
                }
                else if (max == g)
                {
                    h = (b - r) / delta + 2.0;
                }
                else // max == b
                {
                    h = (r - g) / delta + 4.0;
                }

                h /= 6.0; // 0-1 범위로 정규화
            }

            return (h * 360.0, s, l); // H(0-360), S(0-1), L(0-1)
        }

        #endregion
    }
}