using System;

namespace WorkPartner
{
    /// <summary>
    /// 착용 중인 아이템의 ID와 색상(색조) 정보를 저장합니다.
    /// </summary>
    public class EquippedItemInfo
    {
        public Guid ItemId { get; set; }

        // ✨ [수정] HueShift(double) 대신 ColorHex(string)를 사용
        public string ColorHex { get; set; }

        public EquippedItemInfo()
        {
            ItemId = Guid.Empty;
            ColorHex = null; // 기본값은 null
        }

        // ✨ [수정] 생성자도 ColorHex를 받도록 (또는 기본값 사용)
        public EquippedItemInfo(Guid id, string colorHex = null)
        {
            ItemId = id;
            ColorHex = colorHex;
        }
    }
}