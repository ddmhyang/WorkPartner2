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

        // ✨ [추가] 아이콘 경로 (AvatarPage에서 아이템 목록 표시에 사용)
        public string IconPath { get; set; }

        // ✨ [추가] 이 아이템이 색상인지 여부
        public bool IsColor { get; set; }

        // ✨ [추가] 색상 값 (IsColor가 true일 때 사용)
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