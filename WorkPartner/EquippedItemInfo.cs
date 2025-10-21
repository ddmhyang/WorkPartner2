using System;

namespace WorkPartner
{
    /// <summary>
    /// 착용 중인 아이템의 ID와 색상(색조) 정보를 저장합니다.
    /// </summary>
    public class EquippedItemInfo
    {
        public Guid ItemId { get; set; }
        public double HueShift { get; set; } // 0~360도 색조 값 (0 = 원본색)

        public EquippedItemInfo()
        {
            ItemId = Guid.Empty;
            HueShift = 0;
        }

        public EquippedItemInfo(Guid id, double hue = 0)
        {
            ItemId = id;
            HueShift = hue;
        }
    }
}