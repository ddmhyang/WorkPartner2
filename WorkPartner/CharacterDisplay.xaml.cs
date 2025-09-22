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
        private List<ShopItem> _fullShopInventory;

        public CharacterDisplay()
        {
            InitializeComponent();
            LoadFullInventory();
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                UpdateCharacter();
            }
        }

        private void LoadFullInventory()
        {
            if (File.Exists(DataManager.ItemsDbFilePath))
            {
                try
                {
                    var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                    // Newtonsoft.Json을 사용하여 대소문자 구분 없이 Enum을 파싱합니다.
                    _fullShopInventory = JsonConvert.DeserializeObject<List<ShopItem>>(json, new StringEnumConverter()) ?? new List<ShopItem>();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"items_db.json 파일을 읽는 중 오류가 발생했습니다: {ex.Message}", "데이터 오류");
                    _fullShopInventory = new List<ShopItem>();
                }
            }
            else
            {
                _fullShopInventory = new List<ShopItem>();
            }
        }

        // 기존 메서드: 설정 파일에서 직접 로드
        public void UpdateCharacter()
        {
            var settings = DataManager.LoadSettings();
            RenderCharacter(settings.EquippedItems, settings.CustomColors);
        }

        // 새로 추가된 메서드: 외부에서 장비/색상 정보를 받아 렌더링
        public void UpdateCharacter(Dictionary<ItemType, Guid> equippedItems, Dictionary<ItemType, string> customColors)
        {
            RenderCharacter(equippedItems, customColors);
        }

        private void RenderCharacter(Dictionary<ItemType, Guid> equippedItems, Dictionary<ItemType, string> customColors)
        {
            CharacterCanvas.Children.Clear();

            // 아이템이 그려지는 순서를 정의합니다. (아래 -> 위)
            var layers = new List<ItemType>
            {
                ItemType.Background,
                ItemType.Body,
                ItemType.AnimalTail, // 꼬리가 옷보다 먼저 그려지도록 순서 조정
                ItemType.Face,
                ItemType.EyeShape,
                ItemType.MouthShape,
                ItemType.Clothes,
                ItemType.Top,
                ItemType.Bottom,
                ItemType.Shoes,
                ItemType.HairStyle,
                ItemType.AnimalEar,
                ItemType.Accessory,
                ItemType.FaceDeco,
                ItemType.Cushion

            };

            foreach (var layerType in layers)
            {
                if (equippedItems.TryGetValue(layerType, out Guid itemId))
                {
                    var item = _fullShopInventory.FirstOrDefault(i => i.Id == itemId);
                    if (item != null && !string.IsNullOrEmpty(item.ImagePath))
                    {
                        var image = CreateImage(item.ImagePath);
                        if (image == null) continue;

                        // 색상 적용 로직
                        ItemType? colorType = null;
                        if (layerType == ItemType.HairStyle) colorType = ItemType.HairColor;
                        else if (layerType == ItemType.Face || layerType == ItemType.EyeShape) colorType = ItemType.EyeColor;
                        else if (layerType == ItemType.Top || layerType == ItemType.Bottom || layerType == ItemType.Clothes) colorType = ItemType.ClothesColor;

                        if (colorType.HasValue && customColors.TryGetValue(colorType.Value, out string colorHex))
                        {
                            try
                            {
                                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                                image.Effect = new TintColorEffect { TintColor = color };
                            }
                            catch { /* 잘못된 색상 코드 무시 */ }
                        }

                        CharacterCanvas.Children.Add(image);
                    }
                }
            }
        }

        private Image CreateImage(string path)
        {
            string absolutePath = Path.IsPathRooted(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

            if (!File.Exists(absolutePath))
            {
                return null;
            }

            return new Image
            {
                Source = new BitmapImage(new Uri(absolutePath)),
                Width = 150,
                Height = 150,
                Stretch = Stretch.Uniform
            };
        }
    }
}

