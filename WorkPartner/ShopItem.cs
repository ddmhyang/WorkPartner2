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
    // 파일: WorkPartner/ShopItem.cs (또는 ItemType이 정의된 파일)

    // ✨ [수정] 새 아이템 목록과 100% 일치하는 열거형입니다.
    // 예전 파츠(Cushion, Eye, Mouth, Body, Clothes 등)를 모두 삭제했습니다.
    public enum ItemType
    {
        // ===================================
        // ✨ 새 파츠 목록 (상점 노출)
        // ===================================
        Background,     // 1. 배경
        Tail,           // 2. 꼬리
        Bottom,         // 4. 하의
        Top,            // 6. 상의
        Outerwear,      // 7. 아우터
        BackHair,       // 10. 뒷머리
        Face,           // 11. 얼굴
        AnimalEar,      // 12. 동물귀
        FrontHair,      // 13. 앞머리
        Accessory,      // 14. 장신구
        Shoes,      // 15. 신발

        // ===================================
        // ✨ 기본 파츠 (상점 비노출)
        // ===================================
        Lower,          // 3. 하체
        Upper,          // 5. 상체
        Head,           // 8. 머리
        Scalp           // 9. 두피
    }
}