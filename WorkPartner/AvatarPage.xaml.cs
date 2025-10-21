// [수정] WorkPartner/AvatarPage.xaml.cs
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
        private ItemType _selectedCategory = ItemType.HairFront; // 기본 카테고리
        private Button _selectedCategoryButton = null;

        private ShopItem _selectedShopItem; // 현재 상점에서 선택한 아이템
        private Border _selectedItemBorder; // 현재 상점에서 선택한 아이템의 테두리
        private bool _isSliderEventBlocked = false; // 슬라이더 값 변경 이벤트 중복 방지 플래그

        public AvatarPage()
        {
            InitializeComponent();
            LoadData();
        }

        /// <summary>
        /// 데이터를 로드하고 미리보기 상태를 초기화합니다.
        /// </summary>
        public void LoadData()
        {
            _savedSettings = DataManager.LoadSettings();
            _previewSettings = DeepClone(_savedSettings); // ✨ 미리보기용 설정 복사

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

            CharacterPreview.UpdateCharacter(_previewSettings); // ✨ 미리보기 설정으로 캐릭터 표시
            PopulateCategories();
            UpdateItemList();
            UpdateControlsVisibility(); // 컨트롤 숨김/표시
        }

        #region 카테고리 및 아이템 목록 UI

        /// <summary>
        /// 상단 카테고리 버튼을 생성합니다.
        /// </summary>
        private void PopulateCategories()
        {
            CategoryPanel.Children.Clear();

            // ✨ 새 ItemType 순서대로 정렬
            var categories = _allItems.Select(i => i.Type).Distinct().OrderBy(t => GetCategoryOrder(t));

            foreach (var category in categories)
            {
                // 'Body'는 기본 파츠이므로 상점에서 선택하지 않음
                if (category == ItemType.Body) continue;

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
                UpdateControlsVisibility(); // 카테고리 변경 시 슬라이더 숨김
            }
        }

        /// <summary>
        /// 아이템 목록 UI를 다시 그립니다.
        /// </summary>
        private void UpdateItemList()
        {
            ItemPanel.Children.Clear();
            _selectedItemBorder = null; // 선택 테두리 초기화

            var itemsInCategory = _allItems.Where(i => i.Type == _selectedCategory);

            foreach (var item in itemsInCategory)
            {
                var itemView = CreateItemView(item);
                ItemPanel.Children.Add(itemView);
            }
        }

        /// <summary>
        /// 아이템 1개의 UI (테두리, 아이콘, 가격)를 생성합니다.
        /// </summary>
        private Border CreateItemView(ShopItem item)
        {
            bool isOwned = _savedSettings.OwnedItemIds.Contains(item.Id);
            bool isEquipped = IsItemEquippedInPreview(item); // ✨ 미리보기 상태 기준

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
                try { image.Source = new BitmapImage(new Uri(item.IconPath, UriKind.Relative)); }
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
                var coinIcon = new Image { Source = new BitmapImage(new Uri("/images/coin.png", UriKind.Relative)), Width = 10, Height = 10 };
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
                _selectedItemBorder = border; // 현재 장착 중인 아이템을 선택된 것으로 설정
                _selectedShopItem = item;
            }

            return border;
        }

        #endregion

        #region 미리보기 (Preview) 로직

        /// <summary>
        /// 아이템 클릭 시, 구매/장착 대신 '미리보기'를 적용합니다.
        /// </summary>
        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not ShopItem selectedItem) return;

            _selectedShopItem = selectedItem;

            // 시각적 선택 효과 업데이트
            if (_selectedItemBorder != null)
            {
                _selectedItemBorder.BorderBrush = Brushes.LightGray;
                _selectedItemBorder.BorderThickness = new Thickness(1);
            }
            border.BorderBrush = (Brush)FindResource("AccentColorBrush");
            border.BorderThickness = new Thickness(2);
            _selectedItemBorder = border;

            // 미리보기 적용
            ApplyPreview(selectedItem);

            // 컨트롤 UI 업데이트
            UpdateControlsVisibility();
        }

        /// <summary>
        /// 선택한 아이템을 _previewSettings에 적용합니다.
        /// </summary>
        private void ApplyPreview(ShopItem itemToEquip)
        {
            if (itemToEquip.Type == ItemType.Accessories)
            {
                // 장신구: 중복 착용/해제 처리
                var existing = _previewSettings.EquippedAccessories.FirstOrDefault(e => e.ItemId == itemToEquip.Id);
                if (existing != null)
                {
                    // 이미 착용 중이면 해제
                    _previewSettings.EquippedAccessories.Remove(existing);
                }
                else if (_previewSettings.EquippedAccessories.Count < 3)
                {
                    // 3개 미만이면 새로 착용
                    _previewSettings.EquippedAccessories.Add(new EquippedItemInfo(itemToEquip.Id, 0));
                }
                else
                {
                    MessageBox.Show("장신구는 최대 3개까지만 착용할 수 있습니다.", "알림");
                }
            }
            else
            {
                // 일반 파츠: 교체 또는 해제
                if (_previewSettings.EquippedParts.TryGetValue(itemToEquip.Type, out var current) && current.ItemId == itemToEquip.Id)
                {
                    // 이미 착용한 아이템을 다시 클릭하면 해제 (기본 아이템으로 돌아가기 - 여기서는 그냥 제거)
                    _previewSettings.EquippedParts.Remove(itemToEquip.Type);
                    // TODO: 기본 아이템으로 되돌리는 로직이 필요할 수 있습니다.
                }
                else
                {
                    // 다른 아이템으로 교체
                    _previewSettings.EquippedParts[itemToEquip.Type] = new EquippedItemInfo(itemToEquip.Id, 0);
                }
            }

            // 캐릭터 디스플레이 업데이트
            CharacterPreview.UpdateCharacter(_previewSettings);
            // 아이템 목록 테두리(장착 여부) 업데이트
            UpdateItemList();
        }

        /// <summary>
        /// 슬라이더 값이 변경되면, 선택된 아이템의 색조를 _previewSettings에 적용합니다.
        /// </summary>
        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedShopItem == null || _isSliderEventBlocked) return;

            double newHue = e.NewValue;
            EquippedItemInfo itemInfo = null;

            if (_selectedShopItem.Type == ItemType.Accessories)
            {
                itemInfo = _previewSettings.EquippedAccessories.FirstOrDefault(i => i.ItemId == _selectedShopItem.Id);
            }
            else
            {
                _previewSettings.EquippedParts.TryGetValue(_selectedShopItem.Type, out itemInfo);
            }

            if (itemInfo != null)
            {
                itemInfo.HueShift = newHue;
                CharacterPreview.UpdateCharacter(_previewSettings); // 실시간 미리보기
            }
        }

        /// <summary>
        /// 아이템 선택 변경 시, 슬라이더의 표시 여부와 현재 값을 업데이트합니다.
        /// </summary>
        private void UpdateControlsVisibility()
        {
            if (_selectedShopItem != null && _selectedShopItem.CanChangeColor && IsItemEquippedInPreview(_selectedShopItem))
            {
                HueSliderPanel.Visibility = Visibility.Visible;

                // 현재 아이템의 Hue 값을 찾아 슬라이더에 설정
                EquippedItemInfo itemInfo = null;
                if (_selectedShopItem.Type == ItemType.Accessories)
                    itemInfo = _previewSettings.EquippedAccessories.FirstOrDefault(i => i.ItemId == _selectedShopItem.Id);
                else
                    _previewSettings.EquippedParts.TryGetValue(_selectedShopItem.Type, out itemInfo);

                // 이벤트 핸들러가 이 값 변경으로 인해 실행되는 것을 방지
                _isSliderEventBlocked = true;
                HueSlider.Value = itemInfo?.HueShift ?? 0;
                _isSliderEventBlocked = false;
            }
            else
            {
                HueSliderPanel.Visibility = Visibility.Collapsed;
                _isSliderEventBlocked = true;
                HueSlider.Value = 0;
                _isSliderEventBlocked = false;
            }
        }

        #endregion

        #region 저장 및 되돌리기 로직

        /// <summary>
        /// '저장하기' 버튼 클릭: 변경 사항을 구매하고 _savedSettings에 적용합니다.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            long totalCost = 0;
            var itemsToBuy = new List<ShopItem>();

            // 1. 구매할 아이템 목록 계산 (파츠)
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

            // 2. 구매할 아이템 목록 계산 (장신구)
            foreach (var previewAccessory in _previewSettings.EquippedAccessories)
            {
                if (previewAccessory != null && !_savedSettings.OwnedItemIds.Contains(previewAccessory.ItemId))
                {
                    var shopItem = _allItems.FirstOrDefault(i => i.Id == previewAccessory.ItemId);
                    if (shopItem != null)
                    {
                        itemsToBuy.Add(shopItem);
                        totalCost += shopItem.Price;
                    }
                }
            }

            // 중복 제거 (필요 시)
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

                // 코인 차감 및 아이템 추가
                _savedSettings.Coins -= totalCost;
                foreach (var item in itemsToBuy)
                {
                    _savedSettings.OwnedItemIds.Add(item.Id);
                }
            }

            // 4. _savedSettings에 _previewSettings를 덮어쓰기 (DeepClone)
            _savedSettings.EquippedParts = DeepClone(_previewSettings.EquippedParts);
            _savedSettings.EquippedAccessories = DeepClone(_previewSettings.EquippedAccessories);

            // 5. 저장 및 UI 갱신
            DataManager.SaveSettingsAndNotify(_savedSettings);
            CoinDisplay.Text = _savedSettings.Coins.ToString("N0");
            UpdateItemList(); // '보유 중' 상태 갱신
            MessageBox.Show("성공적으로 저장되었습니다!", "저장 완료");
        }

        /// <summary>
        /// '되돌리기' 버튼 클릭: _previewSettings를 _savedSettings로 되돌립니다.
        /// </summary>
        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            _previewSettings = DeepClone(_savedSettings);
            CharacterPreview.UpdateCharacter(_previewSettings);
            UpdateItemList();
            UpdateControlsVisibility();
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// _previewSettings 기준으로 아이템이 장착 중인지 확인합니다.
        /// </summary>
        private bool IsItemEquippedInPreview(ShopItem item)
        {
            if (item.Type == ItemType.Accessories)
            {
                return _previewSettings.EquippedAccessories.Any(e => e.ItemId == item.Id);
            }
            else
            {
                return _previewSettings.EquippedParts.TryGetValue(item.Type, out var equipped) && equipped.ItemId == item.Id;
            }
        }

        /// <summary>
        /// 새 ItemType에 맞는 카테고리 이름을 반환합니다.
        /// </summary>
        private string GetCategoryDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.HairFront => "앞머리",
                ItemType.HairBack => "뒷머리",
                ItemType.Eye => "눈",
                ItemType.Mouth => "입",
                ItemType.Clothes => "옷",
                ItemType.Accessories => "장신구",
                ItemType.Cushion => "방석",
                ItemType.Background => "배경",
                _ => itemType.ToString(),
            };
        }

        /// <summary>
        /// 카테고리 표시 순서를 반환합니다.
        /// </summary>
        private int GetCategoryOrder(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.HairFront => 1,
                ItemType.HairBack => 2,
                ItemType.Eye => 3,
                ItemType.Mouth => 4,
                ItemType.Clothes => 5,
                ItemType.Accessories => 6,
                ItemType.Cushion => 7,
                ItemType.Background => 8,
                _ => 99,
            };
        }

        /// <summary>
        /// JSON 직렬화/역직렬화를 이용해 객체를 깊은 복사(Deep Clone)합니다.
        /// </summary>
        private T DeepClone<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        #endregion
    }
}