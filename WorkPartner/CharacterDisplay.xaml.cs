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
        private AppSettings _settings;
        private List<ShopItem> _allItems;
        private readonly Dictionary<ItemType, TintColorEffect> _colorEffects = new();

        public CharacterDisplay()
        {
            InitializeComponent();
            LoadItems();
            UpdateCharacter();
        }

        private void LoadItems()
        {
            try
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                _allItems = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
            }
            catch
            {
                _allItems = new List<ShopItem>();
            }
        }

        public void UpdateCharacter()
        {
            _settings = DataManager.LoadSettings();
            if (_allItems == null || _allItems.Count == 0) LoadItems();

            // 모든 이미지 초기화
            BackgroundImage.Source = null;
            BodyImage.Source = null;
            ClothesImage.Source = null;
            EyeShapeImage.Source = null;
            MouthShapeImage.Source = null;
            HairStyleImage.Source = null;

            // 장착된 아이템 표시
            foreach (var equipped in _settings.EquippedItems)
            {
                var item = _allItems.FirstOrDefault(i => i.Id == equipped.Value);
                if (item != null)
                {
                    ApplyItem(item);
                }
            }

            // 커스텀 색상 적용
            foreach (var customColor in _settings.CustomColors)
            {
                ApplyColor(customColor.Key, customColor.Value);
            }
        }

        private void ApplyItem(ShopItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath)) return;

            var imageSource = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute));
            var targetImage = GetImageForType(item.Type);

            if (targetImage != null)
            {
                targetImage.Source = imageSource;
            }
        }

        private void ApplyColor(ItemType type, string colorHex)
        {
            var targetImage = GetImageForType(type, isColor: true);
            if (targetImage == null) return;

            if (!_colorEffects.ContainsKey(type))
            {
                _colorEffects[type] = new TintColorEffect();
                targetImage.Effect = _colorEffects[type];
            }
            _colorEffects[type].TintColor = (Color)ColorConverter.ConvertFromString(colorHex);
        }

        private Image GetImageForType(ItemType type, bool isColor = false)
        {
            if (isColor)
            {
                return type switch
                {
                    ItemType.HairColor => HairStyleImage,
                    ItemType.EyeColor => EyeShapeImage,
                    // 옷 색상 등 다른 색상 아이템이 추가될 경우 여기에 추가
                    _ => null,
                };
            }

            return type switch
            {
                ItemType.Background => BackgroundImage,
                ItemType.Body => BodyImage,
                ItemType.Clothes => ClothesImage,
                ItemType.EyeShape => EyeShapeImage,
                ItemType.MouthShape => MouthShapeImage,
                ItemType.HairStyle => HairStyleImage,
                _ => null,
            };
        }
    }
}