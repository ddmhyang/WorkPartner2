using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ItemType Type { get; set; }
        public int Price { get; set; }
        public string ImagePath { get; set; }
        public string IconPath { get; set; }
        public bool IsColor { get; set; }
        public string ColorValue { get; set; }

        public ShopItem()
        {
            Id = Guid.NewGuid();
        }
    }

    public enum ItemType
    {
        HairStyle,
        HairColor,
        EyeShape,
        EyeColor,
        MouthShape,
        Clothes,
        Background,
        Body
    }
}