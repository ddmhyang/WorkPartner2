using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging; // Added for BitmapImage
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public partial class AvatarPage : UserControl
    {
        private AppSettings _settings;
        private List<ShopItem> _allItems;
        private ItemType _selectedCategory = ItemType.HairStyle;
        private Button _selectedCategoryButton = null;

        public AvatarPage()
        {
            InitializeComponent();
            LoadData();
        }

        public void LoadData()
        {
            _settings = DataManager.LoadSettings();
            CoinDisplay.Text = _settings.Coins.ToString("N0");

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

            CharacterPreview.UpdateCharacter();
            PopulateCategories();
            UpdateItemList();
        }

        private void PopulateCategories()
        {
            CategoryPanel.Children.Clear();
            var categories = _allItems.Select(i => i.Type).Distinct().OrderBy(t => t.ToString());

            foreach (var category in categories)
            {
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
            }
        }

        private void UpdateItemList()
        {
            ItemPanel.Children.Clear();
            var itemsInCategory = _allItems.Where(i => i.Type == _selectedCategory);

            foreach (var item in itemsInCategory)
            {
                var itemView = CreateItemView(item);
                ItemPanel.Children.Add(itemView);
            }
        }

        private Border CreateItemView(ShopItem item)
        {
            bool isOwned = _settings.OwnedItemIds.Contains(item.Id);
            bool isEquipped = IsItemEquipped(item);

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

            // ✨ This is the corrected image loading logic
            var image = new Image
            {
                Width = 40,
                Height = 40,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 0, 0)
            };
            if (!string.IsNullOrEmpty(item.IconPath))
            {
                try
                {
                    image.Source = new BitmapImage(new Uri(item.IconPath, UriKind.Relative));
                }
                catch (Exception) { /* Image not found, leave it blank */ }
            }


            var nameLabel = new TextBlock
            {
                Text = item.Name,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 18),
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            };

            var pricePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 5)
            };

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

            return border;
        }

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is ShopItem selectedItem)
            {
                bool isOwned = _settings.OwnedItemIds.Contains(selectedItem.Id);

                if (isOwned)
                {
                    EquipItem(selectedItem);
                }
                else
                {
                    BuyItem(selectedItem);
                }
            }
        }

        private void BuyItem(ShopItem item)
        {
            if (_settings.Coins >= item.Price)
            {
                if (MessageBox.Show($"{item.Name}을(를) {item.Price}코인에 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _settings.Coins -= item.Price;
                    _settings.OwnedItemIds.Add(item.Id);
                    DataManager.SaveSettingsAndNotify(_settings);
                    CoinDisplay.Text = _settings.Coins.ToString("N0");
                    EquipItem(item);
                }
            }
            else
            {
                MessageBox.Show("코인이 부족합니다.");
            }
        }

        private void EquipItem(ShopItem item)
        {
            if (item.IsColor)
            {
                _settings.CustomColors[item.Type] = item.ColorValue;
            }
            else
            {
                if (_settings.EquippedItems.ContainsKey(item.Type))
                {
                    _settings.EquippedItems.Remove(item.Type);
                }
                _settings.EquippedItems[item.Type] = item.Id;
            }

            DataManager.SaveSettingsAndNotify(_settings);
            CharacterPreview.UpdateCharacter();
            UpdateItemList();
        }

        private bool IsItemEquipped(ShopItem item)
        {
            if (item.IsColor)
            {
                return _settings.CustomColors.TryGetValue(item.Type, out var color) && color == item.ColorValue;
            }
            else
            {
                return _settings.EquippedItems.TryGetValue(item.Type, out var equippedId) && equippedId == item.Id;
            }
        }

        private string GetCategoryDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.HairStyle => "머리",
                ItemType.HairColor => "머리색",
                ItemType.EyeShape => "눈",
                ItemType.EyeColor => "눈색",
                ItemType.MouthShape => "입",
                ItemType.Clothes => "옷",
                ItemType.Background => "배경",
                ItemType.Body => "몸",
                _ => itemType.ToString(),
            };
        }
    }
}