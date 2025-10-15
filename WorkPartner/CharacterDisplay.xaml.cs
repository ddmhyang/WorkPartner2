using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        // TintColorEffect를 파츠별로 관리하기 위한 Dictionary
        private readonly Dictionary<Image, TintColorEffect> tintEffects = new Dictionary<Image, TintColorEffect>();

        public CharacterDisplay()
        {
            InitializeComponent();
            InitializeTintEffects();
        }

        // 각 이미지 파츠에 대한 TintColorEffect를 미리 생성하고 Dictionary에 저장합니다.
        private void InitializeTintEffects()
        {
            // 색상 변경이 필요한 파츠들만 추가합니다.
            tintEffects[HairBackPart] = new TintColorEffect();
            tintEffects[HairFrontPart] = new TintColorEffect();
            tintEffects[EyePart] = new TintColorEffect();
            tintEffects[ClothesPart] = new TintColorEffect();
            tintEffects[CushionPart] = new TintColorEffect();
            tintEffects[AccessoryPart1] = new TintColorEffect();
            tintEffects[AccessoryPart2] = new TintColorEffect();
            tintEffects[AccessoryPart3] = new TintColorEffect();

            // Mouth와 Background는 보통 색 변경 대상이 아니므로 제외합니다.
        }

        // CharacterAppearance 객체를 받아 캐릭터의 전체 모습을 업데이트하는 유일한 메서드입니다.
        public void UpdateCharacter(CharacterAppearance appearance)
        {
            // 각 파츠의 이미지와 색상을 설정합니다.
            SetPartImage(HairBackPart, "hairBack", appearance.HairBack, appearance.HairBackColor);
            SetPartImage(HairFrontPart, "hairFront", appearance.HairFront, appearance.HairFrontColor);
            SetPartImage(EyePart, "eye", appearance.Eye, appearance.EyeColor);
            SetPartImage(MouthPart, "mouth", appearance.Mouth, null); // 입은 색상 변경 없음
            SetPartImage(ClothesPart, "clothes", appearance.Clothes, appearance.ClothesColor);
            SetPartImage(CushionPart, "cushion", appearance.Cushion, appearance.CushionColor);
            SetPartImage(BackgroundPart, "background", appearance.Background, null); // 배경은 색상 변경 없음

            // 장신구는 List로 관리되므로 별도 처리합니다.
            UpdateAccessories(appearance.Accessories, appearance.AccessoryColors);
        }

        // 장신구 목록을 업데이트합니다.
        private void UpdateAccessories(List<string> accessoryIds, List<string> accessoryColors)
        {
            var accessoryParts = new[] { AccessoryPart1, AccessoryPart2, AccessoryPart3 };

            for (int i = 0; i < accessoryParts.Length; i++)
            {
                if (i < accessoryIds.Count)
                {
                    // 착용할 장신구가 있으면 이미지와 색상 설정
                    SetPartImage(accessoryParts[i], "accessories", accessoryIds[i], accessoryColors[i]);
                }
                else
                {
                    // 착용할 장신구가 없으면 이미지 제거
                    accessoryParts[i].Source = null;
                }
            }
        }

        // 특정 파츠의 이미지 소스와 색상을 설정하는 도우미 메서드입니다.
        private void SetPartImage(Image imageControl, string category, string itemId, string colorHex)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                imageControl.Source = null;
                return;
            }

            // 이미지 경로를 생성합니다. 예: /images/character/hairFront1.png
            var uri = new Uri($"/images/character/{itemId}.png", UriKind.Relative);
            imageControl.Source = new BitmapImage(uri);

            // 색상 값을 적용합니다.
            if (tintEffects.TryGetValue(imageControl, out TintColorEffect effect) && colorHex != null)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    effect.TintColor = color;
                    imageControl.Effect = effect;
                }
                catch (FormatException)
                {
                    // 유효하지 않은 색상 코드일 경우 효과를 적용하지 않습니다.
                    imageControl.Effect = null;
                }
            }
            else
            {
                // 색상 변경이 필요 없거나 지원되지 않는 파츠는 효과를 제거합니다.
                imageControl.Effect = null;
            }
        }
    }
}
