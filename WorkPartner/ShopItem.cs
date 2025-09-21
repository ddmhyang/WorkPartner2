using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WorkPartner
{
    // JSON의 문자열을 Enum으로 변환할 때, 대소문자를 구분하지 않도록 설정합니다.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ItemType
    {
        // 렌더링 순서 및 JSON 파일 기준 정렬
        Background,
        Body,
        AnimalTail, // 꼬리
        Face,       // 얼굴 기본 요소 (레거시 또는 기본)
        EyeShape,   // 눈 모양
        MouthShape, // 입 모양
        HairStyle,  // 헤어 스타일
        AnimalEar,  // 동물 귀
        Clothes,    // 한벌 옷
        Top,        // 상의
        Bottom,     // 하의
        Shoes,      // 신발
        Accessory,  // 장신구
        FaceDeco,   // 얼굴 장식 (안경, 수염 등)
        Cushion,    // 쿠션 (배경 아이템)

        // 색상 전용 타입 (실제 아이템 아님)
        HairColor,
        EyeColor,
        ClothesColor,
        CushionColor
    }

    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public string ImagePath { get; set; }
        public ItemType Type { get; set; }
        public string ColorValue { get; set; } // 색상 아이템을 위한 속성 추가
    }
}

