using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public partial class AvatarPage : UserControl, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;

        private Dictionary<ItemType, Guid> _tempEquippedItems;
        private Dictionary<ItemType, string> _tempCustomColors;
        private List<Guid> _tempOwnedItemIds;
        private int _tempCoins;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public int TempCoins
        {
            get => _tempCoins;
            set
            {
                _tempCoins = value;
                OnPropertyChanged(nameof(TempCoins));
            }
        }

        public AvatarPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void LoadData()
        {
            _settings = DataManager.LoadSettings();
            LoadFullInventory();

            // 임시 데이터 초기화
            _tempEquippedItems = new Dictionary<ItemType, Guid>(_settings.EquippedItems);
            _tempCustomColors = new Dictionary<ItemType, string>(_settings.CustomColors);
            _tempOwnedItemIds = new List<Guid>(_settings.OwnedItemIds);
            TempCoins = _settings.Coins;

            UsernameTextBlock.Text = _settings.Username;
            PopulateCategories();
            UpdateCharacterPreview();
        }

        private void LoadFullInventory()
        {
            if (File.Exists(DataManager.ItemsDbFilePath))
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                _fullShopInventory = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
            }
            else
            {
                System.Windows.MessageBox.Show("아이템 데이터베이스 파일(items_db.json)을 찾을 수 없습니다.", "오류");
                _fullShopInventory = new List<ShopItem>();
            }
        }

        private void PopulateCategories()
        {
            var categories = _fullShopInventory.Select(i => i.Type)
                                               .Distinct()
                                               .Where(t => t != ItemType.Body)
                                               .OrderBy(t => t.ToString())
                                               .ToList();
            CategoryListBox.ItemsSource = categories;
            if (categories.Any())
            {
                CategoryListBox.SelectedIndex = 0;
            }
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ItemType selectedType)
            {
                RefreshItemsList(selectedType);

                var colorPickers = new[] { HairColorPicker, EyeColorPicker, ClothesColorPicker };
                foreach (var picker in colorPickers)
                {
                    picker.Visibility = Visibility.Collapsed;
                }

                if (IsColorCategory(selectedType))
                {
                    LoadCustomColorToPicker(selectedType);
                }
            }
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DisplayShopItem displayItem)
            {
                var clickedItem = displayItem.OriginalItem;
                if (clickedItem == null) return;

                if (!displayItem.IsOwned && clickedItem.Price > 0)
                {
                    if (System.Windows.MessageBox.Show($"'{clickedItem.Name}' 아이템을 {clickedItem.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        if (TempCoins >= clickedItem.Price)
                        {
                            TempCoins -= clickedItem.Price;
                            _tempOwnedItemIds.Add(clickedItem.Id);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("코인이 부족합니다.", "알림");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                if (IsColorCategory(clickedItem.Type))
                {
                    if (!string.IsNullOrEmpty(clickedItem.ColorValue))
                    {
                        _tempCustomColors[clickedItem.Type] = clickedItem.ColorValue;
                    }
                }
                else
                {
                    if (_tempEquippedItems.TryGetValue(clickedItem.Type, out Guid currentId) && currentId == clickedItem.Id)
                    {
                        _tempEquippedItems.Remove(clickedItem.Type);
                    }
                    else
                    {
                        _tempEquippedItems[clickedItem.Type] = clickedItem.Id;
                    }
                }

                RefreshItemsList();
                UpdateCharacterPreview();
            }
        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData(); // 저장된 설정으로 모든 임시 데이터를 다시 로드
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.EquippedItems = _tempEquippedItems;
            _settings.CustomColors = _tempCustomColors;
            _settings.OwnedItemIds = _tempOwnedItemIds;
            _settings.Coins = TempCoins;
            DataManager.SaveSettingsAndNotify(_settings);
            System.Windows.MessageBox.Show("변경 사항이 저장되었습니다.", "저장 완료");
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (sender is ColorPicker picker && picker.Tag is ItemType colorType && picker.SelectedColor.HasValue)
            {
                if (!IsColorCategory(colorType)) return;
                _tempCustomColors[colorType] = picker.SelectedColor.Value.ToString();
                UpdateCharacterPreview();
            }
        }

        private void UpdateCharacterPreview()
        {
            CharacterPreviewControl.UpdateCharacter(_tempEquippedItems, _tempCustomColors);
        }

        private void RefreshItemsList(ItemType? type = null)
        {
            var selectedType = type ?? CategoryListBox.SelectedItem as ItemType?;
            if (selectedType.HasValue)
            {
                var itemsToShow = _fullShopInventory.Where(item => item.Type == selectedType.Value).ToList();
                var displayItems = itemsToShow.Select(item => new DisplayShopItem(item, _tempOwnedItemIds, _tempEquippedItems)).ToList();
                ItemsListView.ItemsSource = displayItems;
            }
        }

        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor || type == ItemType.EyeColor || type == ItemType.ClothesColor || type == ItemType.CushionColor;
        }

        private void LoadCustomColorToPicker(ItemType type)
        {
            ColorPicker targetPicker = null;
            if (type == ItemType.HairColor) targetPicker = HairColorPicker;
            else if (type == ItemType.EyeColor) targetPicker = EyeColorPicker;
            else if (type == ItemType.ClothesColor) targetPicker = ClothesColorPicker;

            if (targetPicker == null) return;

            targetPicker.Visibility = Visibility.Visible;
            if (_tempCustomColors.TryGetValue(type, out string colorHex))
            {
                try
                {
                    targetPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(colorHex);
                }
                catch { targetPicker.SelectedColor = Colors.White; }
            }
            else
            {
                targetPicker.SelectedColor = Colors.White;
            }
        }
    }
}

