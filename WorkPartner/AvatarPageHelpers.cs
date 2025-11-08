using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WorkPartner
{
    /// <summary>
    /// AvatarPage에서 사용하는 헬퍼 함수들을 모아둔 클래스입니다.
    /// </summary>
    public class AvatarPageHelpers
    {
        /// <summary>
        /// ✨ [수정됨] Hue(숫자) 대신 Color(문자열)를 저장합니다.
        /// </summary>
        public void SaveEquippedColor(string partType, Color color)
        {
            var settings = DataManager.LoadSettings();
            var partToUpdate = new List<EquippedItemInfo>();

            if (partType == "Hair")
            {
                if (settings.EquippedParts.TryGetValue(ItemType.BackHair, out var backHair))
                    partToUpdate.Add(backHair);
                if (settings.EquippedParts.TryGetValue(ItemType.FrontHair, out var frontHair))
                    partToUpdate.Add(frontHair);
            }
            // (다른 파츠 타입 추가 가능) ...

            if (partToUpdate.Count > 0)
            {
                // ✨ [핵심 수정] Color -> Hex 문자열로 변환
                string hex = color.ToString(); // (예: "#FFFF0000")

                foreach (var partInfo in partToUpdate)
                {
                    if (partInfo != null)
                    {
                        // ✨ [핵심 수정] HueShift 대신 ColorHex에 저장
                        partInfo.ColorHex = hex;
                    }
                }
                DataManager.SaveSettings(settings);
            }
        }
    }
}