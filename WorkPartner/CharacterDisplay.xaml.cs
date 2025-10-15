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
            // LoadItems(); // Can be removed to avoid loading on init, will be loaded by UpdateCharacter
            // UpdateCharacter();
        }

        private void LoadItems()
        {
            // Avoid reloading if already loaded
            if (_allItems != null && _allItems.Count > 0) return;
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
            LoadItems();

            // Clear all images first
            BackgroundImage.Source = null;
            BodyImage.Source = null;
            ClothesImage.Source = null;
            EyeShapeImage.Source = null;
            MouthShapeImage.Source = null;
            HairStyleImage.Source = null;

            // Apply equipped items
            foreach (var equipped in _settings.EquippedItems)
            {
                var item = _allItems.FirstOrDefault(i => i.Id == equipped.Value);
                if (item != null)
                {
                    ApplyItem(item);
                }
            }

            // Apply custom colors
            foreach (var customColor in _settings.CustomColors)
            {
                ApplyColor(customColor.Key, customColor.Value);
            }
        }

        private void ApplyItem(ShopItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath)) return;

            // ✨ Corrected image loading with relative path
            try
            {
                var imageSource = new BitmapImage(new Uri(item.ImagePath, UriKind.Relative));
                var targetImage = GetImageForType(item.Type);

                if (targetImage != null)
                {
                    targetImage.Source = imageSource;
                }
            }
            catch (Exception) { /* Failed to load image */ }
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