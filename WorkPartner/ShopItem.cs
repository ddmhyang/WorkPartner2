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
        public ItemType Type { get; set; } // ✨ [수정] 아래의 새 enum을 사용합니다.
        public int Price { get; set; }
        public string ImagePath { get; set; } // 파츠 이미지 경로
        public string IconPath { get; set; } // 상점 아이콘 이미지 경로

        // ✨ [수정] IsColor와 ColorValue를 CanChangeColor로 대체합니다.
        // public bool IsColor { get; set; } // [삭제]
        // public string ColorValue { get; set; } // [삭제]
        public bool CanChangeColor { get; set; } // ✨ [추가] 색조 변경(Hue)이 가능한 아이템인지 여부

        public ShopItem()
        {
            Id = Guid.NewGuid();
        }
    }

    // ✨ [수정] ItemType을 새 사양에 맞게 완전히 변경합니다.
    public enum ItemType
    {
        Background,  // 배경 (레이어 1)
        Cushion,     // 방석 (레이어 2)
        HairBack,    // 뒷머리 (레이어 3)
        Body,        // 몸 (기본 파츠, 레이어 4)
        HairFront,   // 앞머리 (레이어 5)
        Eye,         // 눈 (레이어 6)
        Mouth,       // 입 (레이어 6)
        Clothes,     // 옷 (레이어 7)
        Accessories  // 장신구 (레이어 8, 중복 가능)
    }
}